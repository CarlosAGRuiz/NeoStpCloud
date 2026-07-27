using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Cuentas por cobrar: deriva saldos de DTE (factura/CCF a crédito) PROCESADO menos pagos
/// CONFIRMADOS, usando <see cref="CobranzaCalculator"/>. Registra pagos. Aislado por EmpresaId.
/// </summary>
public class CobranzaService : ICobranzaService
{
    private const string AuditModule = "COBRANZA";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IConnectWebhookDispatcher? _webhooks;

    public CobranzaService(
        NeoStpDbContext db,
        IAuditoriaService auditoria,
        IConnectWebhookDispatcher? webhooks = null)
    {
        _db = db;
        _auditoria = auditoria;
        _webhooks = webhooks;
    }

    private sealed record Row(int DteId, string Tipo, string NumeroControl, DateTime FechaEmision,
        int? PlazoDias, int? ClienteId, string? ClienteNombre, decimal Total, decimal Pagado);

    private async Task<List<CobroPendienteDto>> CargarPendientesAsync(int empresaId, int? clienteId, string? search, CancellationToken ct)
    {
        var q = _db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId
                     && d.EstadoCodigo == DteEstadoCodigos.Procesado
                     && (d.TipoDteCodigo == TipoDteCodigos.FacturaConsumidorFinal || d.TipoDteCodigo == TipoDteCodigos.ComprobanteCreditoFiscal)
                     && (d.CondicionOperacionCodigo == "2" || d.CondicionOperacionCodigo == "3"));

        if (clienteId is int cid) q = q.Where(d => d.ClienteId == cid);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(d => EF.Functions.Like(d.NumeroControl, $"%{s}%")
                          || EF.Functions.Like(d.ReceptorNombre ?? string.Empty, $"%{s}%"));
        }

        var rows = await q.Select(d => new Row(
                d.Id, d.TipoDteCodigo, d.NumeroControl, d.FechaEmision, d.PlazoDias,
                d.ClienteId, d.ReceptorNombre, d.TotalPagar,
                _db.Set<PagoCliente>().Where(p => p.DteDocumentoId == d.Id && p.EstadoCodigo == PagoEstados.Confirmado)
                    .Sum(p => (decimal?)p.Monto) ?? 0m))
            .ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var lista = new List<CobroPendienteDto>(rows.Count);
        foreach (var r in rows)
        {
            var saldo = CobranzaCalculator.Saldo(r.Total, r.Pagado);
            if (saldo <= 0) continue;
            var fechaEmision = DateOnly.FromDateTime(r.FechaEmision);
            var venc = CobranzaCalculator.Vencimiento(fechaEmision, r.PlazoDias);
            lista.Add(new CobroPendienteDto
            {
                DteDocumentoId = r.DteId,
                TipoDteCodigo = r.Tipo,
                NumeroControl = r.NumeroControl,
                FechaEmision = fechaEmision,
                Vencimiento = venc,
                ClienteId = r.ClienteId,
                ClienteNombre = string.IsNullOrWhiteSpace(r.ClienteNombre) ? "Consumidor final" : r.ClienteNombre!,
                Total = r.Total,
                Pagado = r.Pagado,
                Saldo = saldo,
                EstadoCobro = CobranzaCalculator.EstadoCobro(saldo, venc, hoy),
                DiasVencido = CobranzaCalculator.DiasVencido(venc, hoy),
            });
        }
        // Vencidas primero, luego por vencimiento más antiguo
        return lista
            .OrderByDescending(x => x.EstadoCobro == CobroEstados.Vencido)
            .ThenBy(x => x.Vencimiento)
            .ToList();
    }

    public async Task<CobranzaResumenDto> GetResumenAsync(int empresaId, CancellationToken ct = default)
    {
        var pend = await CargarPendientesAsync(empresaId, null, null, ct);
        var vencidas = pend.Where(p => p.EstadoCobro == CobroEstados.Vencido).ToList();
        return new CobranzaResumenDto
        {
            TotalPendiente = pend.Sum(p => p.Saldo),
            TotalVencido = vencidas.Sum(p => p.Saldo),
            FacturasPendientes = pend.Count,
            FacturasVencidas = vencidas.Count,
            ClientesConDeuda = pend.Select(p => p.ClienteId ?? 0).Distinct().Count(),
        };
    }

    public async Task<Result<PagedResult<CobroPendienteDto>>> GetPendientesAsync(int empresaId, CobranzaQuery query, CancellationToken ct = default)
    {
        var lista = await CargarPendientesAsync(empresaId, query.ClienteId, query.Search, ct);
        if (query.SoloVencidas)
            lista = lista.Where(p => p.EstadoCobro == CobroEstados.Vencido).ToList();

        var total = lista.Count;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = lista.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Result<PagedResult<CobroPendienteDto>>.Ok(PagedResult<CobroPendienteDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<SaldoClienteDto>> GetSaldoClienteAsync(int empresaId, int clienteId, CancellationToken ct = default)
    {
        var facturas = await CargarPendientesAsync(empresaId, clienteId, null, ct);
        var nombre = facturas.Select(f => f.ClienteNombre).FirstOrDefault()
            ?? (await _db.Clientes.AsNoTracking().Where(c => c.Id == clienteId && c.EmpresaId == empresaId)
                    .Select(c => c.Nombre).FirstOrDefaultAsync(ct))
            ?? "Cliente";

        return Result<SaldoClienteDto>.Ok(new SaldoClienteDto
        {
            ClienteId = clienteId,
            ClienteNombre = nombre,
            TotalPendiente = facturas.Sum(f => f.Saldo),
            TotalVencido = facturas.Where(f => f.EstadoCobro == CobroEstados.Vencido).Sum(f => f.Saldo),
            Facturas = facturas,
        });
    }

    public async Task<Result<IReadOnlyList<PagoClienteDto>>> GetPagosAsync(int empresaId, int dteDocumentoId, CancellationToken ct = default)
    {
        var pagos = await _db.Set<PagoCliente>().AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.DteDocumentoId == dteDocumentoId)
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id)
            .Select(p => ToDto(p))
            .ToListAsync(ct);
        return Result<IReadOnlyList<PagoClienteDto>>.Ok(pagos);
    }

    public async Task<Result<PagoClienteDto>> RegistrarPagoAsync(int empresaId, int dteDocumentoId, RegistrarPagoRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Monto <= 0)
            return Result<PagoClienteDto>.Fail("El monto debe ser mayor a cero.", "VALIDATION");

        var dte = await _db.DteDocumentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dteDocumentoId && d.EmpresaId == empresaId, ct);
        if (dte is null) return Result<PagoClienteDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (dte.EstadoCodigo != DteEstadoCodigos.Procesado || !CobranzaCalculator.EsCobrable(dte.TipoDteCodigo) || !CobranzaCalculator.EsCredito(dte.CondicionOperacionCodigo))
            return Result<PagoClienteDto>.Fail("El documento no admite registro de cobros (debe ser factura/CCF a crédito y procesado).", "INVALID_STATE");

        var pagado = await PagadoConfirmadoAsync(dteDocumentoId, ct);
        var saldo = CobranzaCalculator.Saldo(dte.TotalPagar, pagado);
        if (request.Monto > saldo)
            return Result<PagoClienteDto>.Fail($"El monto ({request.Monto:N2}) excede el saldo pendiente ({saldo:N2}).", "VALIDATION");

        var entity = new PagoCliente
        {
            EmpresaId = empresaId,
            DteDocumentoId = dteDocumentoId,
            Fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Monto = request.Monto,
            FormaPagoCodigo = string.IsNullOrWhiteSpace(request.FormaPagoCodigo) ? "EFECTIVO" : request.FormaPagoCodigo.Trim().ToUpperInvariant(),
            Referencia = request.Referencia?.Trim(),
            Nota = request.Nota?.Trim(),
            ComprobanteUrl = request.ComprobanteUrl?.Trim(),
            EstadoCodigo = request.PendienteRevision ? PagoEstados.PendienteRevision : PagoEstados.Confirmado,
            CreatedBy = actor,
        };
        _db.Set<PagoCliente>().Add(entity);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "REGISTRAR", $"Pago {entity.Monto:N2} sobre DTE {dte.NumeroControl} ({entity.EstadoCodigo})", entity.Id);

        // E6: solo cuando el dinero cuenta de verdad; un pago en revisión aún no es cobro.
        if (entity.EstadoCodigo == PagoEstados.Confirmado)
            await NotificarPagoConfirmadoAsync(empresaId, entity, dte.NumeroControl, dte.TotalPagar, pagado + entity.Monto, ct);

        return Result<PagoClienteDto>.Ok(ToDto(entity));
    }

    public async Task<Result> AnularPagoAsync(int empresaId, int pagoId, string? actor, CancellationToken ct = default)
    {
        var p = await _db.Set<PagoCliente>().FirstOrDefaultAsync(x => x.Id == pagoId && x.EmpresaId == empresaId, ct);
        if (p is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");
        if (p.EstadoCodigo == PagoEstados.Anulado) return Result.Fail("El pago ya está anulado.", "INVALID_STATE");
        p.EstadoCodigo = PagoEstados.Anulado;
        p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ANULAR", $"Pago #{p.Id} anulado", p.Id);
        return Result.Ok();
    }

    public async Task<Result> ConfirmarPagoAsync(int empresaId, int pagoId, string? actor, CancellationToken ct = default)
    {
        var p = await _db.Set<PagoCliente>().FirstOrDefaultAsync(x => x.Id == pagoId && x.EmpresaId == empresaId, ct);
        if (p is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");
        if (p.EstadoCodigo != PagoEstados.PendienteRevision) return Result.Fail("Solo se confirman pagos en revisión.", "INVALID_STATE");

        var dte = await _db.DteDocumentos.AsNoTracking().FirstAsync(d => d.Id == p.DteDocumentoId, ct);
        var pagado = await PagadoConfirmadoAsync(p.DteDocumentoId, ct);
        if (p.Monto > CobranzaCalculator.Saldo(dte.TotalPagar, pagado))
            return Result.Fail("El pago excede el saldo pendiente actual.", "VALIDATION");

        p.EstadoCodigo = PagoEstados.Confirmado;
        p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CONFIRMAR", $"Pago #{p.Id} confirmado", p.Id);
        await NotificarPagoConfirmadoAsync(empresaId, p, dte.NumeroControl, dte.TotalPagar, pagado + p.Monto, ct);
        return Result.Ok();
    }

    /// <summary>
    /// Webhook de negocio (E6): el integrador quiere enterarse de que le pagaron para
    /// cerrar el pedido en su sistema. Best-effort: no afecta el registro del pago.
    /// </summary>
    private async Task NotificarPagoConfirmadoAsync(
        int empresaId, PagoCliente pago, string numeroControl,
        decimal totalDte, decimal pagadoAcumulado, CancellationToken ct)
    {
        if (_webhooks is null) return;

        var saldo = CobranzaCalculator.Saldo(totalDte, pagadoAcumulado);
        await _webhooks.DispatchNegocioAsync(new ConnectEventoNegocioPayload
        {
            Evento = ConnectEventos.CobroPagoConfirmado,
            EmpresaId = empresaId,
            EntidadTipo = "PagoCliente",
            EntidadId = pago.Id,
            Descripcion = $"Pago de $ {pago.Monto:N2} confirmado sobre {numeroControl}.",
            Datos = new Dictionary<string, object?>
            {
                ["dteDocumentoId"] = pago.DteDocumentoId,
                ["numeroControl"] = numeroControl,
                ["monto"] = pago.Monto,
                ["formaPago"] = pago.FormaPagoCodigo,
                ["referencia"] = pago.Referencia,
                ["saldoRestante"] = saldo,
                ["saldado"] = saldo <= 0,
            },
        }, ct);
    }

    private async Task<decimal> PagadoConfirmadoAsync(int dteId, CancellationToken ct)
        => await _db.Set<PagoCliente>().AsNoTracking()
            .Where(p => p.DteDocumentoId == dteId && p.EstadoCodigo == PagoEstados.Confirmado)
            .SumAsync(p => (decimal?)p.Monto, ct) ?? 0m;

    private static PagoClienteDto ToDto(PagoCliente p) => new()
    {
        Id = p.Id, DteDocumentoId = p.DteDocumentoId, Fecha = p.Fecha, Monto = p.Monto,
        FormaPagoCodigo = p.FormaPagoCodigo, Referencia = p.Referencia, Nota = p.Nota,
        ComprobanteUrl = p.ComprobanteUrl, EstadoCodigo = p.EstadoCodigo, CreatedAt = p.CreatedAt,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "PagoCliente", EntidadId = entidadId.ToString(),
            Resultado = "OK", Detalle = detalle,
        });
}
