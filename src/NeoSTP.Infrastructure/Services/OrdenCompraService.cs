using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>V3-S1 - ordenes de compra y conversion controlada a factura/CxP.</summary>
public sealed class OrdenCompraService : IOrdenCompraService
{
    private const string AuditModule = "NEOCOMPRAS";

    private readonly NeoStpDbContext _db;
    private readonly ICompraService _compras;
    private readonly IAuditoriaService _auditoria;

    public OrdenCompraService(NeoStpDbContext db, ICompraService compras, IAuditoriaService auditoria)
    {
        _db = db;
        _compras = compras;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<OrdenCompraDto>>> ListAsync(
        int empresaId, string? estado, int? proveedorId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.OrdenesCompra.AsNoTracking().Where(x => x.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var normalizado = estado.Trim().ToUpperInvariant();
            if (!OrdenCompraEstados.All.Contains(normalizado))
                return Result<PagedResult<OrdenCompraDto>>.Fail("Estado de orden invalido.", "VALIDATION");
            q = q.Where(x => x.EstadoCodigo == normalizado);
        }
        if (proveedorId is int pid) q = q.Where(x => x.ProveedorId == pid);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(x => x.Numero.Contains(search) || x.Proveedor.Nombre.Contains(search));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new OrdenCompraDto
            {
                Id = x.Id,
                Numero = x.Numero,
                ProveedorId = x.ProveedorId,
                ProveedorNombre = x.Proveedor.Nombre,
                Fecha = x.Fecha,
                FechaEntregaEsperada = x.FechaEntregaEsperada,
                EstadoCodigo = x.EstadoCodigo,
                MonedaCodigo = x.MonedaCodigo,
                Subtotal = x.Subtotal,
                Iva = x.Iva,
                Total = x.Total,
                Lineas = x.Lineas.Count,
                FacturaCompraId = x.FacturaCompraId,
            }).ToListAsync(ct);

        return Result<PagedResult<OrdenCompraDto>>.Ok(PagedResult<OrdenCompraDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<OrdenCompraDetalleDto>> GetAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var orden = await QueryDetalle().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return orden is null
            ? Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND")
            : Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    public async Task<Result<OrdenCompraDetalleDto>> CrearAsync(
        int empresaId, GuardarOrdenCompraRequest request, string? actor, CancellationToken ct = default)
    {
        var proveedor = await _db.Proveedores.FirstOrDefaultAsync(
            x => x.Id == request.ProveedorId && x.EmpresaId == empresaId, ct);
        if (proveedor is null)
            return Result<OrdenCompraDetalleDto>.Fail("Proveedor no encontrado.", "PROVEEDOR_NOT_FOUND");
        if (proveedor.EstadoCodigo != ProveedorEstados.Activo)
            return Result<OrdenCompraDetalleDto>.Fail("El proveedor esta inactivo.", "INVALID_STATE");

        var fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.FechaEntregaEsperada is DateOnly entrega && entrega < fecha)
            return Result<OrdenCompraDetalleDto>.Fail("La entrega esperada no puede ser anterior a la orden.", "VALIDATION");

        var lineas = await BuildLineasAsync(empresaId, request.Lineas, actor, ct);
        if (lineas.IsFailure)
            return Result<OrdenCompraDetalleDto>.Fail(lineas.Error!, lineas.ErrorCode, lineas.ValidationErrors);

        var orden = new OrdenCompra
        {
            EmpresaId = empresaId,
            ProveedorId = proveedor.Id,
            Proveedor = proveedor,
            Numero = $"OC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant(),
            Fecha = fecha,
            FechaEntregaEsperada = request.FechaEntregaEsperada,
            EstadoCodigo = OrdenCompraEstados.Borrador,
            MonedaCodigo = "USD",
            Observaciones = request.Observaciones?.Trim(),
            CreatedBy = actor,
            Lineas = lineas.Value!,
        };
        AplicarTotales(orden);

        _db.OrdenesCompra.Add(orden);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_ORDEN_COMPRA", orden.Numero, orden.Id);
        return Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    public async Task<Result<OrdenCompraDetalleDto>> ActualizarAsync(
        int empresaId, int id, GuardarOrdenCompraRequest request, string? actor, CancellationToken ct = default)
    {
        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        if (orden.EstadoCodigo != OrdenCompraEstados.Borrador)
            return Result<OrdenCompraDetalleDto>.Fail("Solo una orden en borrador puede editarse.", "INVALID_STATE");

        var proveedor = await _db.Proveedores.FirstOrDefaultAsync(
            x => x.Id == request.ProveedorId && x.EmpresaId == empresaId, ct);
        if (proveedor is null)
            return Result<OrdenCompraDetalleDto>.Fail("Proveedor no encontrado.", "PROVEEDOR_NOT_FOUND");
        if (proveedor.EstadoCodigo != ProveedorEstados.Activo)
            return Result<OrdenCompraDetalleDto>.Fail("El proveedor esta inactivo.", "INVALID_STATE");

        var fecha = request.Fecha ?? orden.Fecha;
        if (request.FechaEntregaEsperada is DateOnly entrega && entrega < fecha)
            return Result<OrdenCompraDetalleDto>.Fail("La entrega esperada no puede ser anterior a la orden.", "VALIDATION");

        var lineas = await BuildLineasAsync(empresaId, request.Lineas, actor, ct);
        if (lineas.IsFailure)
            return Result<OrdenCompraDetalleDto>.Fail(lineas.Error!, lineas.ErrorCode, lineas.ValidationErrors);

        _db.OrdenCompraLineas.RemoveRange(orden.Lineas);
        orden.ProveedorId = proveedor.Id;
        orden.Proveedor = proveedor;
        orden.Fecha = fecha;
        orden.FechaEntregaEsperada = request.FechaEntregaEsperada;
        orden.Observaciones = request.Observaciones?.Trim();
        orden.UpdatedAt = DateTime.UtcNow;
        orden.UpdatedBy = actor;
        orden.Lineas = lineas.Value!;
        AplicarTotales(orden);

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR_ORDEN_COMPRA", orden.Numero, orden.Id);
        return Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    public Task<Result<OrdenCompraDetalleDto>> EmitirAsync(
        int empresaId, int id, string? actor, CancellationToken ct = default)
        => CambiarEstadoAsync(empresaId, id, OrdenCompraEstados.Borrador, OrdenCompraEstados.Emitida,
            "EMITIR_ORDEN_COMPRA", actor, ct);

    public async Task<Result<OrdenCompraDetalleDto>> CancelarAsync(
        int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        if (orden.EstadoCodigo is OrdenCompraEstados.Recibida or OrdenCompraEstados.Cancelada)
            return Result<OrdenCompraDetalleDto>.Fail("La orden no puede cancelarse en su estado actual.", "INVALID_STATE");

        orden.EstadoCodigo = OrdenCompraEstados.Cancelada;
        orden.UpdatedAt = DateTime.UtcNow;
        orden.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CANCELAR_ORDEN_COMPRA", orden.Numero, orden.Id);
        return Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    public async Task<Result<OrdenCompraDetalleDto>> ConvertirAFacturaAsync(
        int empresaId, int id, ConvertirOrdenCompraRequest request, string? actor, CancellationToken ct = default)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        if (orden.EstadoCodigo != OrdenCompraEstados.Emitida || orden.FacturaCompraId is not null)
            return Result<OrdenCompraDetalleDto>.Fail("Solo una orden emitida y no recibida puede convertirse.", "INVALID_STATE");

        var inventario = orden.Lineas
            .Where(x => !x.Producto.EsServicio)
            .Select(x => new CompraInventarioLineaRequest
            {
                ProductoId = x.ProductoId,
                Cantidad = x.Cantidad,
                CostoUnitario = x.PrecioUnitario,
            }).ToList();

        var factura = await _compras.CrearFacturaAsync(empresaId, new CrearFacturaCompraRequest
        {
            ProveedorId = orden.ProveedorId,
            NumeroDocumento = request.NumeroDocumento,
            TipoDocumento = request.TipoDocumento,
            FechaEmision = request.FechaEmision,
            FechaVencimiento = request.FechaVencimiento,
            CondicionPago = request.CondicionPago,
            Subtotal = orden.Subtotal,
            Iva = orden.Iva,
            IvaDeducible = request.IvaDeducible,
            Descripcion = string.IsNullOrWhiteSpace(request.Descripcion)
                ? $"Recepcion de {orden.Numero}"
                : $"{orden.Numero}: {request.Descripcion.Trim()}",
            RegistrarGastoProfit = request.RegistrarGastoProfit,
            LineasInventario = inventario.Count == 0 ? null : inventario,
        }, actor, ct);
        if (factura.IsFailure)
            return Result<OrdenCompraDetalleDto>.Fail(factura.Error!, factura.ErrorCode, factura.ValidationErrors);

        orden.FacturaCompraId = factura.Value!.Id;
        orden.FacturaCompra = null;
        orden.EstadoCodigo = OrdenCompraEstados.Recibida;
        orden.UpdatedAt = DateTime.UtcNow;
        orden.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RECIBIR_ORDEN_COMPRA", $"{orden.Numero} -> factura {factura.Value.Id}", orden.Id);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    private async Task<Result<OrdenCompraDetalleDto>> CambiarEstadoAsync(
        int empresaId, int id, string esperado, string nuevo, string accion, string? actor, CancellationToken ct)
    {
        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        if (orden.EstadoCodigo != esperado)
            return Result<OrdenCompraDetalleDto>.Fail($"La orden debe estar en estado {esperado}.", "INVALID_STATE");

        orden.EstadoCodigo = nuevo;
        orden.UpdatedAt = DateTime.UtcNow;
        orden.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, accion, orden.Numero, orden.Id);
        return Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    private async Task<Result<List<OrdenCompraLinea>>> BuildLineasAsync(
        int empresaId, IReadOnlyCollection<GuardarOrdenCompraLineaRequest>? requests, string? actor, CancellationToken ct)
    {
        if (requests is null || requests.Count == 0)
            return Result<List<OrdenCompraLinea>>.Fail("La orden requiere al menos una linea.", "VALIDATION");
        if (requests.GroupBy(x => x.ProductoId).Any(x => x.Count() > 1))
            return Result<List<OrdenCompraLinea>>.Fail("No repita productos; consolide la cantidad en una linea.", "VALIDATION");

        var ids = requests.Select(x => x.ProductoId).Distinct().ToList();
        var productos = await _db.Productos.Where(x => x.EmpresaId == empresaId && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        if (productos.Count != ids.Count)
            return Result<List<OrdenCompraLinea>>.Fail("Uno o mas productos no existen en la empresa.", "PRODUCTO_NOT_FOUND");

        var lineas = new List<OrdenCompraLinea>(requests.Count);
        var numero = 1;
        foreach (var request in requests)
        {
            var producto = productos[request.ProductoId];
            if (producto.EstadoCodigo != "ACTIVO")
                return Result<List<OrdenCompraLinea>>.Fail($"El producto {producto.CodigoInterno} esta inactivo.", "INVALID_STATE");

            OrdenCompraLineaCalculo calculo;
            try
            {
                calculo = OrdenCompraCalculator.CalcularLinea(
                    request.Cantidad, request.PrecioUnitario, request.AplicaIva ?? producto.AplicaIva);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Result<List<OrdenCompraLinea>>.Fail("Cantidad o precio de linea invalido.", "VALIDATION");
            }

            lineas.Add(new OrdenCompraLinea
            {
                EmpresaId = empresaId,
                NumeroLinea = numero++,
                ProductoId = producto.Id,
                Producto = producto,
                Descripcion = producto.Nombre,
                UnidadMedidaCodigo = producto.UnidadMedidaCodigo,
                Cantidad = request.Cantidad,
                PrecioUnitario = request.PrecioUnitario,
                AplicaIva = request.AplicaIva ?? producto.AplicaIva,
                Subtotal = calculo.Subtotal,
                Iva = calculo.Iva,
                Total = calculo.Total,
                CreatedBy = actor,
            });
        }
        return Result<List<OrdenCompraLinea>>.Ok(lineas);
    }

    private IQueryable<OrdenCompra> QueryDetalle() => _db.OrdenesCompra
        .Include(x => x.Proveedor)
        .Include(x => x.Lineas).ThenInclude(x => x.Producto);

    private static void AplicarTotales(OrdenCompra orden)
    {
        orden.Subtotal = orden.Lineas.Sum(x => x.Subtotal);
        orden.Iva = orden.Lineas.Sum(x => x.Iva);
        orden.Total = orden.Lineas.Sum(x => x.Total);
    }

    private static OrdenCompraDetalleDto ToDetalle(OrdenCompra orden) => new()
    {
        Id = orden.Id,
        Numero = orden.Numero,
        ProveedorId = orden.ProveedorId,
        ProveedorNombre = orden.Proveedor?.Nombre ?? string.Empty,
        Fecha = orden.Fecha,
        FechaEntregaEsperada = orden.FechaEntregaEsperada,
        EstadoCodigo = orden.EstadoCodigo,
        MonedaCodigo = orden.MonedaCodigo,
        Subtotal = orden.Subtotal,
        Iva = orden.Iva,
        Total = orden.Total,
        Lineas = orden.Lineas.Count,
        FacturaCompraId = orden.FacturaCompraId,
        Observaciones = orden.Observaciones,
        Detalle = orden.Lineas.OrderBy(x => x.NumeroLinea).Select(x => new OrdenCompraLineaDto
        {
            Id = x.Id,
            NumeroLinea = x.NumeroLinea,
            ProductoId = x.ProductoId,
            ProductoCodigo = x.Producto?.CodigoInterno ?? string.Empty,
            Descripcion = x.Descripcion,
            UnidadMedidaCodigo = x.UnidadMedidaCodigo,
            EsServicio = x.Producto?.EsServicio ?? false,
            Cantidad = x.Cantidad,
            PrecioUnitario = x.PrecioUnitario,
            AplicaIva = x.AplicaIva,
            Subtotal = x.Subtotal,
            Iva = x.Iva,
            Total = x.Total,
        }).ToList(),
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int id)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = "OrdenCompra",
            EntidadId = id.ToString(),
            Resultado = "OK",
            Detalle = detalle,
        });
}
