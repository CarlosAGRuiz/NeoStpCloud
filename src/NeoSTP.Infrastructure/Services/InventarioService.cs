using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Inventario;
using NeoSTP.Application.Inventario.Dtos;
using NeoSTP.Domain.Core.Inventario;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// INVENTARIO (Fase B). Existencias + kardex con costeo por promedio ponderado
/// (<see cref="CostoPromedioCalculator"/>). Actualiza el costo del producto para alimentar
/// NeoProfit. Aislado por EmpresaId.
/// </summary>
public class InventarioService : IInventarioService
{
    private const string AuditModule = "INVENTARIO";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public InventarioService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<ExistenciaDto>>> ListExistenciasAsync(int empresaId, bool soloStockBajo, PagedQuery query, CancellationToken ct = default)
    {
        // Parte de los productos (para mostrar también los que aún no tienen existencia).
        var q = from p in _db.Productos.AsNoTracking().Where(p => p.EmpresaId == empresaId)
                join e in _db.ExistenciasProducto.AsNoTracking() on new { p.EmpresaId, ProductoId = p.Id } equals new { e.EmpresaId, e.ProductoId } into ex
                from e in ex.DefaultIfEmpty()
                select new { p.Id, p.CodigoInterno, p.Nombre, p.EstadoCodigo, e };

        q = q.Where(x => x.EstadoCodigo == "ACTIVO");
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.CodigoInterno.Contains(s) || x.Nombre.Contains(s));
        }
        if (soloStockBajo)
            q = q.Where(x => x.e != null && x.e.StockMinimo > 0 && x.e.Cantidad <= x.e.StockMinimo);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = await q.OrderBy(x => x.Nombre)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(x => ToDto(x.Id, x.CodigoInterno, x.Nombre,
            x.e?.Cantidad ?? 0m, x.e?.CostoPromedio ?? 0m, x.e?.StockMinimo ?? 0m)).ToList();
        return Result<PagedResult<ExistenciaDto>>.Ok(PagedResult<ExistenciaDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ExistenciaDto>> GetExistenciaAsync(int empresaId, int productoId, CancellationToken ct = default)
    {
        var p = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productoId && x.EmpresaId == empresaId, ct);
        if (p is null) return Result<ExistenciaDto>.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND");
        var e = await _db.ExistenciasProducto.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ProductoId == productoId, ct);
        return Result<ExistenciaDto>.Ok(ToDto(p.Id, p.CodigoInterno, p.Nombre, e?.Cantidad ?? 0m, e?.CostoPromedio ?? 0m, e?.StockMinimo ?? 0m));
    }

    public async Task<Result<PagedResult<MovimientoInventarioDto>>> GetKardexAsync(int empresaId, int productoId, PagedQuery query, CancellationToken ct = default)
    {
        var existe = await _db.Productos.AnyAsync(p => p.Id == productoId && p.EmpresaId == empresaId, ct);
        if (!existe) return Result<PagedResult<MovimientoInventarioDto>>.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND");

        var q = _db.MovimientosInventario.AsNoTracking().Where(m => m.EmpresaId == empresaId && m.ProductoId == productoId);
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => ToMovDto(m)).ToListAsync(ct);
        return Result<PagedResult<MovimientoInventarioDto>>.Ok(PagedResult<MovimientoInventarioDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ExistenciaDto>> RegistrarEntradaAsync(int empresaId, RegistrarMovimientoInventarioRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Cantidad <= 0) return Result<ExistenciaDto>.Fail("La cantidad debe ser mayor que cero.", "VALIDATION");
        var (prod, exist, err) = await CargarAsync(empresaId, request.ProductoId, ct);
        if (err is not null) return err;

        var costo = request.CostoUnitario ?? (prod!.CostoUnitario ?? exist!.CostoPromedio);
        var saldo = CostoPromedioCalculator.Entrada(new(exist!.Cantidad, exist.CostoPromedio), request.Cantidad, costo);
        await AplicarAsync(empresaId, prod!, exist, saldo, TiposMovimientoInventario.Entrada, request.Cantidad, costo,
            NormalizarOrigen(request.Origen), request.OrigenId, request.Fecha, request.Referencia, request.Nota, actor, ct);
        return Result<ExistenciaDto>.Ok(ToDto(prod!.Id, prod.CodigoInterno, prod.Nombre, exist.Cantidad, exist.CostoPromedio, exist.StockMinimo));
    }

    public async Task<Result<ExistenciaDto>> RegistrarSalidaAsync(int empresaId, RegistrarMovimientoInventarioRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Cantidad <= 0) return Result<ExistenciaDto>.Fail("La cantidad debe ser mayor que cero.", "VALIDATION");
        var (prod, exist, err) = await CargarAsync(empresaId, request.ProductoId, ct);
        if (err is not null) return err;
        if (request.Cantidad > exist!.Cantidad)
            return Result<ExistenciaDto>.Fail($"Stock insuficiente (disponible {exist.Cantidad:N2}).", "STOCK_INSUFICIENTE");

        var saldo = CostoPromedioCalculator.Salida(new(exist.Cantidad, exist.CostoPromedio), request.Cantidad);
        await AplicarAsync(empresaId, prod!, exist, saldo, TiposMovimientoInventario.Salida, request.Cantidad, exist.CostoPromedio,
            NormalizarOrigen(request.Origen), request.OrigenId, request.Fecha, request.Referencia, request.Nota, actor, ct, actualizarCostoProducto: false);
        return Result<ExistenciaDto>.Ok(ToDto(prod!.Id, prod.CodigoInterno, prod.Nombre, exist.Cantidad, exist.CostoPromedio, exist.StockMinimo));
    }

    public async Task<Result<ExistenciaDto>> AjustarAsync(int empresaId, AjusteStockRequest request, string? actor, CancellationToken ct = default)
    {
        var (prod, exist, err) = await CargarAsync(empresaId, request.ProductoId, ct);
        if (err is not null) return err;

        var saldo = CostoPromedioCalculator.Ajuste(new(exist!.Cantidad, exist.CostoPromedio), request.CantidadAbsoluta, request.CostoUnitario);
        await AplicarAsync(empresaId, prod!, exist, saldo, TiposMovimientoInventario.Ajuste, request.CantidadAbsoluta, saldo.CostoPromedio,
            OrigenesMovimientoInventario.Ajuste, null, null, null, request.Nota, actor, ct);
        return Result<ExistenciaDto>.Ok(ToDto(prod!.Id, prod.CodigoInterno, prod.Nombre, exist.Cantidad, exist.CostoPromedio, exist.StockMinimo));
    }

    public async Task<Result<ExistenciaDto>> SetStockMinimoAsync(int empresaId, SetStockMinimoRequest request, string? actor, CancellationToken ct = default)
    {
        var (prod, exist, err) = await CargarAsync(empresaId, request.ProductoId, ct);
        if (err is not null) return err;
        exist!.StockMinimo = request.StockMinimo < 0 ? 0 : request.StockMinimo;
        exist.UpdatedAt = DateTime.UtcNow; exist.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "STOCK_MINIMO", $"{prod!.CodigoInterno} = {exist.StockMinimo:N2}", prod.Id);
        return Result<ExistenciaDto>.Ok(ToDto(prod.Id, prod.CodigoInterno, prod.Nombre, exist.Cantidad, exist.CostoPromedio, exist.StockMinimo));
    }

    public async Task<Result<InventarioResumenDto>> ResumenAsync(int empresaId, CancellationToken ct = default)
    {
        var ex = await _db.ExistenciasProducto.AsNoTracking()
            .Where(e => e.EmpresaId == empresaId)
            .Select(e => new { e.Cantidad, e.CostoPromedio, e.StockMinimo }).ToListAsync(ct);
        return Result<InventarioResumenDto>.Ok(new InventarioResumenDto
        {
            ValorTotal = ex.Sum(e => CostoPromedioCalculator.ValorInventario(e.Cantidad, e.CostoPromedio)),
            Productos = ex.Count,
            ProductosBajoStock = ex.Count(e => e.StockMinimo > 0 && e.Cantidad <= e.StockMinimo),
            ProductosSinStock = ex.Count(e => e.Cantidad <= 0),
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(Domain.Core.Productos.Producto? prod, ExistenciaProducto? exist, Result<ExistenciaDto>? err)> CargarAsync(int empresaId, int productoId, CancellationToken ct)
    {
        var prod = await _db.Productos.FirstOrDefaultAsync(p => p.Id == productoId && p.EmpresaId == empresaId, ct);
        if (prod is null) return (null, null, Result<ExistenciaDto>.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND"));
        var exist = await _db.ExistenciasProducto.FirstOrDefaultAsync(e => e.EmpresaId == empresaId && e.ProductoId == productoId, ct);
        if (exist is null)
        {
            exist = new ExistenciaProducto { EmpresaId = empresaId, ProductoId = productoId, Cantidad = 0m, CostoPromedio = prod.CostoUnitario ?? 0m };
            _db.ExistenciasProducto.Add(exist);
        }
        return (prod, exist, null);
    }

    private async Task AplicarAsync(int empresaId, Domain.Core.Productos.Producto prod, ExistenciaProducto exist,
        CostoPromedioCalculator.Saldo saldo, string tipo, decimal cantidad, decimal costoMovimiento,
        string origen, int? origenId, DateOnly? fecha, string? referencia, string? nota, string? actor,
        CancellationToken ct, bool actualizarCostoProducto = true)
    {
        exist.Cantidad = saldo.Cantidad;
        exist.CostoPromedio = saldo.CostoPromedio;
        exist.UpdatedAt = DateTime.UtcNow; exist.UpdatedBy = actor;

        if (actualizarCostoProducto && saldo.CostoPromedio > 0)
        {
            prod.CostoUnitario = Math.Round(saldo.CostoPromedio, 2, MidpointRounding.AwayFromZero); // mejora el costo en NeoProfit
            prod.UpdatedAt = DateTime.UtcNow; prod.UpdatedBy = actor;
        }

        _db.MovimientosInventario.Add(new MovimientoInventario
        {
            EmpresaId = empresaId, ProductoId = prod.Id, Fecha = fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Tipo = tipo, Cantidad = cantidad, CostoUnitario = costoMovimiento, Origen = origen, OrigenId = origenId,
            Referencia = referencia?.Trim(), Nota = nota?.Trim(),
            SaldoCantidad = saldo.Cantidad, SaldoCostoPromedio = saldo.CostoPromedio, CreatedBy = actor,
        });
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, tipo, $"{prod.CodigoInterno} {cantidad:N2} → saldo {saldo.Cantidad:N2}", prod.Id);
    }

    private static string NormalizarOrigen(string origen)
        => OrigenesMovimientoInventario.All.Contains(origen) ? origen : OrigenesMovimientoInventario.Ajuste;

    private static ExistenciaDto ToDto(int productoId, string codigo, string nombre, decimal cantidad, decimal costo, decimal stockMin) => new()
    {
        ProductoId = productoId, Codigo = codigo, Nombre = nombre, Cantidad = cantidad, CostoPromedio = costo,
        Valor = CostoPromedioCalculator.ValorInventario(cantidad, costo),
        StockMinimo = stockMin, StockBajo = stockMin > 0 && cantidad <= stockMin,
    };

    private static MovimientoInventarioDto ToMovDto(MovimientoInventario m) => new()
    {
        Id = m.Id, ProductoId = m.ProductoId, Fecha = m.Fecha, Tipo = m.Tipo, Cantidad = m.Cantidad,
        CostoUnitario = m.CostoUnitario, Origen = m.Origen, OrigenId = m.OrigenId, Referencia = m.Referencia, Nota = m.Nota,
        SaldoCantidad = m.SaldoCantidad, SaldoCostoPromedio = m.SaldoCostoPromedio,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "Inventario", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
