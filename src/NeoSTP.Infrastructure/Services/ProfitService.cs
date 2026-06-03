using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Profit;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Implementación financiera de NeoProfit. Proyecta DTE PROCESADO, costos de producto
/// y gastos/compras hacia el <see cref="ProfitCalculator"/> (reglas puras), aislado por EmpresaId.
/// </summary>
public class ProfitService : IProfitService
{
    private const string AuditModule = "NEOPROFIT";
    private const string EstadoActivo = "ACTIVO";
    private const string EstadoInactivo = "INACTIVO";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ProfitService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // ─── Dashboard ───────────────────────────────────────────────────────────

    public async Task<ProfitDashboardDto> GetDashboardAsync(int empresaId, ProfitPeriodoQuery periodo, CancellationToken ct = default)
    {
        var (desde, hasta) = ResolvePeriodo(periodo);
        var (desdeDt, hastaExclusivo) = ToDateTimeRange(desde, hasta);

        // Cabeceras PROCESADO en el período
        var docs = await _db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId
                     && d.EstadoCodigo == DteEstadoCodigos.Procesado
                     && d.FechaEmision >= desdeDt && d.FechaEmision < hastaExclusivo)
            .Select(d => new DocRow(
                d.TipoDteCodigo, d.EstadoCodigo, d.TotalGravada, d.TotalExenta, d.TotalNoSujeto, d.IvaTotal,
                d.ClienteId, d.ReceptorNombre, d.SucursalId, d.FechaEmision))
            .ToListAsync(ct);

        var ventas = ProfitCalculator.CalcularVentas(
            docs.Select(d => new VentaDteInput(d.TipoDteCodigo, d.EstadoCodigo, d.TotalGravada, d.TotalExenta, d.TotalNoSujeto, d.IvaTotal)));

        // Líneas con costo en el período
        var lineas = await CargarLineasAsync(empresaId, desdeDt, hastaExclusivo, ct);
        var ganancia = ProfitCalculator.CalcularGanancia(
            lineas.Select(l => new CostoLineaInput(l.TipoDteCodigo, l.EstadoCodigo, l.Cantidad, l.Venta, l.Costo)));

        // Gastos y compras del período
        var gastos = await _db.ProfitGastos.AsNoTracking()
            .Where(g => g.EmpresaId == empresaId && g.EstadoCodigo == EstadoActivo && g.Fecha >= desde && g.Fecha <= hasta)
            .Select(g => new { g.Monto, g.IvaMonto, g.IvaDeducible })
            .ToListAsync(ct);
        var compras = await _db.ProfitCompras.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.EstadoCodigo == EstadoActivo && c.Fecha >= desde && c.Fecha <= hasta)
            .Select(c => new { c.Subtotal, c.IvaMonto })
            .ToListAsync(ct);

        var gastosTotal = gastos.Sum(g => g.Monto);
        var comprasTotal = compras.Sum(c => c.Subtotal + c.IvaMonto);
        var ivaCredito = gastos.Where(g => g.IvaDeducible).Sum(g => g.IvaMonto) + compras.Sum(c => c.IvaMonto);

        return new ProfitDashboardDto
        {
            Desde = desde,
            Hasta = hasta,
            VentasGravadas = ventas.VentasGravadas,
            VentasExentas = ventas.VentasExentas,
            VentasNoSujetas = ventas.VentasNoSujetas,
            VentaNeta = ventas.VentaNeta,
            Documentos = ventas.Documentos,
            CostoVentas = ganancia.CostoVentas,
            GananciaBruta = ganancia.GananciaBruta,
            MargenPorcentaje = ganancia.MargenPorcentaje,
            LineasSinCosto = ganancia.LineasSinCosto,
            GastosTotal = gastosTotal,
            UtilidadNeta = ganancia.GananciaBruta - gastosTotal,
            ComprasTotal = comprasTotal,
            IvaGenerado = ventas.IvaGenerado,
            IvaCredito = ivaCredito,
            IvaNeto = ventas.IvaGenerado - ivaCredito,
            TopProductos = RankProductos(lineas, 5),
            TopClientes = RankClientes(docs, 5),
            PorSucursal = await RankSucursalesAsync(empresaId, docs, ct),
            Tendencia = Tendencia(docs),
        };
    }

    public async Task<IReadOnlyList<ProfitProductoDto>> GetProductosAsync(int empresaId, ProfitPeriodoQuery periodo, int top = 20, CancellationToken ct = default)
    {
        var (desde, hasta) = ResolvePeriodo(periodo);
        var (desdeDt, hastaExclusivo) = ToDateTimeRange(desde, hasta);
        var lineas = await CargarLineasAsync(empresaId, desdeDt, hastaExclusivo, ct);
        return RankProductos(lineas, top);
    }

    public async Task<IReadOnlyList<ProfitClienteDto>> GetClientesAsync(int empresaId, ProfitPeriodoQuery periodo, int top = 20, CancellationToken ct = default)
    {
        var (desde, hasta) = ResolvePeriodo(periodo);
        var docs = await CargarDocsAsync(empresaId, desde, hasta, ct);
        return RankClientes(docs, top);
    }

    public async Task<IReadOnlyList<ProfitSucursalDto>> GetSucursalesAsync(int empresaId, ProfitPeriodoQuery periodo, CancellationToken ct = default)
    {
        var (desde, hasta) = ResolvePeriodo(periodo);
        var docs = await CargarDocsAsync(empresaId, desde, hasta, ct);
        return await RankSucursalesAsync(empresaId, docs, ct);
    }

    public async Task<IReadOnlyList<ProfitTendenciaPuntoDto>> GetTendenciaAsync(int empresaId, int dias = 30, CancellationToken ct = default)
    {
        dias = Math.Clamp(dias, 1, 365);
        var hasta = DateOnly.FromDateTime(DateTime.UtcNow);
        var desde = hasta.AddDays(-(dias - 1));
        var docs = await CargarDocsAsync(empresaId, desde, hasta, ct);
        return Tendencia(docs, desde, hasta);
    }

    // ─── Gastos CRUD ─────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<ProfitGastoDto>>> ListGastosAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.ProfitGastos.AsNoTracking().Where(g => g.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(g => EF.Functions.Like(g.Descripcion, $"%{s}%")
                          || EF.Functions.Like(g.Categoria, $"%{s}%")
                          || EF.Functions.Like(g.Proveedor ?? string.Empty, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(g => g.Fecha).ThenByDescending(g => g.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(g => ToGastoDto(g)).ToListAsync(ct);

        return Result<PagedResult<ProfitGastoDto>>.Ok(PagedResult<ProfitGastoDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ProfitGastoDto>> GetGastoAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var g = await _db.ProfitGastos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return g is null ? Result<ProfitGastoDto>.Fail("Gasto no encontrado.", "GASTO_NOT_FOUND") : Result<ProfitGastoDto>.Ok(ToGastoDto(g));
    }

    public async Task<Result<ProfitGastoDto>> CreateGastoAsync(int empresaId, CreateProfitGastoRequest request, string? actor, CancellationToken ct = default)
    {
        if (Validar(request) is { } err) return Result<ProfitGastoDto>.Fail(err, "VALIDATION");

        var entity = new ProfitGasto
        {
            EmpresaId = empresaId,
            Fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Categoria = string.IsNullOrWhiteSpace(request.Categoria) ? "OTROS" : request.Categoria.Trim().ToUpperInvariant(),
            Descripcion = request.Descripcion.Trim(),
            Proveedor = request.Proveedor?.Trim(),
            Monto = request.Monto,
            IvaMonto = request.IvaMonto,
            IvaDeducible = request.IvaDeducible,
            EstadoCodigo = EstadoActivo,
            CreatedBy = actor,
        };
        _db.ProfitGastos.Add(entity);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR", "OK", $"Gasto {entity.Descripcion} ({entity.Total:N2})", "ProfitGasto", entity.Id);
        return Result<ProfitGastoDto>.Ok(ToGastoDto(entity));
    }

    public async Task<Result<ProfitGastoDto>> UpdateGastoAsync(int empresaId, int id, UpdateProfitGastoRequest request, string? actor, CancellationToken ct = default)
    {
        if (Validar(request) is { } err) return Result<ProfitGastoDto>.Fail(err, "VALIDATION");

        var g = await _db.ProfitGastos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (g is null) return Result<ProfitGastoDto>.Fail("Gasto no encontrado.", "GASTO_NOT_FOUND");

        g.Fecha = request.Fecha ?? g.Fecha;
        g.Categoria = string.IsNullOrWhiteSpace(request.Categoria) ? g.Categoria : request.Categoria.Trim().ToUpperInvariant();
        g.Descripcion = request.Descripcion.Trim();
        g.Proveedor = request.Proveedor?.Trim();
        g.Monto = request.Monto;
        g.IvaMonto = request.IvaMonto;
        g.IvaDeducible = request.IvaDeducible;
        g.UpdatedAt = DateTime.UtcNow;
        g.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR", "OK", $"Gasto {g.Descripcion}", "ProfitGasto", g.Id);
        return Result<ProfitGastoDto>.Ok(ToGastoDto(g));
    }

    public async Task<Result> InactivarGastoAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var g = await _db.ProfitGastos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (g is null) return Result.Fail("Gasto no encontrado.", "GASTO_NOT_FOUND");
        if (g.EstadoCodigo == EstadoInactivo) return Result.Fail("El gasto ya está inactivo.", "INVALID_STATE");
        g.EstadoCodigo = EstadoInactivo;
        g.UpdatedAt = DateTime.UtcNow;
        g.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INACTIVAR", "OK", $"Gasto {g.Descripcion}", "ProfitGasto", g.Id);
        return Result.Ok();
    }

    // ─── Compras CRUD ────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<ProfitCompraDto>>> ListComprasAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.ProfitCompras.AsNoTracking().Where(c => c.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(c => EF.Functions.Like(c.Proveedor, $"%{s}%")
                          || EF.Functions.Like(c.NumeroDocumento ?? string.Empty, $"%{s}%")
                          || EF.Functions.Like(c.Descripcion ?? string.Empty, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => ToCompraDto(c)).ToListAsync(ct);

        return Result<PagedResult<ProfitCompraDto>>.Ok(PagedResult<ProfitCompraDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ProfitCompraDto>> GetCompraAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var c = await _db.ProfitCompras.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return c is null ? Result<ProfitCompraDto>.Fail("Compra no encontrada.", "COMPRA_NOT_FOUND") : Result<ProfitCompraDto>.Ok(ToCompraDto(c));
    }

    public async Task<Result<ProfitCompraDto>> CreateCompraAsync(int empresaId, CreateProfitCompraRequest request, string? actor, CancellationToken ct = default)
    {
        if (Validar(request) is { } err) return Result<ProfitCompraDto>.Fail(err, "VALIDATION");

        var entity = new ProfitCompra
        {
            EmpresaId = empresaId,
            Fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Proveedor = request.Proveedor.Trim(),
            NumeroDocumento = request.NumeroDocumento?.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            Subtotal = request.Subtotal,
            IvaMonto = request.IvaMonto,
            EstadoCodigo = EstadoActivo,
            CreatedBy = actor,
        };
        _db.ProfitCompras.Add(entity);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR", "OK", $"Compra {entity.Proveedor} ({entity.Total:N2})", "ProfitCompra", entity.Id);
        return Result<ProfitCompraDto>.Ok(ToCompraDto(entity));
    }

    public async Task<Result<ProfitCompraDto>> UpdateCompraAsync(int empresaId, int id, UpdateProfitCompraRequest request, string? actor, CancellationToken ct = default)
    {
        if (Validar(request) is { } err) return Result<ProfitCompraDto>.Fail(err, "VALIDATION");

        var c = await _db.ProfitCompras.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (c is null) return Result<ProfitCompraDto>.Fail("Compra no encontrada.", "COMPRA_NOT_FOUND");

        c.Fecha = request.Fecha ?? c.Fecha;
        c.Proveedor = request.Proveedor.Trim();
        c.NumeroDocumento = request.NumeroDocumento?.Trim();
        c.Descripcion = request.Descripcion?.Trim();
        c.Subtotal = request.Subtotal;
        c.IvaMonto = request.IvaMonto;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR", "OK", $"Compra {c.Proveedor}", "ProfitCompra", c.Id);
        return Result<ProfitCompraDto>.Ok(ToCompraDto(c));
    }

    public async Task<Result> InactivarCompraAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var c = await _db.ProfitCompras.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (c is null) return Result.Fail("Compra no encontrada.", "COMPRA_NOT_FOUND");
        if (c.EstadoCodigo == EstadoInactivo) return Result.Fail("La compra ya está inactiva.", "INVALID_STATE");
        c.EstadoCodigo = EstadoInactivo;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INACTIVAR", "OK", $"Compra {c.Proveedor}", "ProfitCompra", c.Id);
        return Result.Ok();
    }

    // ─── Helpers de cálculo ──────────────────────────────────────────────────

    private sealed record DocRow(
        string TipoDteCodigo, string EstadoCodigo, decimal TotalGravada, decimal TotalExenta, decimal TotalNoSujeto,
        decimal IvaTotal, int? ClienteId, string? ReceptorNombre, int? SucursalId, DateTime FechaEmision);

    private sealed record LineaRow(
        string TipoDteCodigo, string EstadoCodigo, decimal Cantidad, decimal Venta, int? ProductoId, string Nombre, decimal? Costo);

    private async Task<List<DocRow>> CargarDocsAsync(int empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var (desdeDt, hastaExclusivo) = ToDateTimeRange(desde, hasta);
        return await _db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId
                     && d.EstadoCodigo == DteEstadoCodigos.Procesado
                     && d.FechaEmision >= desdeDt && d.FechaEmision < hastaExclusivo)
            .Select(d => new DocRow(
                d.TipoDteCodigo, d.EstadoCodigo, d.TotalGravada, d.TotalExenta, d.TotalNoSujeto, d.IvaTotal,
                d.ClienteId, d.ReceptorNombre, d.SucursalId, d.FechaEmision))
            .ToListAsync(ct);
    }

    private async Task<List<LineaRow>> CargarLineasAsync(int empresaId, DateTime desdeDt, DateTime hastaExclusivo, CancellationToken ct)
        => await _db.Set<Domain.Core.Dte.DteDocumentoDetalle>().AsNoTracking()
            .Where(x => x.Documento.EmpresaId == empresaId
                     && x.Documento.EstadoCodigo == DteEstadoCodigos.Procesado
                     && x.Documento.FechaEmision >= desdeDt && x.Documento.FechaEmision < hastaExclusivo)
            .Select(x => new LineaRow(
                x.Documento.TipoDteCodigo, x.Documento.EstadoCodigo, x.Cantidad,
                x.VentaGravada + x.VentaExenta + x.VentaNoSujeta,
                x.ProductoId,
                x.Producto != null ? x.Producto.Nombre : x.Descripcion,
                x.Producto != null ? x.Producto.CostoUnitario : null))
            .ToListAsync(ct);

    private static List<ProfitProductoDto> RankProductos(List<LineaRow> lineas, int top)
        => lineas
            .GroupBy(l => l.ProductoId)
            .Select(g =>
            {
                var gan = ProfitCalculator.CalcularGanancia(
                    g.Select(l => new CostoLineaInput(l.TipoDteCodigo, l.EstadoCodigo, l.Cantidad, l.Venta, l.Costo)));
                var cantidad = g.Where(l => ProfitCalculator.EsComputable(l.EstadoCodigo))
                                .Sum(l => ProfitCalculator.Signo(l.TipoDteCodigo) * l.Cantidad);
                var venta = g.Where(l => ProfitCalculator.EsComputable(l.EstadoCodigo))
                             .Sum(l => ProfitCalculator.Signo(l.TipoDteCodigo) * l.Venta);
                return new ProfitProductoDto
                {
                    ProductoId = g.Key,
                    Nombre = g.First().Nombre,
                    Cantidad = cantidad,
                    Venta = venta,
                    Costo = gan.CostoVentas,
                    Ganancia = gan.GananciaBruta,
                    MargenPorcentaje = gan.MargenPorcentaje,
                    CostoPendiente = gan.LineasSinCosto > 0,
                };
            })
            .OrderByDescending(p => p.Venta)
            .Take(Math.Clamp(top, 1, 100))
            .ToList();

    private static List<ProfitClienteDto> RankClientes(List<DocRow> docs, int top)
        => docs
            .GroupBy(d => d.ClienteId)
            .Select(g => new ProfitClienteDto
            {
                ClienteId = g.Key,
                Nombre = g.Select(x => x.ReceptorNombre).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "Consumidor final",
                Documentos = g.Count(),
                Venta = g.Sum(x => ProfitCalculator.Signo(x.TipoDteCodigo) * (x.TotalGravada + x.TotalExenta + x.TotalNoSujeto)),
            })
            .OrderByDescending(c => c.Venta)
            .Take(Math.Clamp(top, 1, 100))
            .ToList();

    private async Task<List<ProfitSucursalDto>> RankSucursalesAsync(int empresaId, List<DocRow> docs, CancellationToken ct)
    {
        var nombres = await _db.Sucursales.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId)
            .ToDictionaryAsync(s => s.Id, s => s.Nombre, ct);

        return docs
            .GroupBy(d => d.SucursalId)
            .Select(g => new ProfitSucursalDto
            {
                SucursalId = g.Key,
                Nombre = g.Key is int sid && nombres.TryGetValue(sid, out var n) ? n : "Sin sucursal",
                Documentos = g.Count(),
                Venta = g.Sum(x => ProfitCalculator.Signo(x.TipoDteCodigo) * (x.TotalGravada + x.TotalExenta + x.TotalNoSujeto)),
            })
            .OrderByDescending(s => s.Venta)
            .ToList();
    }

    private static List<ProfitTendenciaPuntoDto> Tendencia(List<DocRow> docs, DateOnly? desde = null, DateOnly? hasta = null)
    {
        var porDia = docs
            .GroupBy(d => DateOnly.FromDateTime(d.FechaEmision))
            .ToDictionary(
                g => g.Key,
                g => (Venta: g.Sum(x => ProfitCalculator.Signo(x.TipoDteCodigo) * (x.TotalGravada + x.TotalExenta + x.TotalNoSujeto)), Docs: g.Count()));

        if (desde is null || hasta is null)
        {
            return porDia.OrderBy(k => k.Key)
                .Select(k => new ProfitTendenciaPuntoDto { Fecha = k.Key, Venta = k.Value.Venta, Documentos = k.Value.Docs })
                .ToList();
        }

        // Rellenar días sin ventas con 0
        var puntos = new List<ProfitTendenciaPuntoDto>();
        for (var d = desde.Value; d <= hasta.Value; d = d.AddDays(1))
        {
            porDia.TryGetValue(d, out var v);
            puntos.Add(new ProfitTendenciaPuntoDto { Fecha = d, Venta = v.Venta, Documentos = v.Docs });
        }
        return puntos;
    }

    // ─── Helpers varios ──────────────────────────────────────────────────────

    private static (DateOnly Desde, DateOnly Hasta) ResolvePeriodo(ProfitPeriodoQuery p)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var desde = p.Desde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = p.Hasta ?? hoy;
        if (hasta < desde) (desde, hasta) = (hasta, desde);
        return (desde, hasta);
    }

    private static (DateTime DesdeDt, DateTime HastaExclusivo) ToDateTimeRange(DateOnly desde, DateOnly hasta)
        => (desde.ToDateTime(TimeOnly.MinValue), hasta.AddDays(1).ToDateTime(TimeOnly.MinValue));

    private static string? Validar(CreateProfitGastoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Descripcion)) return "La descripción es obligatoria.";
        if (r.Monto < 0 || r.IvaMonto < 0) return "Los montos no pueden ser negativos.";
        return null;
    }

    private static string? Validar(CreateProfitCompraRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Proveedor)) return "El proveedor es obligatorio.";
        if (r.Subtotal < 0 || r.IvaMonto < 0) return "Los montos no pueden ser negativos.";
        return null;
    }

    private static ProfitGastoDto ToGastoDto(ProfitGasto g) => new()
    {
        Id = g.Id, Fecha = g.Fecha, Categoria = g.Categoria, Descripcion = g.Descripcion, Proveedor = g.Proveedor,
        Monto = g.Monto, IvaMonto = g.IvaMonto, IvaDeducible = g.IvaDeducible, Total = g.Monto + g.IvaMonto,
        EstadoCodigo = g.EstadoCodigo,
    };

    private static ProfitCompraDto ToCompraDto(ProfitCompra c) => new()
    {
        Id = c.Id, Fecha = c.Fecha, Proveedor = c.Proveedor, NumeroDocumento = c.NumeroDocumento, Descripcion = c.Descripcion,
        Subtotal = c.Subtotal, IvaMonto = c.IvaMonto, Total = c.Subtotal + c.IvaMonto, EstadoCodigo = c.EstadoCodigo,
    };

    private Task Audit(int empresaId, string? actor, string accion, string resultado, string detalle, string entidad, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = entidad, EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });
}
