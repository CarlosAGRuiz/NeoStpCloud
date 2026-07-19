using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Inventario;
using NeoSTP.Application.Inventario.Dtos;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Domain.Core.Inventario;
using NeoSTP.Domain.Core.Pos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NEOPOS (Sprint 1). Registra ventas POS (tickets de cobro), calcula totales con
/// <see cref="PosCalculator"/>, arma el ticket (PDF/impresión) y lo envía por correo.
/// Aislado por EmpresaId. La promoción a DTE queda preparada vía VentaPos.DteDocumentoId.
/// </summary>
public class PosService : IPosService
{
    private const string AuditModule = "NEOPOS";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ITicketPdfService _ticketPdf;
    private readonly ITenantEmailSender _email;
    private readonly IConnectDteService _dte;
    private readonly IInventarioService _inventario;
    private readonly PosOptions _opts;

    public PosService(NeoStpDbContext db, IAuditoriaService auditoria, ITicketPdfService ticketPdf, ITenantEmailSender email, IConnectDteService dte, IInventarioService inventario, IOptions<PosOptions> opts)
    {
        _db = db;
        _auditoria = auditoria;
        _ticketPdf = ticketPdf;
        _email = email;
        _dte = dte;
        _inventario = inventario;
        _opts = opts.Value;
    }

    public async Task<Result<PagedResult<VentaPosDto>>> ListAsync(int empresaId, DateOnly? desde, DateOnly? hasta, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.VentasPos.AsNoTracking().Where(v => v.EmpresaId == empresaId);
        if (desde is DateOnly d) { var dt = d.ToDateTime(TimeOnly.MinValue); q = q.Where(v => v.Fecha >= dt); }
        if (hasta is DateOnly h) { var ht = h.ToDateTime(TimeOnly.MaxValue); q = q.Where(v => v.Fecha <= ht); }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(v => v.Numero.Contains(s) || v.ClienteNombre.Contains(s));
        }
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new VentaPosDto
            {
                Id = v.Id, Numero = v.Numero, Fecha = v.Fecha, ClienteNombre = v.ClienteNombre,
                FormaPagoCodigo = v.FormaPagoCodigo, Total = v.Total, EstadoCodigo = v.EstadoCodigo,
                EstadoFacturacion = v.EstadoFacturacion, DteDocumentoId = v.DteDocumentoId, Items = v.Lineas.Count,
            }).ToListAsync(ct);
        return Result<PagedResult<VentaPosDto>>.Ok(PagedResult<VentaPosDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<VentaPosDetalleDto>> GetAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var v = await _db.VentasPos.AsNoTracking().Include(x => x.Lineas)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return v is null
            ? Result<VentaPosDetalleDto>.Fail("Venta no encontrada.", "VENTA_POS_NOT_FOUND")
            : Result<VentaPosDetalleDto>.Ok(ToDetalle(v));
    }

    public async Task<Result<VentaPosDetalleDto>> CrearVentaAsync(int empresaId, CrearVentaRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Lineas is null || request.Lineas.Count == 0)
            return Result<VentaPosDetalleDto>.Fail("Agrega al menos un producto.", "VALIDATION");
        if (!FormasPagoPos.All.Contains(request.FormaPagoCodigo))
            return Result<VentaPosDetalleDto>.Fail("Forma de pago inválida.", "VALIDATION");

        // Resolver productos referenciados.
        var idsProd = request.Lineas.Where(l => l.ProductoId is int).Select(l => l.ProductoId!.Value).Distinct().ToList();
        var productos = new Dictionary<int, (string codigo, string nombre, decimal precio, bool iva, bool bien)>();
        if (idsProd.Count > 0)
        {
            var filas = await _db.Productos.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId && idsProd.Contains(p.Id))
                .Select(p => new { p.Id, p.CodigoInterno, p.Nombre, p.PrecioUnitario, p.AplicaIva, p.TipoItem })
                .ToListAsync(ct);
            foreach (var p in filas)
                productos[p.Id] = (p.CodigoInterno, p.Nombre, p.PrecioUnitario, p.AplicaIva, p.TipoItem != "SERVICIO");
        }

        var venta = new VentaPos
        {
            EmpresaId = empresaId, SucursalId = request.SucursalId, PuntoVentaId = request.PuntoVentaId,
            Fecha = DateTime.UtcNow, ClienteId = request.ClienteId,
            ClienteNombre = string.IsNullOrWhiteSpace(request.ClienteNombre) ? "Consumidor final" : request.ClienteNombre.Trim(),
            FormaPagoCodigo = request.FormaPagoCodigo, Nota = request.Nota?.Trim(),
            EstadoCodigo = VentaPosEstados.Completada, CreatedBy = actor,
        };

        var inputs = new List<PosCalculator.LineaInput>();
        foreach (var l in request.Lineas)
        {
            decimal precio; bool aplicaIva; string codigo, descripcion;
            if (l.ProductoId is int pid && productos.TryGetValue(pid, out var p))
            {
                precio = l.PrecioUnitario ?? p.precio;
                aplicaIva = l.AplicaIva ?? p.iva;
                codigo = l.Codigo ?? p.codigo;
                descripcion = l.Descripcion ?? p.nombre;
            }
            else
            {
                if (l.PrecioUnitario is not decimal pr) return Result<VentaPosDetalleDto>.Fail("Cada línea necesita producto o precio.", "VALIDATION");
                precio = pr; aplicaIva = l.AplicaIva ?? true;
                codigo = l.Codigo ?? "—";
                descripcion = string.IsNullOrWhiteSpace(l.Descripcion) ? "Ítem" : l.Descripcion!.Trim();
            }
            if (l.Cantidad <= 0) return Result<VentaPosDetalleDto>.Fail("Cantidad inválida.", "VALIDATION");

            var calc = PosCalculator.CalcularLinea(new PosCalculator.LineaInput(l.Cantidad, precio, l.Descuento, aplicaIva), _opts.IvaTasa);
            venta.Lineas.Add(new VentaPosLinea
            {
                ProductoId = l.ProductoId, Codigo = codigo, Descripcion = descripcion,
                Cantidad = l.Cantidad, PrecioUnitario = precio, Descuento = l.Descuento, AplicaIva = aplicaIva,
                IvaLinea = calc.IvaLinea, Total = calc.Total, CreatedBy = actor,
            });
            inputs.Add(new PosCalculator.LineaInput(l.Cantidad, precio, l.Descuento, aplicaIva));
        }

        var totales = PosCalculator.CalcularVenta(inputs, _opts.IvaTasa);
        venta.Subtotal = totales.Subtotal; venta.IvaTotal = totales.IvaTotal;
        venta.TotalDescuento = totales.TotalDescuento; venta.Total = totales.Total;
        venta.EfectivoRecibido = request.EfectivoRecibido;
        venta.Cambio = PosCalculator.Cambio(totales.Total, request.EfectivoRecibido);

        // Liga la venta a la caja abierta (si hay una), para el corte de caja.
        venta.SesionCajaId = await _db.SesionesCaja
            .Where(s => s.EmpresaId == empresaId && s.EstadoCodigo == SesionCajaEstados.Abierta)
            .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);

        venta.Numero = await SiguienteNumeroAsync(empresaId, ct);
        _db.VentasPos.Add(venta);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_VENTA", $"{venta.Numero} · {venta.Total:N2} · {venta.Lineas.Count} ítems", venta.Id);

        // Descuento de inventario (best-effort, solo BIEN). No bloquea la venta si falta stock.
        foreach (var l in venta.Lineas)
        {
            if (l.ProductoId is int pid && productos.TryGetValue(pid, out var p) && p.bien)
                await _inventario.RegistrarSalidaAsync(empresaId, new RegistrarMovimientoInventarioRequest
                {
                    ProductoId = pid, Cantidad = l.Cantidad, SucursalId = venta.SucursalId,
                    Origen = OrigenesMovimientoInventario.Venta, OrigenId = venta.Id, Referencia = venta.Numero,
                }, actor, ct);
        }

        return Result<VentaPosDetalleDto>.Ok(ToDetalle(venta));
    }

    public async Task<Result> AnularAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var v = await _db.VentasPos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (v is null) return Result.Fail("Venta no encontrada.", "VENTA_POS_NOT_FOUND");
        if (v.EstadoCodigo == VentaPosEstados.Anulada) return Result.Fail("La venta ya está anulada.", "INVALID_STATE");
        if (v.EstadoFacturacion == VentaPosFacturacion.Facturada)
            return Result.Fail("La venta ya fue facturada (DTE). Anula el DTE correspondiente.", "INVALID_STATE");

        v.EstadoCodigo = VentaPosEstados.Anulada;
        v.UpdatedAt = DateTime.UtcNow; v.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ANULAR_VENTA", v.Numero, v.Id);

        // Reingreso de inventario por las salidas que generó esta venta (best-effort), al mismo costo.
        var salidas = await _db.MovimientosInventario.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Origen == OrigenesMovimientoInventario.Venta
                && m.OrigenId == v.Id && m.Tipo == TiposMovimientoInventario.Salida)
            .Select(m => new { m.ProductoId, m.Cantidad, m.CostoUnitario, m.SucursalId }).ToListAsync(ct);
        foreach (var m in salidas)
            await _inventario.RegistrarEntradaAsync(empresaId, new RegistrarMovimientoInventarioRequest
            {
                ProductoId = m.ProductoId, Cantidad = m.Cantidad, CostoUnitario = m.CostoUnitario,
                SucursalId = m.SucursalId,
                Origen = OrigenesMovimientoInventario.Devolucion, OrigenId = v.Id, Referencia = v.Numero,
            }, actor, ct);

        return Result.Ok();
    }

    public async Task<Result<TicketModel>> GetTicketAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var v = await _db.VentasPos.AsNoTracking().Include(x => x.Lineas)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (v is null) return Result<TicketModel>.Fail("Venta no encontrada.", "VENTA_POS_NOT_FOUND");

        var emp = await _db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new { e.RazonSocial, e.NombreComercial, e.Nit, e.Nrc, e.Direccion, e.Telefono, e.LogoBlob })
            .FirstOrDefaultAsync(ct);

        return Result<TicketModel>.Ok(new TicketModel
        {
            EmpresaNombre = emp?.NombreComercial ?? emp?.RazonSocial ?? "",
            EmpresaNit = emp?.Nit, EmpresaNrc = emp?.Nrc, Direccion = emp?.Direccion, Telefono = emp?.Telefono,
            LogoPng = emp?.LogoBlob,
            Numero = v.Numero, Fecha = v.Fecha, ClienteNombre = v.ClienteNombre, FormaPago = v.FormaPagoCodigo,
            EfectivoRecibido = v.EfectivoRecibido, Cambio = v.Cambio, EstadoCodigo = v.EstadoCodigo, Nota = v.Nota,
            Subtotal = v.Subtotal, IvaTotal = v.IvaTotal, TotalDescuento = v.TotalDescuento, Total = v.Total,
            AnchoMm = _opts.AnchoTicketMm, MonedaSimbolo = _opts.MonedaSimbolo, PieTicket = _opts.PieTicket,
            Lineas = v.Lineas.Select(l => new TicketLinea
            {
                Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Total = l.Total,
            }).ToList(),
        });
    }

    public async Task<Result> EnviarTicketCorreoAsync(int empresaId, int id, string email, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return Result.Fail("Correo requerido.", "VALIDATION");
        var ticket = await GetTicketAsync(empresaId, id, ct);
        if (ticket.IsFailure) return Result.Fail(ticket.Error ?? "Venta no encontrada.", ticket.ErrorCode);

        var t = ticket.Value!;
        var pdf = _ticketPdf.GenerarTicket(t);
        var msg = new EmailMessage
        {
            To = email.Trim(),
            Subject = $"Ticket {t.Numero} · {t.EmpresaNombre}",
            HtmlBody = $"<p>Adjunto tu comprobante de compra <strong>{t.Numero}</strong> por <strong>{t.MonedaSimbolo}{t.Total:N2}</strong>.</p><p>{t.PieTicket}</p>",
            TextBody = $"Ticket {t.Numero} por {t.MonedaSimbolo}{t.Total:N2}. {t.PieTicket}",
            Attachments = { new EmailAttachment { FileName = $"ticket_{t.Numero}.pdf", MediaType = "application/pdf", Content = pdf } },
        };
        var res = await _email.EnviarAsync(empresaId, msg, ct);
        if (!res.Success) return Result.Fail(res.Detalle ?? res.Mensaje ?? "No se pudo enviar el correo.", "EMAIL_FAILED");
        await Audit(empresaId, actor, "ENVIAR_TICKET", $"{t.Numero} → {email}", id);
        return Result.Ok();
    }

    public async Task<Result<PosResumenDiaDto>> ResumenDiaAsync(int empresaId, DateOnly fecha, CancellationToken ct = default)
    {
        var ini = fecha.ToDateTime(TimeOnly.MinValue);
        var fin = fecha.ToDateTime(TimeOnly.MaxValue);
        var ventas = await _db.VentasPos.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.EstadoCodigo == VentaPosEstados.Completada && v.Fecha >= ini && v.Fecha <= fin)
            .Select(v => new { v.Total, v.FormaPagoCodigo }).ToListAsync(ct);

        return Result<PosResumenDiaDto>.Ok(new PosResumenDiaDto
        {
            Fecha = fecha,
            Ventas = ventas.Count,
            Total = ventas.Sum(v => v.Total),
            Efectivo = ventas.Where(v => v.FormaPagoCodigo == FormasPagoPos.Efectivo).Sum(v => v.Total),
            Tarjeta = ventas.Where(v => v.FormaPagoCodigo == FormasPagoPos.Tarjeta).Sum(v => v.Total),
            Otros = ventas.Where(v => v.FormaPagoCodigo != FormasPagoPos.Efectivo && v.FormaPagoCodigo != FormasPagoPos.Tarjeta).Sum(v => v.Total),
        });
    }

    public async Task<Result<VentaPosDetalleDto>> PromoverADteAsync(int empresaId, int ventaId, PromoverVentaRequest request, string? actor, CancellationToken ct = default)
    {
        var v = await _db.VentasPos.Include(x => x.Lineas).FirstOrDefaultAsync(x => x.Id == ventaId && x.EmpresaId == empresaId, ct);
        if (v is null) return Result<VentaPosDetalleDto>.Fail("Venta no encontrada.", "VENTA_POS_NOT_FOUND");
        if (v.EstadoCodigo == VentaPosEstados.Anulada) return Result<VentaPosDetalleDto>.Fail("La venta está anulada.", "INVALID_STATE");
        if (v.EstadoFacturacion == VentaPosFacturacion.Facturada)
            return Result<VentaPosDetalleDto>.Fail("La venta ya fue facturada.", "INVALID_STATE");
        if (v.Lineas.Count == 0) return Result<VentaPosDetalleDto>.Fail("La venta no tiene líneas.", "VALIDATION");

        var tipo = request.TipoDteCodigo is "01" or "03" ? request.TipoDteCodigo : "01";
        var clienteId = request.ClienteId ?? v.ClienteId;
        if (tipo == "03" && clienteId is null)
            return Result<VentaPosDetalleDto>.Fail("El CCF (03) requiere un cliente con NRC.", "VALIDATION");

        // Los precios POS son IVA incluido → mapean directo a FC (01); para CCF (03) el
        // pipeline DTE reconoce la clasificación GRAVADA y deriva el IVA contenido igual (gravada*0.13/1.13).
        var dteReq = new CreateDteDocumentoRequest
        {
            TipoDteCodigo = tipo,
            SucursalId = v.SucursalId,
            PuntoVentaId = v.PuntoVentaId,
            ClienteId = clienteId,
            CondicionOperacionCodigo = "1", // contado
            FormaPagoCodigo = MapFormaPago(v.FormaPagoCodigo),
            TipoMonedaCodigo = "USD",
            Observaciones = $"Generado desde venta POS {v.Numero}",
            Lineas = v.Lineas.Select(l => new CreateDteDocumentoLineaRequest
            {
                ProductoId = l.ProductoId,
                Codigo = l.Codigo,
                Descripcion = l.Descripcion,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario,
                MontoDescuento = l.Descuento,
                Clasificacion = l.AplicaIva ? "GRAVADA" : "EXENTA",
            }).ToList(),
        };

        var emision = await _dte.EmitirAsync(empresaId, dteReq, actor, ct);
        if (emision.IsFailure)
            return Result<VentaPosDetalleDto>.Fail(emision.Error ?? "No se pudo emitir el DTE.", emision.ErrorCode);

        v.DteDocumentoId = emision.Value!.Id;
        v.EstadoFacturacion = VentaPosFacturacion.Facturada;
        v.UpdatedAt = DateTime.UtcNow; v.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "PROMOVER_DTE", $"{v.Numero} → DTE #{emision.Value.Id} ({tipo})", v.Id);
        return Result<VentaPosDetalleDto>.Ok(ToDetalle(v));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Mapea la forma de pago POS al catálogo MH CAT-017 (aprox.).</summary>
    private static string MapFormaPago(string formaPos) => formaPos switch
    {
        FormasPagoPos.Efectivo => "01",
        FormasPagoPos.Tarjeta => "02",
        FormasPagoPos.Transferencia => "03",
        FormasPagoPos.Qr => "03",
        _ => "99",
    };

    private async Task<string> SiguienteNumeroAsync(int empresaId, CancellationToken ct)
    {
        var count = await _db.VentasPos.CountAsync(v => v.EmpresaId == empresaId, ct);
        return $"POS-{count + 1:000000}";
    }

    private static VentaPosDetalleDto ToDetalle(VentaPos v) => new()
    {
        Id = v.Id, Numero = v.Numero, Fecha = v.Fecha, ClienteNombre = v.ClienteNombre,
        FormaPagoCodigo = v.FormaPagoCodigo, Total = v.Total, EstadoCodigo = v.EstadoCodigo,
        EstadoFacturacion = v.EstadoFacturacion, DteDocumentoId = v.DteDocumentoId, Items = v.Lineas.Count,
        SucursalId = v.SucursalId, PuntoVentaId = v.PuntoVentaId, ClienteId = v.ClienteId,
        Subtotal = v.Subtotal, IvaTotal = v.IvaTotal, TotalDescuento = v.TotalDescuento,
        EfectivoRecibido = v.EfectivoRecibido, Cambio = v.Cambio, Nota = v.Nota,
        Lineas = v.Lineas.Select(l => new VentaPosLineaDto
        {
            Id = l.Id, ProductoId = l.ProductoId, Codigo = l.Codigo, Descripcion = l.Descripcion,
            Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Descuento = l.Descuento,
            AplicaIva = l.AplicaIva, IvaLinea = l.IvaLinea, Total = l.Total,
        }).ToList(),
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "VentaPos", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
