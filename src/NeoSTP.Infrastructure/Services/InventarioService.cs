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

    public async Task<Result<PagedResult<ExistenciaDto>>> ListExistenciasAsync(int empresaId, bool soloStockBajo, PagedQuery query, int? sucursalId = null, CancellationToken ct = default)
    {
        // Productos activos (para mostrar también los que aún no tienen existencia).
        var qp = _db.Productos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.EstadoCodigo == "ACTIVO");
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            qp = qp.Where(p => p.CodigoInterno.Contains(s) || p.Nombre.Contains(s));
        }
        var productos = await qp.OrderBy(p => p.Nombre)
            .Select(p => new { p.Id, p.CodigoInterno, p.Nombre })
            .ToListAsync(ct);

        var ids = productos.Select(p => p.Id).ToList();
        var qe = _db.ExistenciasProducto.AsNoTracking()
            .Where(e => e.EmpresaId == empresaId && ids.Contains(e.ProductoId));
        if (sucursalId is not null) qe = qe.Where(e => e.SucursalId == sucursalId);
        var existencias = (await qe.ToListAsync(ct)).ToLookup(e => e.ProductoId);

        // Con filtro de sucursal se muestra el saldo de esa sucursal; sin filtro, el
        // consolidado por producto (suma de sucursales + central, costo ponderado).
        var items = productos.Select(p =>
        {
            var rows = existencias[p.Id].ToList();
            var cantidad = rows.Sum(e => e.Cantidad);
            var costo = cantidad > 0
                ? Math.Round(rows.Sum(e => e.Cantidad * e.CostoPromedio) / cantidad, 4, MidpointRounding.AwayFromZero)
                : rows.FirstOrDefault()?.CostoPromedio ?? 0m;
            var stockMin = rows.Sum(e => e.StockMinimo);
            var dto = ToDto(p.Id, p.CodigoInterno, p.Nombre, cantidad, costo, stockMin);
            dto.SucursalId = sucursalId;
            dto.StockBajo = rows.Any(e => e.StockMinimo > 0 && e.Cantidad <= e.StockMinimo);
            return dto;
        }).ToList();

        if (soloStockBajo) items = items.Where(i => i.StockBajo).ToList();

        var total = items.Count;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Result<PagedResult<ExistenciaDto>>.Ok(PagedResult<ExistenciaDto>.Create(pageItems, total, page, pageSize));
    }

    public async Task<Result<ExistenciaDto>> GetExistenciaAsync(int empresaId, int productoId, int? sucursalId = null, CancellationToken ct = default)
    {
        var p = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productoId && x.EmpresaId == empresaId, ct);
        if (p is null) return Result<ExistenciaDto>.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND");

        var qe = _db.ExistenciasProducto.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.ProductoId == productoId);
        if (sucursalId is not null) qe = qe.Where(x => x.SucursalId == sucursalId);
        var rows = await qe.ToListAsync(ct);

        var cantidad = rows.Sum(e => e.Cantidad);
        var costo = cantidad > 0
            ? Math.Round(rows.Sum(e => e.Cantidad * e.CostoPromedio) / cantidad, 4, MidpointRounding.AwayFromZero)
            : rows.FirstOrDefault()?.CostoPromedio ?? 0m;
        var dto = ToDto(p.Id, p.CodigoInterno, p.Nombre, cantidad, costo, rows.Sum(e => e.StockMinimo));
        dto.SucursalId = sucursalId;
        dto.StockBajo = rows.Any(e => e.StockMinimo > 0 && e.Cantidad <= e.StockMinimo);
        return Result<ExistenciaDto>.Ok(dto);
    }

    public async Task<Result<PagedResult<MovimientoInventarioDto>>> GetKardexAsync(int empresaId, int productoId, PagedQuery query, int? sucursalId = null, CancellationToken ct = default)
    {
        var existe = await _db.Productos.AnyAsync(p => p.Id == productoId && p.EmpresaId == empresaId, ct);
        if (!existe) return Result<PagedResult<MovimientoInventarioDto>>.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND");

        var q = _db.MovimientosInventario.AsNoTracking().Where(m => m.EmpresaId == empresaId && m.ProductoId == productoId);
        if (sucursalId is not null) q = q.Where(m => m.SucursalId == sucursalId);
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
        var (prod, exist, err) = await CargarAsync(empresaId, request.ProductoId, request.SucursalId, ct);
        if (err is not null) return err;

        string? numeroLote = null;
        if (prod!.ControlaLote)
        {
            numeroLote = request.NumeroLote?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(numeroLote))
                return Result<ExistenciaDto>.Fail(
                    $"El producto {prod.CodigoInterno} controla lote: indica el número de lote de la entrada.", "LOTE_REQUERIDO");

            var lote = await _db.LotesProducto.FirstOrDefaultAsync(
                l => l.EmpresaId == empresaId && l.ProductoId == prod.Id
                  && l.SucursalId == request.SucursalId && l.NumeroLote == numeroLote, ct);
            if (lote is null)
            {
                lote = new LoteProducto
                {
                    EmpresaId = empresaId, ProductoId = prod.Id, SucursalId = request.SucursalId,
                    NumeroLote = numeroLote,
                    FechaVencimiento = request.FechaVencimiento, Cantidad = 0m,
                    CreatedAt = DateTime.UtcNow, CreatedBy = actor,
                };
                _db.LotesProducto.Add(lote);
            }
            else if (request.FechaVencimiento is not null)
            {
                lote.FechaVencimiento = request.FechaVencimiento;
            }
            lote.Cantidad += request.Cantidad;
            lote.UpdatedAt = DateTime.UtcNow; lote.UpdatedBy = actor;
        }

        var costo = request.CostoUnitario ?? (prod.CostoUnitario ?? exist!.CostoPromedio);
        var saldo = CostoPromedioCalculator.Entrada(new(exist!.Cantidad, exist.CostoPromedio), request.Cantidad, costo);
        await AplicarAsync(empresaId, prod, exist, saldo, TiposMovimientoInventario.Entrada, request.Cantidad, costo,
            NormalizarOrigen(request.Origen), request.OrigenId, request.Fecha, request.Referencia, request.Nota, actor, ct,
            numeroLote: numeroLote);
        return Result<ExistenciaDto>.Ok(ToDto(prod.Id, prod.CodigoInterno, prod.Nombre, exist.Cantidad, exist.CostoPromedio, exist.StockMinimo));
    }

    public async Task<Result<ExistenciaDto>> RegistrarSalidaAsync(int empresaId, RegistrarMovimientoInventarioRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Cantidad <= 0) return Result<ExistenciaDto>.Fail("La cantidad debe ser mayor que cero.", "VALIDATION");
        var (prod, exist, err) = await CargarAsync(empresaId, request.ProductoId, request.SucursalId, ct);
        if (err is not null) return err;
        if (request.Cantidad > exist!.Cantidad)
            return Result<ExistenciaDto>.Fail($"Stock insuficiente (disponible {exist.Cantidad:N2}).", "STOCK_INSUFICIENTE");

        string? numeroLote = null;
        string? notaLotes = null;
        if (prod!.ControlaLote)
        {
            var consumo = await ConsumirLotesAsync(empresaId, prod.Id, request.SucursalId, request.Cantidad,
                request.NumeroLote?.Trim().ToUpperInvariant(), actor, ct);
            if (consumo.IsFailure)
                return Result<ExistenciaDto>.Fail(consumo.Error!, consumo.ErrorCode);
            (numeroLote, notaLotes) = consumo.Value;
        }

        var nota = string.IsNullOrEmpty(notaLotes)
            ? request.Nota
            : string.IsNullOrWhiteSpace(request.Nota) ? notaLotes : $"{request.Nota} | {notaLotes}";

        var saldo = CostoPromedioCalculator.Salida(new(exist.Cantidad, exist.CostoPromedio), request.Cantidad);
        await AplicarAsync(empresaId, prod, exist, saldo, TiposMovimientoInventario.Salida, request.Cantidad, exist.CostoPromedio,
            NormalizarOrigen(request.Origen), request.OrigenId, request.Fecha, request.Referencia, nota, actor, ct,
            actualizarCostoProducto: false, numeroLote: numeroLote);
        return Result<ExistenciaDto>.Ok(ToDto(prod.Id, prod.CodigoInterno, prod.Nombre, exist.Cantidad, exist.CostoPromedio, exist.StockMinimo));
    }

    /// <summary>
    /// Consume el saldo por lotes: con número de lote explícito descuenta de ese lote;
    /// sin él aplica FEFO (vence primero → sale primero; lotes sin vencimiento al final).
    /// Devuelve el número de lote del movimiento ("FEFO" si cruzó varios) y la nota de detalle.
    /// </summary>
    private async Task<Result<(string NumeroLote, string? Nota)>> ConsumirLotesAsync(
        int empresaId, int productoId, int? sucursalId, decimal cantidad, string? loteSolicitado, string? actor, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(loteSolicitado))
        {
            var lote = await _db.LotesProducto.FirstOrDefaultAsync(
                l => l.EmpresaId == empresaId && l.ProductoId == productoId
                  && l.SucursalId == sucursalId && l.NumeroLote == loteSolicitado, ct);
            if (lote is null)
                return Result<(string, string?)>.Fail($"El lote {loteSolicitado} no existe.", "LOTE_NOT_FOUND");
            if (lote.Cantidad < cantidad)
                return Result<(string, string?)>.Fail(
                    $"El lote {loteSolicitado} solo tiene {lote.Cantidad:N2} unidades.", "LOTE_INSUFICIENTE");

            lote.Cantidad -= cantidad;
            lote.UpdatedAt = DateTime.UtcNow; lote.UpdatedBy = actor;
            return Result<(string, string?)>.Ok((loteSolicitado, null));
        }

        var lotes = await _db.LotesProducto
            .Where(l => l.EmpresaId == empresaId && l.ProductoId == productoId
                     && l.SucursalId == sucursalId && l.Cantidad > 0)
            .OrderBy(l => l.FechaVencimiento == null)
            .ThenBy(l => l.FechaVencimiento)
            .ThenBy(l => l.Id)
            .ToListAsync(ct);

        var restante = cantidad;
        var consumidos = new List<string>();
        foreach (var lote in lotes)
        {
            if (restante <= 0) break;
            var tomar = Math.Min(lote.Cantidad, restante);
            lote.Cantidad -= tomar;
            lote.UpdatedAt = DateTime.UtcNow; lote.UpdatedBy = actor;
            restante -= tomar;
            consumidos.Add($"{lote.NumeroLote}:{tomar:0.##}");
        }

        // Stock previo a activar el control de lotes: se descarga sin lote y se deja rastro.
        if (restante > 0)
        {
            if (consumidos.Count == 0)
                return Result<(string, string?)>.Ok(("SIN_LOTE", null));
            consumidos.Add($"sin lote:{restante:0.##}");
            return Result<(string, string?)>.Ok(("FEFO", $"Lotes: {string.Join(", ", consumidos)}"));
        }

        return consumidos.Count == 1
            ? Result<(string, string?)>.Ok((consumidos[0].Split(':')[0], null))
            : Result<(string, string?)>.Ok(("FEFO", $"Lotes: {string.Join(", ", consumidos)}"));
    }

    public async Task<Result<IReadOnlyList<LoteDto>>> ListLotesAsync(int empresaId, int? productoId = null,
        bool soloPorVencer = false, int diasUmbral = 30, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = from l in _db.LotesProducto.AsNoTracking()
                join p in _db.Productos.AsNoTracking() on l.ProductoId equals p.Id
                where l.EmpresaId == empresaId && l.Cantidad > 0
                select new { l, p.CodigoInterno, p.Nombre };
        if (productoId is int pidLotes) q = q.Where(x => x.l.ProductoId == pidLotes);

        var rows = await q.OrderBy(x => x.l.FechaVencimiento == null)
            .ThenBy(x => x.l.FechaVencimiento).ThenBy(x => x.Nombre)
            .ToListAsync(ct);

        var items = rows.Select(x =>
        {
            int? dias = x.l.FechaVencimiento is DateOnly v ? v.DayNumber - hoy.DayNumber : null;
            return new LoteDto
            {
                Id = x.l.Id, ProductoId = x.l.ProductoId, SucursalId = x.l.SucursalId,
                ProductoCodigo = x.CodigoInterno, ProductoNombre = x.Nombre,
                NumeroLote = x.l.NumeroLote, FechaVencimiento = x.l.FechaVencimiento,
                Cantidad = x.l.Cantidad, DiasParaVencer = dias,
                Vencido = dias < 0, PorVencer = dias >= 0 && dias <= diasUmbral,
            };
        }).ToList();

        if (soloPorVencer)
            items = items.Where(i => i.Vencido || i.PorVencer).ToList();

        return Result<IReadOnlyList<LoteDto>>.Ok(items);
    }

    public async Task<Result<ExistenciaDto>> AjustarAsync(int empresaId, AjusteStockRequest request, string? actor, CancellationToken ct = default)
    {
        var (prod, exist, err) = await CargarAsync(empresaId, request.ProductoId, request.SucursalId, ct);
        if (err is not null) return err;

        var saldo = CostoPromedioCalculator.Ajuste(new(exist!.Cantidad, exist.CostoPromedio), request.CantidadAbsoluta, request.CostoUnitario);
        await AplicarAsync(empresaId, prod!, exist, saldo, TiposMovimientoInventario.Ajuste, request.CantidadAbsoluta, saldo.CostoPromedio,
            OrigenesMovimientoInventario.Ajuste, null, null, null, request.Nota, actor, ct, sucursalId: request.SucursalId);
        return Result<ExistenciaDto>.Ok(ToDto(prod!.Id, prod.CodigoInterno, prod.Nombre, exist.Cantidad, exist.CostoPromedio, exist.StockMinimo));
    }

    public async Task<Result<ExistenciaDto>> SetStockMinimoAsync(int empresaId, SetStockMinimoRequest request, string? actor, CancellationToken ct = default)
    {
        var (prod, exist, err) = await CargarAsync(empresaId, request.ProductoId, request.SucursalId, ct);
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

    public async Task<Result> TrasladarAsync(int empresaId, TrasladoInventarioRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Cantidad <= 0) return Result.Fail("La cantidad debe ser mayor que cero.", "VALIDATION");
        if ((request.SucursalOrigenId ?? 0) == (request.SucursalDestinoId ?? 0))
            return Result.Fail("El origen y el destino del traslado deben ser distintos.", "VALIDATION");

        var (prod, origen, errO) = await CargarAsync(empresaId, request.ProductoId, request.SucursalOrigenId, ct);
        if (errO is not null) return Result.Fail(errO.Error!, errO.ErrorCode);
        if (origen!.Cantidad < request.Cantidad)
            return Result.Fail($"Stock insuficiente en el origen (disponible {origen.Cantidad:N2}).", "STOCK_INSUFICIENTE");

        var (_, destino, errD) = await CargarAsync(empresaId, request.ProductoId, request.SucursalDestinoId, ct);
        if (errD is not null) return Result.Fail(errD.Error!, errD.ErrorCode);

        // Lotes: se consumen en el origen (lote específico o FEFO) y se replican en el destino
        // conservando número y vencimiento.
        string? numeroLote = null;
        string? notaLotes = null;
        if (prod!.ControlaLote)
        {
            var loteSolicitado = request.NumeroLote?.Trim().ToUpperInvariant();
            var lotesOrigen = await _db.LotesProducto
                .Where(l => l.EmpresaId == empresaId && l.ProductoId == prod.Id
                         && l.SucursalId == request.SucursalOrigenId && l.Cantidad > 0)
                .OrderBy(l => l.FechaVencimiento == null).ThenBy(l => l.FechaVencimiento).ThenBy(l => l.Id)
                .ToListAsync(ct);
            if (!string.IsNullOrEmpty(loteSolicitado))
                lotesOrigen = lotesOrigen.Where(l => l.NumeroLote == loteSolicitado).ToList();

            var restante = request.Cantidad;
            var movidos = new List<(string Numero, DateOnly? Vence, decimal Cantidad)>();
            foreach (var lote in lotesOrigen)
            {
                if (restante <= 0) break;
                var tomar = Math.Min(lote.Cantidad, restante);
                lote.Cantidad -= tomar;
                lote.UpdatedAt = DateTime.UtcNow; lote.UpdatedBy = actor;
                restante -= tomar;
                movidos.Add((lote.NumeroLote, lote.FechaVencimiento, tomar));
            }
            if (restante > 0 && !string.IsNullOrEmpty(loteSolicitado))
                return Result.Fail($"El lote {loteSolicitado} no tiene saldo suficiente en el origen.", "LOTE_INSUFICIENTE");

            foreach (var (numero, vence, cantidadLote) in movidos)
            {
                var loteDestino = await _db.LotesProducto.FirstOrDefaultAsync(
                    l => l.EmpresaId == empresaId && l.ProductoId == prod.Id
                      && l.SucursalId == request.SucursalDestinoId && l.NumeroLote == numero, ct);
                if (loteDestino is null)
                {
                    loteDestino = new LoteProducto
                    {
                        EmpresaId = empresaId, ProductoId = prod.Id, SucursalId = request.SucursalDestinoId,
                        NumeroLote = numero, FechaVencimiento = vence, Cantidad = 0m,
                        CreatedAt = DateTime.UtcNow, CreatedBy = actor,
                    };
                    _db.LotesProducto.Add(loteDestino);
                }
                loteDestino.Cantidad += cantidadLote;
                loteDestino.UpdatedAt = DateTime.UtcNow; loteDestino.UpdatedBy = actor;
            }

            numeroLote = movidos.Count == 1 ? movidos[0].Numero : movidos.Count > 1 ? "FEFO" : "SIN_LOTE";
            if (movidos.Count > 1)
                notaLotes = $"Lotes: {string.Join(", ", movidos.Select(m => $"{m.Numero}:{m.Cantidad:0.##}"))}";
        }

        // Salida en origen + entrada en destino al costo promedio del origen; un solo SaveChanges.
        var referencia = $"TRAS-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var costo = origen.CostoPromedio;
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow);
        var nota = string.IsNullOrWhiteSpace(request.Nota) ? notaLotes
            : string.IsNullOrEmpty(notaLotes) ? request.Nota.Trim() : $"{request.Nota.Trim()} | {notaLotes}";

        var saldoOrigen = CostoPromedioCalculator.Salida(new(origen.Cantidad, origen.CostoPromedio), request.Cantidad);
        origen.Cantidad = saldoOrigen.Cantidad; origen.CostoPromedio = saldoOrigen.CostoPromedio;
        origen.UpdatedAt = DateTime.UtcNow; origen.UpdatedBy = actor;

        var saldoDestino = CostoPromedioCalculator.Entrada(new(destino!.Cantidad, destino.CostoPromedio), request.Cantidad, costo);
        destino.Cantidad = saldoDestino.Cantidad; destino.CostoPromedio = saldoDestino.CostoPromedio;
        destino.UpdatedAt = DateTime.UtcNow; destino.UpdatedBy = actor;

        _db.MovimientosInventario.Add(new MovimientoInventario
        {
            EmpresaId = empresaId, ProductoId = prod.Id, SucursalId = request.SucursalOrigenId, Fecha = fecha,
            Tipo = TiposMovimientoInventario.Salida, Cantidad = request.Cantidad, CostoUnitario = costo,
            Origen = OrigenesMovimientoInventario.Traslado, Referencia = referencia,
            Nota = nota, NumeroLote = numeroLote,
            SaldoCantidad = saldoOrigen.Cantidad, SaldoCostoPromedio = saldoOrigen.CostoPromedio, CreatedBy = actor,
        });
        _db.MovimientosInventario.Add(new MovimientoInventario
        {
            EmpresaId = empresaId, ProductoId = prod.Id, SucursalId = request.SucursalDestinoId, Fecha = fecha,
            Tipo = TiposMovimientoInventario.Entrada, Cantidad = request.Cantidad, CostoUnitario = costo,
            Origen = OrigenesMovimientoInventario.Traslado, Referencia = referencia,
            Nota = nota, NumeroLote = numeroLote,
            SaldoCantidad = saldoDestino.Cantidad, SaldoCostoPromedio = saldoDestino.CostoPromedio, CreatedBy = actor,
        });

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "TRASLADO",
            $"{prod.CodigoInterno} {request.Cantidad:N2}: suc {(request.SucursalOrigenId?.ToString() ?? "central")} → {(request.SucursalDestinoId?.ToString() ?? "central")} ({referencia})",
            prod.Id);
        return Result.Ok();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(Domain.Core.Productos.Producto? prod, ExistenciaProducto? exist, Result<ExistenciaDto>? err)> CargarAsync(int empresaId, int productoId, int? sucursalId, CancellationToken ct)
    {
        var prod = await _db.Productos.FirstOrDefaultAsync(p => p.Id == productoId && p.EmpresaId == empresaId, ct);
        if (prod is null) return (null, null, Result<ExistenciaDto>.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND"));

        if (sucursalId is int sid
            && !await _db.Sucursales.AnyAsync(s => s.Id == sid && s.EmpresaId == empresaId, ct))
        {
            return (null, null, Result<ExistenciaDto>.Fail("Sucursal no encontrada.", "SUCURSAL_NOT_FOUND"));
        }

        var exist = await _db.ExistenciasProducto.FirstOrDefaultAsync(
            e => e.EmpresaId == empresaId && e.ProductoId == productoId && e.SucursalId == sucursalId, ct);
        if (exist is null)
        {
            exist = new ExistenciaProducto
            {
                EmpresaId = empresaId, ProductoId = productoId, SucursalId = sucursalId,
                Cantidad = 0m, CostoPromedio = prod.CostoUnitario ?? 0m,
            };
            _db.ExistenciasProducto.Add(exist);
        }
        return (prod, exist, null);
    }

    private async Task AplicarAsync(int empresaId, Domain.Core.Productos.Producto prod, ExistenciaProducto exist,
        CostoPromedioCalculator.Saldo saldo, string tipo, decimal cantidad, decimal costoMovimiento,
        string origen, int? origenId, DateOnly? fecha, string? referencia, string? nota, string? actor,
        CancellationToken ct, bool actualizarCostoProducto = true, string? numeroLote = null, int? sucursalId = null)
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
            EmpresaId = empresaId, ProductoId = prod.Id,
            SucursalId = sucursalId ?? exist.SucursalId,
            Fecha = fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Tipo = tipo, Cantidad = cantidad, CostoUnitario = costoMovimiento, Origen = origen, OrigenId = origenId,
            Referencia = referencia?.Trim(), Nota = nota?.Trim(), NumeroLote = numeroLote,
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
        Id = m.Id, ProductoId = m.ProductoId, SucursalId = m.SucursalId, Fecha = m.Fecha, Tipo = m.Tipo, Cantidad = m.Cantidad,
        CostoUnitario = m.CostoUnitario, Origen = m.Origen, OrigenId = m.OrigenId, Referencia = m.Referencia, Nota = m.Nota,
        NumeroLote = m.NumeroLote,
        SaldoCantidad = m.SaldoCantidad, SaldoCostoPromedio = m.SaldoCostoPromedio,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "Inventario", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
