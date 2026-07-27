using System.Data;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Application.Inventario;
using NeoSTP.Application.Inventario.Dtos;
using NeoSTP.Domain.Core.Common;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Inventario;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>V3 - ordenes, recepciones parciales y conversion controlada a factura/CxP.</summary>
public sealed class OrdenCompraService : IOrdenCompraService
{
    private const string AuditModule = "NEOCOMPRAS";

    private readonly NeoStpDbContext _db;
    private readonly ICompraService _compras;
    private readonly IInventarioService _inventario;
    private readonly IAuditoriaService _auditoria;
    private readonly ICorrelativoService? _correlativos;
    private readonly IConnectWebhookDispatcher? _webhooks;

    public OrdenCompraService(
        NeoStpDbContext db,
        ICompraService compras,
        IInventarioService inventario,
        IAuditoriaService auditoria,
        ICorrelativoService? correlativos = null,
        IConnectWebhookDispatcher? webhooks = null)
    {
        _db = db;
        _compras = compras;
        _inventario = inventario;
        _auditoria = auditoria;
        _correlativos = correlativos;
        _webhooks = webhooks;
    }

    /// <summary>
    /// Número del documento. Con el servicio de correlativos entrega OC-2026-000042;
    /// sin él (llamadores antiguos en pruebas) cae al esquema anterior.
    /// </summary>
    private async Task<string> NumerarAsync(int empresaId, string serie, CancellationToken ct)
        => _correlativos is not null
            ? await _correlativos.SiguienteAsync(empresaId, serie, ct)
            : $"{serie}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28].ToUpperInvariant();

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
                Recepciones = x.Recepciones.Count,
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
            Numero = await NumerarAsync(empresaId, CorrelativoSeries.OrdenCompra, ct),
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

    public async Task<Result<OrdenCompraDetalleDto>> EmitirAsync(
        int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        // E4: sobre el umbral de la empresa, la orden requiere aprobación antes de emitirse.
        var umbral = await _db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => e.UmbralAprobacionCompras)
            .FirstOrDefaultAsync(ct);

        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        if (orden.EstadoCodigo != OrdenCompraEstados.Borrador)
            return Result<OrdenCompraDetalleDto>.Fail($"La orden debe estar en estado {OrdenCompraEstados.Borrador}.", "INVALID_STATE");

        var requiereAprobacion = umbral is decimal u && u > 0 && orden.Total >= u;
        orden.EstadoCodigo = requiereAprobacion ? OrdenCompraEstados.PorAprobar : OrdenCompraEstados.Emitida;
        orden.UpdatedAt = DateTime.UtcNow;
        orden.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor,
            requiereAprobacion ? "ENVIAR_APROBACION_ORDEN_COMPRA" : "EMITIR_ORDEN_COMPRA",
            requiereAprobacion ? $"{orden.Numero} (total {orden.Total:N2} ≥ umbral {umbral:N2})" : orden.Numero,
            orden.Id);

        // E6: avisar al integrador para que dispare su flujo de autorización.
        if (requiereAprobacion && _webhooks is not null)
        {
            await _webhooks.DispatchNegocioAsync(new ConnectEventoNegocioPayload
            {
                Evento = ConnectEventos.CompraOrdenPorAprobar,
                EmpresaId = empresaId,
                EntidadTipo = "OrdenCompra",
                EntidadId = orden.Id,
                Descripcion = $"La orden {orden.Numero} por $ {orden.Total:N2} espera aprobación.",
                Datos = new Dictionary<string, object?>
                {
                    ["numero"] = orden.Numero,
                    ["proveedor"] = orden.Proveedor?.Nombre,
                    ["total"] = orden.Total,
                    ["umbral"] = umbral,
                },
            }, ct);
        }

        return Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    public Task<Result<OrdenCompraDetalleDto>> AprobarAsync(
        int empresaId, int id, string? actor, CancellationToken ct = default)
        => CambiarEstadoAsync(empresaId, id, OrdenCompraEstados.PorAprobar, OrdenCompraEstados.Emitida,
            "APROBAR_ORDEN_COMPRA", actor, ct);

    public async Task<Result<OrdenCompraDetalleDto>> RechazarAsync(
        int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default)
    {
        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        if (orden.EstadoCodigo != OrdenCompraEstados.PorAprobar)
            return Result<OrdenCompraDetalleDto>.Fail($"La orden debe estar en estado {OrdenCompraEstados.PorAprobar}.", "INVALID_STATE");

        orden.EstadoCodigo = OrdenCompraEstados.Borrador;
        if (!string.IsNullOrWhiteSpace(motivo))
        {
            var nota = $"[Rechazada {DateTime.UtcNow:dd/MM/yyyy}] {motivo.Trim()}";
            orden.Observaciones = string.IsNullOrWhiteSpace(orden.Observaciones)
                ? nota : $"{orden.Observaciones}\n{nota}";
        }
        orden.UpdatedAt = DateTime.UtcNow;
        orden.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RECHAZAR_ORDEN_COMPRA",
            $"{orden.Numero}{(string.IsNullOrWhiteSpace(motivo) ? "" : $": {motivo.Trim()}")}", orden.Id);
        return Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    public async Task<Result> SetUmbralAprobacionAsync(int empresaId, decimal? umbral, string? actor, CancellationToken ct = default)
    {
        if (umbral is < 0)
            return Result.Fail("El umbral no puede ser negativo.", "VALIDATION");

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return Result.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        empresa.UmbralAprobacionCompras = umbral is 0 ? null : umbral;
        empresa.UpdatedAt = DateTime.UtcNow;
        empresa.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UMBRAL_APROBACION",
            empresa.UmbralAprobacionCompras is decimal v ? $"Umbral = {v:N2}" : "Sin aprobaciones", empresaId);
        return Result.Ok();
    }

    public async Task<Result<OrdenCompraDetalleDto>> CancelarAsync(
        int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        if (orden.EstadoCodigo is OrdenCompraEstados.Parcial or OrdenCompraEstados.Recibida or OrdenCompraEstados.Cancelada)
            return Result<OrdenCompraDetalleDto>.Fail("La orden no puede cancelarse en su estado actual.", "INVALID_STATE");

        orden.EstadoCodigo = OrdenCompraEstados.Cancelada;
        orden.UpdatedAt = DateTime.UtcNow;
        orden.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CANCELAR_ORDEN_COMPRA", orden.Numero, orden.Id);
        return Result<OrdenCompraDetalleDto>.Ok(ToDetalle(orden));
    }

    public async Task<Result<OrdenCompraRecepcionDto>> RecibirAsync(
        int empresaId, int id, RegistrarRecepcionOrdenCompraRequest request, string? actor, CancellationToken ct = default)
    {
        var key = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 64)
            return Result<OrdenCompraRecepcionDto>.Fail("La idempotency key debe tener entre 8 y 64 caracteres.", "VALIDATION");
        if (request.Lineas is null || request.Lineas.Count == 0)
            return Result<OrdenCompraRecepcionDto>.Fail("La recepcion requiere al menos una linea.", "VALIDATION");
        if (request.Lineas.GroupBy(x => x.OrdenCompraLineaId).Any(x => x.Count() > 1))
            return Result<OrdenCompraRecepcionDto>.Fail("No repita lineas de la orden en una recepcion.", "VALIDATION");

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;

        var existente = await _db.OrdenCompraRecepciones
            .Include(x => x.Lineas).ThenInclude(x => x.OrdenCompraLinea).ThenInclude(x => x.Producto)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.IdempotencyKey == key, ct);
        if (existente is not null)
        {
            if (existente.OrdenCompraId != id)
                return Result<OrdenCompraRecepcionDto>.Fail("La idempotency key ya pertenece a otra orden.", "IDEMPOTENCY_CONFLICT");
            return Result<OrdenCompraRecepcionDto>.Ok(ToRecepcion(existente));
        }

        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraRecepcionDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        if (orden.EstadoCodigo is not (OrdenCompraEstados.Emitida or OrdenCompraEstados.Parcial))
            return Result<OrdenCompraRecepcionDto>.Fail("Solo una orden emitida o parcial puede recibirse.", "INVALID_STATE");

        var lineasOrden = orden.Lineas.ToDictionary(x => x.Id);
        var cantidadesNuevas = new Dictionary<int, decimal>();
        foreach (var item in request.Lineas)
        {
            if (!lineasOrden.TryGetValue(item.OrdenCompraLineaId, out var linea))
                return Result<OrdenCompraRecepcionDto>.Fail("Una linea no pertenece a la orden.", "ORDEN_LINEA_NOT_FOUND");
            if (item.Cantidad <= 0)
                return Result<OrdenCompraRecepcionDto>.Fail("Las cantidades recibidas deben ser mayores que cero.", "VALIDATION");

            var recibida = linea.Recepciones.Sum(x => x.Cantidad);
            var pendiente = linea.Cantidad - recibida;
            if (item.Cantidad > pendiente)
                return Result<OrdenCompraRecepcionDto>.Fail(
                    $"La recepcion de {linea.Descripcion} excede la cantidad pendiente ({pendiente:N4}).", "RECEPCION_EXCEDE_PENDIENTE");
            cantidadesNuevas[linea.Id] = item.Cantidad;
        }

        var fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (fecha < orden.Fecha)
            return Result<OrdenCompraRecepcionDto>.Fail("La fecha de recepcion no puede ser anterior a la orden.", "VALIDATION");
        var recepcion = new OrdenCompraRecepcion
        {
            EmpresaId = empresaId,
            OrdenCompraId = orden.Id,
            OrdenCompra = orden,
            Numero = await NumerarAsync(empresaId, CorrelativoSeries.RecepcionCompra, ct),
            IdempotencyKey = key,
            Fecha = fecha,
            Referencia = request.Referencia?.Trim(),
            Observaciones = request.Observaciones?.Trim(),
            CreatedBy = actor,
            Lineas = request.Lineas.Select(item => new OrdenCompraRecepcionLinea
            {
                EmpresaId = empresaId,
                OrdenCompraLineaId = item.OrdenCompraLineaId,
                OrdenCompraLinea = lineasOrden[item.OrdenCompraLineaId],
                Cantidad = item.Cantidad,
                CreatedBy = actor,
            }).ToList(),
        };
        orden.Recepciones.Add(recepcion);
        await _db.SaveChangesAsync(ct);

        foreach (var lineaRecepcion in recepcion.Lineas.Where(x => !x.OrdenCompraLinea.Producto.EsServicio))
        {
            var entrada = await _inventario.RegistrarEntradaAsync(empresaId, new RegistrarMovimientoInventarioRequest
            {
                ProductoId = lineaRecepcion.OrdenCompraLinea.ProductoId,
                Fecha = fecha,
                Cantidad = lineaRecepcion.Cantidad,
                CostoUnitario = lineaRecepcion.OrdenCompraLinea.PrecioUnitario,
                Origen = OrigenesMovimientoInventario.RecepcionCompra,
                OrigenId = recepcion.Id,
                Referencia = orden.Numero,
                Nota = recepcion.Referencia,
            }, actor, ct);
            if (entrada.IsFailure)
                return Result<OrdenCompraRecepcionDto>.Fail(entrada.Error!, entrada.ErrorCode, entrada.ValidationErrors);

            lineaRecepcion.MovimientoInventarioId = await _db.MovimientosInventario
                .Where(x => x.EmpresaId == empresaId
                    && x.ProductoId == lineaRecepcion.OrdenCompraLinea.ProductoId
                    && x.Origen == OrigenesMovimientoInventario.RecepcionCompra
                    && x.OrigenId == recepcion.Id)
                .OrderByDescending(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync(ct);
        }

        var completa = orden.Lineas.All(linea =>
            linea.Recepciones.Sum(x => x.Cantidad) >= linea.Cantidad);
        orden.EstadoCodigo = completa ? OrdenCompraEstados.Recibida : OrdenCompraEstados.Parcial;
        orden.UpdatedAt = DateTime.UtcNow;
        orden.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RECIBIR_ORDEN_COMPRA",
            $"{orden.Numero} -> {recepcion.Numero} ({orden.EstadoCodigo})", orden.Id);
        if (transaction is not null) await transaction.CommitAsync(ct);

        return Result<OrdenCompraRecepcionDto>.Ok(ToRecepcion(recepcion));
    }

    public async Task<Result<OrdenCompraDetalleDto>> ConvertirAFacturaAsync(
        int empresaId, int id, ConvertirOrdenCompraRequest request, string? actor, CancellationToken ct = default)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;

        var orden = await QueryDetalle().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (orden is null)
            return Result<OrdenCompraDetalleDto>.Fail("Orden de compra no encontrada.", "ORDEN_COMPRA_NOT_FOUND");
        var tieneRecepciones = orden.Recepciones.Count > 0;
        var estadoValido = tieneRecepciones
            ? orden.EstadoCodigo == OrdenCompraEstados.Recibida
            : orden.EstadoCodigo == OrdenCompraEstados.Emitida;
        if (!estadoValido || orden.FacturaCompraId is not null)
            return Result<OrdenCompraDetalleDto>.Fail(
                "La orden debe estar completamente recibida y sin factura para convertirse.", "INVALID_STATE");

        var inventario = (tieneRecepciones ? [] : orden.Lineas
            .Where(x => !x.Producto.EsServicio)
            .Select(x => new CompraInventarioLineaRequest
            {
                ProductoId = x.ProductoId,
                Cantidad = x.Cantidad,
                CostoUnitario = x.PrecioUnitario,
            }).ToList());

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
                ? $"Orden de compra {orden.Numero}"
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
        await Audit(empresaId, actor, "FACTURAR_ORDEN_COMPRA", $"{orden.Numero} -> factura {factura.Value.Id}", orden.Id);
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
        .Include(x => x.Lineas).ThenInclude(x => x.Producto)
        .Include(x => x.Lineas).ThenInclude(x => x.Recepciones)
        .Include(x => x.Recepciones).ThenInclude(x => x.Lineas)
            .ThenInclude(x => x.OrdenCompraLinea).ThenInclude(x => x.Producto);

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
        Recepciones = orden.Recepciones.Count,
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
            CantidadRecibida = x.Recepciones.Sum(r => r.Cantidad),
            CantidadPendiente = Math.Max(0m, x.Cantidad - x.Recepciones.Sum(r => r.Cantidad)),
            PrecioUnitario = x.PrecioUnitario,
            AplicaIva = x.AplicaIva,
            Subtotal = x.Subtotal,
            Iva = x.Iva,
            Total = x.Total,
        }).ToList(),
        HistorialRecepciones = orden.Recepciones.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id)
            .Select(ToRecepcion).ToList(),
    };

    private static OrdenCompraRecepcionDto ToRecepcion(OrdenCompraRecepcion recepcion) => new()
    {
        Id = recepcion.Id,
        Numero = recepcion.Numero,
        Fecha = recepcion.Fecha,
        Referencia = recepcion.Referencia,
        Observaciones = recepcion.Observaciones,
        Lineas = recepcion.Lineas.OrderBy(x => x.OrdenCompraLinea.NumeroLinea).Select(x => new OrdenCompraRecepcionLineaDto
        {
            OrdenCompraLineaId = x.OrdenCompraLineaId,
            ProductoId = x.OrdenCompraLinea.ProductoId,
            ProductoCodigo = x.OrdenCompraLinea.Producto?.CodigoInterno ?? string.Empty,
            Descripcion = x.OrdenCompraLinea.Descripcion,
            Cantidad = x.Cantidad,
            MovimientoInventarioId = x.MovimientoInventarioId,
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
