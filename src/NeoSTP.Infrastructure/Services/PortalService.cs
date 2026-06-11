using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Portal;
using NeoSTP.Domain.Core.Portal;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NEOPORTAL (V2-C2). Enlaces públicos para que el receptor consulte su documento o estado
/// de cuenta sin sesión. El token (aleatorio, 256 bits) viaja solo en la URL; en BD queda su
/// SHA-256. Expiración y revocación se validan en cada acceso. El enlace queda atado a
/// (EmpresaId, DteDocumentoId|ClienteId) al emitirse: no puede cruzar empresa ni cliente.
/// </summary>
public class PortalService : IPortalService
{
    private const string AuditModule = "NEOPORTAL";

    private readonly NeoStpDbContext _db;
    private readonly IDteDocumentosService _dteDocs;
    private readonly ICobranzaService _cobranza;
    private readonly ICobroQrService _cobroQr;
    private readonly IAuditoriaService _auditoria;
    private readonly NeoSTP.Infrastructure.Diagnostics.NeoStpMetrics? _metrics;

    public PortalService(NeoStpDbContext db, IDteDocumentosService dteDocs, ICobranzaService cobranza, ICobroQrService cobroQr, IAuditoriaService auditoria,
        NeoSTP.Infrastructure.Diagnostics.NeoStpMetrics? metrics = null)
    {
        _db = db;
        _dteDocs = dteDocs;
        _cobranza = cobranza;
        _cobroQr = cobroQr;
        _auditoria = auditoria;
        _metrics = metrics;
    }

    // ── Gestión interna ───────────────────────────────────────────────────────

    public async Task<Result<PortalEnlaceDto>> GenerarEnlaceDocumentoAsync(int empresaId, int dteDocumentoId, GenerarEnlacePortalRequest request, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dteDocumentoId && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<PortalEnlaceDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        var (token, acceso) = NuevoAcceso(empresaId, PortalAccesoTipos.Documento, request, actor);
        acceso.DteDocumentoId = doc.Id;
        _db.PortalAccesos.Add(acceso);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "GENERAR_ENLACE_DOC", $"{doc.NumeroControl} (expira {acceso.ExpiraAt:yyyy-MM-dd})", acceso.Id);

        var dto = ToDto(acceso, doc.NumeroControl, null);
        dto.Token = token;
        return Result<PortalEnlaceDto>.Ok(dto);
    }

    public async Task<Result<PortalEnlaceDto>> GenerarEnlaceEstadoCuentaAsync(int empresaId, int clienteId, GenerarEnlacePortalRequest request, string? actor, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteId && c.EmpresaId == empresaId, ct);
        if (cliente is null) return Result<PortalEnlaceDto>.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");

        var (token, acceso) = NuevoAcceso(empresaId, PortalAccesoTipos.EstadoCuenta, request, actor);
        acceso.ClienteId = cliente.Id;
        _db.PortalAccesos.Add(acceso);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "GENERAR_ENLACE_CUENTA", $"{cliente.Nombre} (expira {acceso.ExpiraAt:yyyy-MM-dd})", acceso.Id);

        var dto = ToDto(acceso, null, cliente.Nombre);
        dto.Token = token;
        return Result<PortalEnlaceDto>.Ok(dto);
    }

    public async Task<Result<PagedResult<PortalEnlaceDto>>> ListEnlacesAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.PortalAccesos.AsNoTracking().Where(a => a.EmpresaId == empresaId);
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = await q.OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                Acceso = a,
                NumeroControl = a.DteDocumentoId != null
                    ? _db.DteDocumentos.Where(d => d.Id == a.DteDocumentoId).Select(d => d.NumeroControl).FirstOrDefault()
                    : null,
                ClienteNombre = a.ClienteId != null
                    ? _db.Clientes.Where(c => c.Id == a.ClienteId).Select(c => c.Nombre).FirstOrDefault()
                    : null,
            }).ToListAsync(ct);
        var items = rows.Select(r => ToDto(r.Acceso, r.NumeroControl, r.ClienteNombre)).ToList();
        return Result<PagedResult<PortalEnlaceDto>>.Ok(PagedResult<PortalEnlaceDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result> RevocarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var acceso = await _db.PortalAccesos.FirstOrDefaultAsync(a => a.Id == id && a.EmpresaId == empresaId, ct);
        if (acceso is null) return Result.Fail("Enlace no encontrado.", "PORTAL_ENLACE_NOT_FOUND");
        if (acceso.RevocadoAt is not null) return Result.Fail("El enlace ya está revocado.", "INVALID_STATE");
        acceso.RevocadoAt = DateTime.UtcNow;
        acceso.UpdatedAt = DateTime.UtcNow; acceso.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "REVOCAR_ENLACE", $"Enlace #{acceso.Id} ({acceso.Tipo})", acceso.Id);
        return Result.Ok();
    }

    // ── Resolución pública por token ──────────────────────────────────────────

    public async Task<Result<PortalDocumentoDto>> GetDocumentoAsync(string token, CancellationToken ct = default)
    {
        var acceso = await ResolverAsync(token, PortalAccesoTipos.Documento, registrarAcceso: true, ct);
        if (acceso.IsFailure) return Result<PortalDocumentoDto>.Fail(acceso.Error!, acceso.ErrorCode);
        var a = acceso.Value!;

        var doc = await _db.DteDocumentos.AsNoTracking()
            .Include(d => d.Empresa)
            .FirstOrDefaultAsync(d => d.Id == a.DteDocumentoId && d.EmpresaId == a.EmpresaId, ct);
        if (doc is null) return Result<PortalDocumentoDto>.Fail("Documento no disponible.", "DTE_NOT_FOUND");

        return Result<PortalDocumentoDto>.Ok(new PortalDocumentoDto
        {
            EmpresaNombre = doc.Empresa?.NombreComercial ?? doc.Empresa?.RazonSocial ?? "",
            TipoDteCodigo = doc.TipoDteCodigo,
            NumeroControl = doc.NumeroControl,
            CodigoGeneracion = doc.CodigoGeneracion,
            SelloRecibido = doc.SelloRecibido,
            EstadoCodigo = doc.EstadoCodigo,
            FechaEmision = doc.FechaEmision,
            ReceptorNombre = doc.ReceptorNombre,
            ReceptorCorreo = doc.ReceptorCorreo,
            TotalPagar = doc.TotalPagar,
            TotalLetras = doc.TotalLetras,
            PagoDisponible = await TienePagoDisponibleAsync(a.EmpresaId, ct),
        });
    }

    public async Task<Result<DteArchivosDto>> GetArchivosAsync(string token, CancellationToken ct = default)
    {
        var acceso = await ResolverAsync(token, PortalAccesoTipos.Documento, registrarAcceso: false, ct);
        if (acceso.IsFailure) return Result<DteArchivosDto>.Fail(acceso.Error!, acceso.ErrorCode);
        return await _dteDocs.ObtenerArchivosAsync(acceso.Value!.EmpresaId, acceso.Value.DteDocumentoId!.Value, ct);
    }

    public async Task<Result<PortalEstadoCuentaDto>> GetEstadoCuentaAsync(string token, CancellationToken ct = default)
    {
        var acceso = await ResolverAsync(token, PortalAccesoTipos.EstadoCuenta, registrarAcceso: true, ct);
        if (acceso.IsFailure) return Result<PortalEstadoCuentaDto>.Fail(acceso.Error!, acceso.ErrorCode);
        var a = acceso.Value!;

        var saldo = await _cobranza.GetSaldoClienteAsync(a.EmpresaId, a.ClienteId!.Value, ct);
        if (saldo.IsFailure) return Result<PortalEstadoCuentaDto>.Fail(saldo.Error!, saldo.ErrorCode);

        var empresa = await _db.Empresas.AsNoTracking()
            .Where(e => e.Id == a.EmpresaId)
            .Select(e => e.NombreComercial ?? e.RazonSocial)
            .FirstOrDefaultAsync(ct);

        return Result<PortalEstadoCuentaDto>.Ok(new PortalEstadoCuentaDto
        {
            EmpresaNombre = empresa ?? "",
            Saldo = saldo.Value!,
            PagoDisponible = await TienePagoDisponibleAsync(a.EmpresaId, ct),
        });
    }

    public async Task<Result<CobroQrDto>> GetQrPagoAsync(string token, int? dteDocumentoId, CancellationToken ct = default)
    {
        var acceso = await ResolverAsync(token, tipo: null, registrarAcceso: false, ct);
        if (acceso.IsFailure) return Result<CobroQrDto>.Fail(acceso.Error!, acceso.ErrorCode);
        var a = acceso.Value!;

        int docId;
        if (a.Tipo == PortalAccesoTipos.Documento)
        {
            docId = a.DteDocumentoId!.Value; // ignora el parámetro: el token manda
        }
        else
        {
            // Estado de cuenta: solo facturas del cliente del token.
            if (dteDocumentoId is not int did)
                return Result<CobroQrDto>.Fail("Indica la factura a pagar.", "VALIDATION");
            var pertenece = await _db.DteDocumentos.AsNoTracking()
                .AnyAsync(d => d.Id == did && d.EmpresaId == a.EmpresaId && d.ClienteId == a.ClienteId, ct);
            if (!pertenece) return Result<CobroQrDto>.Fail("Documento no disponible.", "DTE_NOT_FOUND");
            docId = did;
        }

        return await _cobroQr.GenerarQrAsync(a.EmpresaId, new GenerarQrCobroRequest { DteDocumentoId = docId }, ct);
    }

    public async Task<Result> ReenviarCorreoAsync(string token, string? destinatario, CancellationToken ct = default)
    {
        var acceso = await ResolverAsync(token, PortalAccesoTipos.Documento, registrarAcceso: false, ct);
        if (acceso.IsFailure) return Result.Fail(acceso.Error!, acceso.ErrorCode);
        var a = acceso.Value!;

        var r = await _dteDocs.ReenviarPorCorreoAsync(a.EmpresaId, a.DteDocumentoId!.Value, destinatario, "PORTAL", ct);
        return r.IsSuccess ? Result.Ok() : Result.Fail(r.Error ?? "No se pudo reenviar.", r.ErrorCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (string token, PortalAcceso acceso) NuevoAcceso(int empresaId, string tipo, GenerarEnlacePortalRequest request, string? actor)
    {
        var token = GenerarToken();
        return (token, new PortalAcceso
        {
            EmpresaId = empresaId,
            Tipo = tipo,
            TokenHash = HashToken(token),
            ExpiraAt = DateTime.UtcNow.AddDays(Math.Clamp(request.DiasValidez, 1, 365)),
            Nota = request.Nota?.Trim(),
            CreatedBy = actor,
        });
    }

    /// <summary>Token URL-safe de 256 bits.</summary>
    private static string GenerarToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private async Task<Result<PortalAcceso>> ResolverAsync(string token, string? tipo, bool registrarAcceso, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
            return Result<PortalAcceso>.Fail("Enlace inválido.", "TOKEN_INVALIDO");

        var hash = HashToken(token.Trim());
        var acceso = await _db.PortalAccesos.FirstOrDefaultAsync(a => a.TokenHash == hash, ct);
        if (acceso is null) return Result<PortalAcceso>.Fail("Enlace inválido.", "TOKEN_INVALIDO");
        if (tipo is not null && acceso.Tipo != tipo) return Result<PortalAcceso>.Fail("Enlace inválido.", "TOKEN_INVALIDO");
        if (acceso.RevocadoAt is not null) return Result<PortalAcceso>.Fail("Este enlace fue revocado por el emisor.", "TOKEN_REVOCADO");
        if (acceso.ExpiraAt < DateTime.UtcNow) return Result<PortalAcceso>.Fail("Este enlace expiró. Solicita uno nuevo al emisor.", "TOKEN_EXPIRADO");

        if (registrarAcceso)
        {
            acceso.Accesos++;
            acceso.UltimoAccesoAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _metrics?.PortalAcceso(acceso.EmpresaId, acceso.Tipo);
        }
        return Result<PortalAcceso>.Ok(acceso);
    }

    private Task<bool> TienePagoDisponibleAsync(int empresaId, CancellationToken ct)
        => _db.CuentasCobro.AsNoTracking().AnyAsync(c => c.EmpresaId == empresaId && c.EstadoCodigo == "ACTIVO", ct);

    private static PortalEnlaceDto ToDto(PortalAcceso a, string? numeroControl, string? clienteNombre) => new()
    {
        Id = a.Id, Tipo = a.Tipo, DteDocumentoId = a.DteDocumentoId, NumeroControl = numeroControl,
        ClienteId = a.ClienteId, ClienteNombre = clienteNombre,
        ExpiraAt = a.ExpiraAt, RevocadoAt = a.RevocadoAt, Accesos = a.Accesos, UltimoAccesoAt = a.UltimoAccesoAt,
        Nota = a.Nota, Activo = a.RevocadoAt is null && a.ExpiraAt >= DateTime.UtcNow,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "PortalAcceso", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
