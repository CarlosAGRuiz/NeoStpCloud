using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Application.Tesoreria.Dtos;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Tesoreria;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NEOCOMPRAS (Fase B). Proveedores + cuentas por pagar (espejo de Cobros/CxC). Registra
/// facturas de compra y pagos, deriva saldos/estados con <see cref="CuentasPagarCalculator"/>,
/// y opcionalmente alimenta NeoProfit (gasto) y Tesorería (egreso). Aislado por EmpresaId.
/// </summary>
public class CompraService : ICompraService
{
    private const string AuditModule = "NEOCOMPRAS";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IProfitService _profit;
    private readonly ITesoreriaService _tesoreria;

    public CompraService(NeoStpDbContext db, IAuditoriaService auditoria, IProfitService profit, ITesoreriaService tesoreria)
    {
        _db = db;
        _auditoria = auditoria;
        _profit = profit;
        _tesoreria = tesoreria;
    }

    // ── Proveedores ──────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<ProveedorDto>>> ListProveedoresAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.Proveedores.AsNoTracking().Where(p => p.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => p.Codigo.Contains(s) || p.Nombre.Contains(s) || (p.Nit != null && p.Nit.Contains(s)));
        }
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderBy(p => p.EstadoCodigo == ProveedorEstados.Activo ? 0 : 1).ThenBy(p => p.Nombre)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => ToProveedorDto(p)).ToListAsync(ct);
        return Result<PagedResult<ProveedorDto>>.Ok(PagedResult<ProveedorDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ProveedorDetalleDto>> GetProveedorAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var p = await _db.Proveedores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (p is null) return Result<ProveedorDetalleDto>.Fail("Proveedor no encontrado.", "PROVEEDOR_NOT_FOUND");

        var facturas = await _db.FacturasCompra.AsNoTracking()
            .Where(f => f.ProveedorId == id && f.EstadoCodigo != FacturaCompraEstados.Anulada)
            .Select(f => new { f.Total, Pagado = f.Pagos.Where(pg => pg.EstadoCodigo == PagoProveedorEstados.Confirmado).Sum(pg => (decimal?)pg.Monto) ?? 0m })
            .ToListAsync(ct);
        var saldo = facturas.Sum(f => CuentasPagarCalculator.Saldo(f.Total, f.Pagado));
        var pendientes = facturas.Count(f => CuentasPagarCalculator.Saldo(f.Total, f.Pagado) > 0);

        return Result<ProveedorDetalleDto>.Ok(new ProveedorDetalleDto
        {
            Id = p.Id, Codigo = p.Codigo, Nombre = p.Nombre, Nit = p.Nit, Nrc = p.Nrc,
            Contacto = p.Contacto, Telefono = p.Telefono, Email = p.Email, Direccion = p.Direccion,
            PlazoDiasDefault = p.PlazoDiasDefault, EstadoCodigo = p.EstadoCodigo,
            SaldoPorPagar = saldo, FacturasPendientes = pendientes,
        });
    }

    public async Task<Result<ProveedorDto>> CrearProveedorAsync(int empresaId, CreateProveedorRequest request, string? actor, CancellationToken ct = default)
    {
        var codigo = (request.Codigo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(codigo)) return Result<ProveedorDto>.Fail("El código es obligatorio.", "VALIDATION");
        var dup = await _db.Proveedores.AnyAsync(p => p.EmpresaId == empresaId && p.Codigo == codigo, ct);
        if (dup) return Result<ProveedorDto>.Fail("Ya existe un proveedor con ese código.", "DUPLICATE");

        var prov = new Proveedor
        {
            EmpresaId = empresaId, Codigo = codigo, Nombre = request.Nombre.Trim(),
            Nit = request.Nit?.Trim(), Nrc = request.Nrc?.Trim(), Contacto = request.Contacto?.Trim(),
            Telefono = request.Telefono?.Trim(), Email = request.Email?.Trim(), Direccion = request.Direccion?.Trim(),
            PlazoDiasDefault = request.PlazoDiasDefault, EstadoCodigo = ProveedorEstados.Activo, CreatedBy = actor,
        };
        _db.Proveedores.Add(prov);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_PROVEEDOR", $"{prov.Codigo} · {prov.Nombre}", "Proveedor", prov.Id);
        return Result<ProveedorDto>.Ok(ToProveedorDto(prov));
    }

    public async Task<Result<ProveedorDto>> ActualizarProveedorAsync(int empresaId, int id, UpdateProveedorRequest request, string? actor, CancellationToken ct = default)
    {
        var prov = await _db.Proveedores.FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId, ct);
        if (prov is null) return Result<ProveedorDto>.Fail("Proveedor no encontrado.", "PROVEEDOR_NOT_FOUND");

        prov.Nombre = request.Nombre.Trim();
        prov.Nit = request.Nit?.Trim(); prov.Nrc = request.Nrc?.Trim(); prov.Contacto = request.Contacto?.Trim();
        prov.Telefono = request.Telefono?.Trim(); prov.Email = request.Email?.Trim(); prov.Direccion = request.Direccion?.Trim();
        prov.PlazoDiasDefault = request.PlazoDiasDefault;
        prov.UpdatedAt = DateTime.UtcNow; prov.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR_PROVEEDOR", prov.Codigo, "Proveedor", prov.Id);
        return Result<ProveedorDto>.Ok(ToProveedorDto(prov));
    }

    public Task<Result> InactivarProveedorAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => CambiarEstadoProveedor(empresaId, id, ProveedorEstados.Inactivo, "INACTIVAR_PROVEEDOR", actor, ct);

    public Task<Result> ReactivarProveedorAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => CambiarEstadoProveedor(empresaId, id, ProveedorEstados.Activo, "REACTIVAR_PROVEEDOR", actor, ct);

    private async Task<Result> CambiarEstadoProveedor(int empresaId, int id, string estado, string accion, string? actor, CancellationToken ct)
    {
        var prov = await _db.Proveedores.FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId, ct);
        if (prov is null) return Result.Fail("Proveedor no encontrado.", "PROVEEDOR_NOT_FOUND");
        prov.EstadoCodigo = estado;
        prov.UpdatedAt = DateTime.UtcNow; prov.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, accion, prov.Codigo, "Proveedor", prov.Id);
        return Result.Ok();
    }

    // ── Facturas de compra (CxP) ──────────────────────────────────────────────

    public async Task<Result<PagedResult<FacturaCompraDto>>> ListFacturasAsync(int empresaId, int? proveedorId, bool soloPendientes, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.FacturasCompra.AsNoTracking().Where(f => f.EmpresaId == empresaId);
        if (proveedorId is int pid) q = q.Where(f => f.ProveedorId == pid);
        if (soloPendientes) q = q.Where(f => f.EstadoCodigo == FacturaCompraEstados.Pendiente || f.EstadoCodigo == FacturaCompraEstados.Parcial);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(f => f.NumeroDocumento.Contains(s) || f.Proveedor.Nombre.Contains(s));
        }
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = await q.OrderByDescending(f => f.FechaEmision).ThenByDescending(f => f.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new
            {
                f.Id, f.ProveedorId, ProveedorNombre = f.Proveedor.Nombre, f.NumeroDocumento, f.TipoDocumento,
                f.FechaEmision, f.FechaVencimiento, f.CondicionPago, f.Total, f.EstadoCodigo,
                Pagado = f.Pagos.Where(p => p.EstadoCodigo == PagoProveedorEstados.Confirmado).Sum(p => (decimal?)p.Monto) ?? 0m,
            }).ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = rows.Select(f => ToFacturaDto(f.Id, f.ProveedorId, f.ProveedorNombre, f.NumeroDocumento, f.TipoDocumento,
            f.FechaEmision, f.FechaVencimiento, f.CondicionPago, f.Total, f.Pagado, f.EstadoCodigo, hoy)).ToList();
        return Result<PagedResult<FacturaCompraDto>>.Ok(PagedResult<FacturaCompraDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<FacturaCompraDetalleDto>> GetFacturaAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var f = await _db.FacturasCompra.AsNoTracking().Include(x => x.Pagos).Include(x => x.Proveedor)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (f is null) return Result<FacturaCompraDetalleDto>.Fail("Factura no encontrada.", "FACTURA_COMPRA_NOT_FOUND");
        return Result<FacturaCompraDetalleDto>.Ok(ToFacturaDetalle(f));
    }

    public async Task<Result<FacturaCompraDetalleDto>> CrearFacturaAsync(int empresaId, CrearFacturaCompraRequest request, string? actor, CancellationToken ct = default)
    {
        if (!TiposDocumentoCompra.All.Contains(request.TipoDocumento)) return Result<FacturaCompraDetalleDto>.Fail("Tipo de documento inválido.", "VALIDATION");
        var condicion = CondicionesPagoCompra.All.Contains(request.CondicionPago) ? request.CondicionPago : CondicionesPagoCompra.Credito;
        if (request.Subtotal < 0 || request.Iva < 0) return Result<FacturaCompraDetalleDto>.Fail("Montos inválidos.", "VALIDATION");

        var prov = await _db.Proveedores.FirstOrDefaultAsync(p => p.Id == request.ProveedorId && p.EmpresaId == empresaId, ct);
        if (prov is null) return Result<FacturaCompraDetalleDto>.Fail("Proveedor no encontrado.", "PROVEEDOR_NOT_FOUND");

        var numero = request.NumeroDocumento.Trim();
        var dup = await _db.FacturasCompra.AnyAsync(f => f.EmpresaId == empresaId && f.ProveedorId == prov.Id
            && f.NumeroDocumento == numero && f.EstadoCodigo != FacturaCompraEstados.Anulada, ct);
        if (dup) return Result<FacturaCompraDetalleDto>.Fail("Ya existe una factura con ese número para el proveedor.", "DUPLICATE");

        var emision = request.FechaEmision ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var vencimiento = request.FechaVencimiento
            ?? (condicion == CondicionesPagoCompra.Contado ? emision : CuentasPagarCalculator.Vencimiento(emision, prov.PlazoDiasDefault));
        var total = decimal.Round(request.Subtotal + request.Iva, 2, MidpointRounding.AwayFromZero);

        var factura = new FacturaCompra
        {
            EmpresaId = empresaId, ProveedorId = prov.Id, NumeroDocumento = numero, TipoDocumento = request.TipoDocumento,
            FechaEmision = emision, FechaVencimiento = vencimiento, CondicionPago = condicion,
            Subtotal = decimal.Round(request.Subtotal, 2, MidpointRounding.AwayFromZero),
            Iva = decimal.Round(request.Iva, 2, MidpointRounding.AwayFromZero), Total = total,
            IvaDeducible = request.IvaDeducible, Descripcion = request.Descripcion?.Trim(),
            EstadoCodigo = FacturaCompraEstados.Pendiente, CreatedBy = actor,
        };

        if (request.RegistrarGastoProfit && total > 0)
        {
            var gasto = await _profit.CreateGastoAsync(empresaId, new CreateProfitGastoRequest
            {
                Fecha = emision, Categoria = "COMPRA",
                Descripcion = $"Compra {factura.TipoDocumento} {factura.NumeroDocumento}".Trim(),
                Proveedor = prov.Nombre, Monto = factura.Subtotal, IvaMonto = factura.Iva, IvaDeducible = factura.IvaDeducible,
            }, actor, ct);
            if (gasto.IsSuccess) factura.ProfitGastoId = gasto.Value!.Id;
        }

        _db.FacturasCompra.Add(factura);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_FACTURA_COMPRA", $"{prov.Nombre} · {factura.NumeroDocumento} · {factura.Total:N2}", "FacturaCompra", factura.Id);

        factura.Proveedor = prov;
        return Result<FacturaCompraDetalleDto>.Ok(ToFacturaDetalle(factura));
    }

    public async Task<Result> AnularFacturaAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var f = await _db.FacturasCompra.Include(x => x.Pagos).FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (f is null) return Result.Fail("Factura no encontrada.", "FACTURA_COMPRA_NOT_FOUND");
        if (f.EstadoCodigo == FacturaCompraEstados.Anulada) return Result.Fail("La factura ya está anulada.", "INVALID_STATE");
        if (f.Pagos.Any(p => p.EstadoCodigo == PagoProveedorEstados.Confirmado))
            return Result.Fail("Anula primero los pagos confirmados de la factura.", "INVALID_STATE");

        if (f.ProfitGastoId is int gastoId)
            await _profit.InactivarGastoAsync(empresaId, gastoId, actor, ct); // best-effort: revierte el gasto

        f.EstadoCodigo = FacturaCompraEstados.Anulada;
        f.ProfitGastoId = null;
        f.UpdatedAt = DateTime.UtcNow; f.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ANULAR_FACTURA_COMPRA", f.NumeroDocumento, "FacturaCompra", f.Id);
        return Result.Ok();
    }

    // ── Pagos ─────────────────────────────────────────────────────────────────

    public async Task<Result<PagoProveedorDto>> RegistrarPagoAsync(int empresaId, RegistrarPagoProveedorRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Monto <= 0) return Result<PagoProveedorDto>.Fail("El monto debe ser mayor que cero.", "VALIDATION");

        var f = await _db.FacturasCompra.Include(x => x.Pagos).Include(x => x.Proveedor)
            .FirstOrDefaultAsync(x => x.Id == request.FacturaCompraId && x.EmpresaId == empresaId, ct);
        if (f is null) return Result<PagoProveedorDto>.Fail("Factura no encontrada.", "FACTURA_COMPRA_NOT_FOUND");
        if (f.EstadoCodigo == FacturaCompraEstados.Anulada) return Result<PagoProveedorDto>.Fail("La factura está anulada.", "INVALID_STATE");

        var pagado = f.Pagos.Where(p => p.EstadoCodigo == PagoProveedorEstados.Confirmado).Sum(p => p.Monto);
        var saldo = CuentasPagarCalculator.Saldo(f.Total, pagado);
        var monto = decimal.Round(request.Monto, 2, MidpointRounding.AwayFromZero);
        if (monto > saldo) return Result<PagoProveedorDto>.Fail($"El monto excede el saldo pendiente ({saldo:N2}).", "VALIDATION");

        var fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var pago = new PagoProveedor
        {
            EmpresaId = empresaId, FacturaCompraId = f.Id, Fecha = fecha, Monto = monto,
            FormaPagoCodigo = request.FormaPagoCodigo, Referencia = request.Referencia?.Trim(), Nota = request.Nota?.Trim(),
            EstadoCodigo = PagoProveedorEstados.Confirmado, CreatedBy = actor,
        };

        // Egreso en tesorería (opcional).
        if (request.CuentaTesoreriaId is int cuentaId)
        {
            var mov = await _tesoreria.RegistrarMovimientoAsync(empresaId, new RegistrarMovimientoRequest
            {
                CuentaId = cuentaId, Fecha = fecha, Tipo = TiposMovimientoTesoreria.Egreso, Monto = monto,
                Concepto = $"Pago compra {f.NumeroDocumento} · {f.Proveedor.Nombre}",
                Referencia = pago.Referencia, Origen = OrigenesMovimientoTesoreria.Compra, OrigenId = f.Id,
            }, actor, ct);
            if (mov.IsFailure) return Result<PagoProveedorDto>.Fail(mov.Error ?? "No se pudo registrar el egreso.", mov.ErrorCode);
            pago.MovimientoTesoreriaId = mov.Value!.Id;
        }

        _db.PagosProveedor.Add(pago);
        f.EstadoCodigo = CuentasPagarCalculator.Estado(f.Total, pagado + monto);
        f.UpdatedAt = DateTime.UtcNow; f.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "REGISTRAR_PAGO_COMPRA", $"{f.NumeroDocumento} · {monto:N2}", "PagoProveedor", pago.Id);
        return Result<PagoProveedorDto>.Ok(ToPagoDto(pago));
    }

    public async Task<Result> AnularPagoAsync(int empresaId, int pagoId, string? actor, CancellationToken ct = default)
    {
        var pago = await _db.PagosProveedor.FirstOrDefaultAsync(p => p.Id == pagoId && p.EmpresaId == empresaId, ct);
        if (pago is null) return Result.Fail("Pago no encontrado.", "PAGO_PROVEEDOR_NOT_FOUND");
        if (pago.EstadoCodigo == PagoProveedorEstados.Anulado) return Result.Fail("El pago ya está anulado.", "INVALID_STATE");

        if (pago.MovimientoTesoreriaId is int movId)
            await _tesoreria.AnularMovimientoAsync(empresaId, movId, actor, ct); // best-effort: revierte el egreso

        pago.EstadoCodigo = PagoProveedorEstados.Anulado;
        pago.UpdatedAt = DateTime.UtcNow; pago.UpdatedBy = actor;

        var f = await _db.FacturasCompra.Include(x => x.Pagos).FirstOrDefaultAsync(x => x.Id == pago.FacturaCompraId && x.EmpresaId == empresaId, ct);
        if (f is not null && f.EstadoCodigo != FacturaCompraEstados.Anulada)
        {
            var pagado = f.Pagos.Where(p => p.EstadoCodigo == PagoProveedorEstados.Confirmado && p.Id != pago.Id).Sum(p => p.Monto);
            f.EstadoCodigo = CuentasPagarCalculator.Estado(f.Total, pagado);
            f.UpdatedAt = DateTime.UtcNow; f.UpdatedBy = actor;
        }
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ANULAR_PAGO_COMPRA", $"Pago #{pago.Id} · {pago.Monto:N2}", "PagoProveedor", pago.Id);
        return Result.Ok();
    }

    public async Task<Result<ComprasResumenDto>> ResumenAsync(int empresaId, CancellationToken ct = default)
    {
        var facturas = await _db.FacturasCompra.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId && f.EstadoCodigo != FacturaCompraEstados.Anulada)
            .Select(f => new
            {
                f.Total, f.FechaVencimiento,
                Pagado = f.Pagos.Where(p => p.EstadoCodigo == PagoProveedorEstados.Confirmado).Sum(p => (decimal?)p.Monto) ?? 0m,
            }).ToListAsync(ct);
        var proveedores = await _db.Proveedores.AsNoTracking().CountAsync(p => p.EmpresaId == empresaId && p.EstadoCodigo == ProveedorEstados.Activo, ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        decimal porPagar = 0, vencido = 0; int pendientes = 0;
        foreach (var f in facturas)
        {
            var saldo = CuentasPagarCalculator.Saldo(f.Total, f.Pagado);
            if (saldo <= 0) continue;
            porPagar += saldo;
            pendientes++;
            if (CuentasPagarCalculator.EstaVencida(saldo, f.FechaVencimiento, hoy)) vencido += saldo;
        }
        return Result<ComprasResumenDto>.Ok(new ComprasResumenDto
        {
            TotalPorPagar = porPagar, TotalVencido = vencido, FacturasPendientes = pendientes, Proveedores = proveedores,
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ProveedorDto ToProveedorDto(Proveedor p) => new()
    {
        Id = p.Id, Codigo = p.Codigo, Nombre = p.Nombre, Nit = p.Nit, Nrc = p.Nrc,
        Contacto = p.Contacto, Telefono = p.Telefono, Email = p.Email,
        PlazoDiasDefault = p.PlazoDiasDefault, EstadoCodigo = p.EstadoCodigo,
    };

    private static FacturaCompraDto ToFacturaDto(int id, int provId, string provNombre, string numero, string tipo,
        DateOnly emision, DateOnly vencimiento, string condicion, decimal total, decimal pagado, string estado, DateOnly hoy)
    {
        var saldo = CuentasPagarCalculator.Saldo(total, pagado);
        var vencida = CuentasPagarCalculator.EstaVencida(saldo, vencimiento, hoy);
        return new FacturaCompraDto
        {
            Id = id, ProveedorId = provId, ProveedorNombre = provNombre, NumeroDocumento = numero, TipoDocumento = tipo,
            FechaEmision = emision, FechaVencimiento = vencimiento, CondicionPago = condicion,
            Total = total, Pagado = pagado, Saldo = saldo, EstadoCodigo = estado,
            Vencida = vencida, DiasVencido = vencida ? CuentasPagarCalculator.DiasVencido(vencimiento, hoy) : 0,
        };
    }

    private static FacturaCompraDetalleDto ToFacturaDetalle(FacturaCompra f)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var pagado = f.Pagos.Where(p => p.EstadoCodigo == PagoProveedorEstados.Confirmado).Sum(p => p.Monto);
        var saldo = CuentasPagarCalculator.Saldo(f.Total, pagado);
        var vencida = CuentasPagarCalculator.EstaVencida(saldo, f.FechaVencimiento, hoy);
        return new FacturaCompraDetalleDto
        {
            Id = f.Id, ProveedorId = f.ProveedorId, ProveedorNombre = f.Proveedor?.Nombre ?? "",
            NumeroDocumento = f.NumeroDocumento, TipoDocumento = f.TipoDocumento,
            FechaEmision = f.FechaEmision, FechaVencimiento = f.FechaVencimiento, CondicionPago = f.CondicionPago,
            Subtotal = f.Subtotal, Iva = f.Iva, Total = f.Total, IvaDeducible = f.IvaDeducible, Descripcion = f.Descripcion,
            Pagado = pagado, Saldo = saldo, EstadoCodigo = f.EstadoCodigo, ProfitGastoId = f.ProfitGastoId,
            Vencida = vencida, DiasVencido = vencida ? CuentasPagarCalculator.DiasVencido(f.FechaVencimiento, hoy) : 0,
            Pagos = f.Pagos.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).Select(ToPagoDto).ToList(),
        };
    }

    private static PagoProveedorDto ToPagoDto(PagoProveedor p) => new()
    {
        Id = p.Id, FacturaCompraId = p.FacturaCompraId, Fecha = p.Fecha, Monto = p.Monto,
        FormaPagoCodigo = p.FormaPagoCodigo, Referencia = p.Referencia, Nota = p.Nota,
        MovimientoTesoreriaId = p.MovimientoTesoreriaId, EstadoCodigo = p.EstadoCodigo,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, string entidad, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = entidad, EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
