using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Infrastructure.Persistence;
using QRCoder;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// QR/enlaces de cobro: CRUD de cuentas de cobro y generación del QR de pago. El monto puede
/// derivarse del saldo de una factura (vía <see cref="CobranzaCalculator"/>). Aislado por EmpresaId.
/// </summary>
public class CobroQrService : ICobroQrService
{
    private const string AuditModule = "COBRANZA";
    private const string Activo = "ACTIVO";
    private const string Inactivo = "INACTIVO";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public CobroQrService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<CuentaCobroDto>> ListarCuentasAsync(int empresaId, CancellationToken ct = default)
        => await _db.CuentasCobro.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.EstadoCodigo == Activo)
            .OrderBy(c => c.Nombre).Select(c => ToDto(c)).ToListAsync(ct);

    public async Task<Result<CuentaCobroDto>> CrearCuentaAsync(int empresaId, CrearCuentaCobroRequest request, string? actor, CancellationToken ct = default)
    {
        if (Validar(request) is { } err) return Result<CuentaCobroDto>.Fail(err, "VALIDATION");
        var c = new CuentaCobro
        {
            EmpresaId = empresaId, Tipo = NormTipo(request.Tipo), Nombre = request.Nombre.Trim(),
            Banco = request.Banco?.Trim(), NumeroCuenta = request.NumeroCuenta?.Trim(),
            Titular = request.Titular?.Trim(), UrlPago = request.UrlPago?.Trim(),
            EstadoCodigo = Activo, CreatedBy = actor,
        };
        _db.CuentasCobro.Add(c);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CUENTA_CREAR", c.Nombre, c.Id);
        return Result<CuentaCobroDto>.Ok(ToDto(c));
    }

    public async Task<Result<CuentaCobroDto>> ActualizarCuentaAsync(int empresaId, int id, CrearCuentaCobroRequest request, string? actor, CancellationToken ct = default)
    {
        if (Validar(request) is { } err) return Result<CuentaCobroDto>.Fail(err, "VALIDATION");
        var c = await _db.CuentasCobro.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (c is null) return Result<CuentaCobroDto>.Fail("Cuenta de cobro no encontrada.", "CUENTA_NOT_FOUND");
        c.Tipo = NormTipo(request.Tipo); c.Nombre = request.Nombre.Trim();
        c.Banco = request.Banco?.Trim(); c.NumeroCuenta = request.NumeroCuenta?.Trim();
        c.Titular = request.Titular?.Trim(); c.UrlPago = request.UrlPago?.Trim();
        c.UpdatedAt = DateTime.UtcNow; c.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CUENTA_EDITAR", c.Nombre, c.Id);
        return Result<CuentaCobroDto>.Ok(ToDto(c));
    }

    public async Task<Result> InactivarCuentaAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var c = await _db.CuentasCobro.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (c is null) return Result.Fail("Cuenta de cobro no encontrada.", "CUENTA_NOT_FOUND");
        if (c.EstadoCodigo == Inactivo) return Result.Fail("La cuenta ya está inactiva.", "INVALID_STATE");
        c.EstadoCodigo = Inactivo; c.UpdatedAt = DateTime.UtcNow; c.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CUENTA_INACTIVAR", c.Nombre, c.Id);
        return Result.Ok();
    }

    public async Task<Result<CobroQrDto>> GenerarQrAsync(int empresaId, GenerarQrCobroRequest request, CancellationToken ct = default)
    {
        decimal monto;
        var referencia = request.Referencia?.Trim();

        if (request.DteDocumentoId is int dteId)
        {
            var dte = await _db.DteDocumentos.AsNoTracking()
                .Where(d => d.Id == dteId && d.EmpresaId == empresaId)
                .Select(d => new { d.TotalPagar, d.NumeroControl })
                .FirstOrDefaultAsync(ct);
            if (dte is null) return Result<CobroQrDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

            var pagado = await _db.Set<PagoCliente>().AsNoTracking()
                .Where(p => p.DteDocumentoId == dteId && p.EstadoCodigo == PagoEstados.Confirmado)
                .SumAsync(p => (decimal?)p.Monto, ct) ?? 0m;
            monto = request.Monto ?? CobranzaCalculator.Saldo(dte.TotalPagar, pagado);
            referencia ??= dte.NumeroControl;
        }
        else
        {
            if (request.Monto is not decimal m || m <= 0)
                return Result<CobroQrDto>.Fail("Indica un monto mayor a cero o una factura.", "VALIDATION");
            monto = m;
        }

        if (monto <= 0) return Result<CobroQrDto>.Fail("El saldo a cobrar es cero.", "VALIDATION");
        referencia ??= $"COBRO-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var cuenta = request.CuentaCobroId is int cid
            ? await _db.CuentasCobro.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid && c.EmpresaId == empresaId && c.EstadoCodigo == Activo, ct)
            : await _db.CuentasCobro.AsNoTracking().Where(c => c.EmpresaId == empresaId && c.EstadoCodigo == Activo).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (cuenta is null)
            return Result<CobroQrDto>.Fail("No hay una cuenta de cobro activa. Configura una primero.", "CUENTA_NOT_FOUND");

        var montoStr = monto.ToString("0.00", CultureInfo.InvariantCulture);
        var payload = !string.IsNullOrWhiteSpace(cuenta.UrlPago)
            ? cuenta.UrlPago!.Replace("{monto}", montoStr).Replace("{referencia}", Uri.EscapeDataString(referencia))
            : $"Pago a {cuenta.Titular ?? cuenta.Nombre} | {cuenta.Banco} {cuenta.NumeroCuenta} | Monto: $ {montoStr} | Ref: {referencia}";

        return Result<CobroQrDto>.Ok(new CobroQrDto
        {
            Monto = monto,
            Referencia = referencia,
            CuentaCobroId = cuenta.Id,
            CuentaNombre = cuenta.Nombre,
            CuentaTipo = cuenta.Tipo,
            Banco = cuenta.Banco,
            NumeroCuenta = cuenta.NumeroCuenta,
            Titular = cuenta.Titular,
            DteDocumentoId = request.DteDocumentoId,
            Payload = payload,
            EsLink = payload.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                  || payload.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
            QrPngBase64 = Convert.ToBase64String(GenerarPng(payload)),
        });
    }

    public async Task<Result<CobroPdfDto>> GenerarPdfAsync(int empresaId, GenerarQrCobroRequest request, CancellationToken ct = default)
    {
        var qr = await GenerarQrAsync(empresaId, request, ct);
        if (qr.IsFailure) return Result<CobroPdfDto>.Fail(qr.Error!, qr.ErrorCode);

        var empresa = await _db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new { e.RazonSocial, e.NombreComercial, e.LogoBlob })
            .FirstOrDefaultAsync(ct);
        if (empresa is null) return Result<CobroPdfDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        var pdf = CobroPdfBuilder.Generar(new CobroPdfModel
        {
            EmpresaNombre = string.IsNullOrWhiteSpace(empresa.NombreComercial) ? empresa.RazonSocial : empresa.NombreComercial!,
            LogoPng = empresa.LogoBlob,
            Cobro = qr.Value!,
        });

        return Result<CobroPdfDto>.Ok(new CobroPdfDto
        {
            FileName = $"cobro-{qr.Value!.Referencia}.pdf",
            Pdf = pdf,
        });
    }

    private static byte[] GenerarPng(string contenido)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(8);
    }

    private static string? Validar(CrearCuentaCobroRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre)) return "El nombre de la cuenta es obligatorio.";
        return null;
    }

    private static string NormTipo(string? t)
        => string.IsNullOrWhiteSpace(t) ? CuentaCobroTipos.Transferencia : t.Trim().ToUpperInvariant();

    private static CuentaCobroDto ToDto(CuentaCobro c) => new()
    {
        Id = c.Id, Tipo = c.Tipo, Nombre = c.Nombre, Banco = c.Banco, NumeroCuenta = c.NumeroCuenta,
        Titular = c.Titular, UrlPago = c.UrlPago, EstadoCodigo = c.EstadoCodigo,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "CuentaCobro", EntidadId = entidadId.ToString(),
            Resultado = "OK", Detalle = detalle,
        });
}
