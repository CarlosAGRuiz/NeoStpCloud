using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Productos.Dtos;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Tesoreria;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOCOMPRAS — facturas de compra y cuentas por pagar. Permisos Compras.Ver / Compras.Gestionar.</summary>
[Authorize]
[RequireModulo("COMPRAS")]
public class ComprasController : Controller
{
    public static readonly string[] TiposDocumento = TiposDocumentoCompra.All;
    public static readonly string[] CondicionesPago = CondicionesPagoCompra.All;
    public static readonly string[] FormasPago = ["EFECTIVO", "TRANSFERENCIA", "CHEQUE", "TARJETA", "QR", "OTRO"];

    private readonly ICompraService _compras;
    private readonly IOrdenCompraService _ordenes;
    private readonly ITesoreriaService _tesoreria;
    private readonly IProductosService _productos;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;
    private readonly NeoSTP.Infrastructure.Persistence.NeoStpDbContext _db;

    public ComprasController(
        ICompraService compras,
        IOrdenCompraService ordenes,
        ITesoreriaService tesoreria,
        IProductosService productos,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext,
        NeoSTP.Infrastructure.Persistence.NeoStpDbContext db)
    {
        _compras = compras;
        _ordenes = ordenes;
        _tesoreria = tesoreria;
        _productos = productos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Ordenes(
        string? estado = null, int? proveedorId = null, string? search = null, int page = 1,
        CancellationToken ct = default)
    {
        if (!Has("Compras.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _ordenes.ListAsync(eid, estado, proveedorId,
            new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            result = await _ordenes.ListAsync(eid, null, proveedorId,
                new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        }
        ViewBag.Search = search;
        ViewBag.Estado = estado;
        ViewBag.ProveedorId = proveedorId;
        ViewBag.Estados = OrdenCompraEstados.All;
        ViewBag.PuedeGestionar = Has("Compras.Gestionar");
        ViewBag.PuedeAprobar = Has("Compras.Aprobar");
        ViewBag.UmbralAprobacion = await _db.Empresas.AsNoTracking()
            .Where(e => e.Id == eid).Select(e => e.UmbralAprobacionCompras).FirstOrDefaultAsync(ct);
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> CrearOrden(int? proveedorId, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        await CargarCatalogosOrdenAsync(eid, ct);
        ViewBag.OrdenId = 0;
        return View(new GuardarOrdenCompraRequest
        {
            ProveedorId = proveedorId ?? 0,
            Fecha = DateOnly.FromDateTime(DateTime.UtcNow),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearOrden(GuardarOrdenCompraRequest model, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        if (!ModelState.IsValid)
        {
            await CargarCatalogosOrdenAsync(eid, ct);
            ViewBag.OrdenId = 0;
            return View(model);
        }

        var result = await _ordenes.CrearAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "No se pudo crear la orden.");
            await CargarCatalogosOrdenAsync(eid, ct);
            ViewBag.OrdenId = 0;
            return View(model);
        }
        TempData["Success"] = "Orden de compra creada en borrador.";
        return RedirectToAction(nameof(DetalleOrden), new { id = result.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> EditarOrden(int id, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _ordenes.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        if (result.Value!.EstadoCodigo != OrdenCompraEstados.Borrador)
        {
            TempData["Error"] = "Solo una orden en borrador puede editarse.";
            return RedirectToAction(nameof(DetalleOrden), new { id });
        }

        await CargarCatalogosOrdenAsync(eid, ct);
        ViewBag.OrdenId = id;
        return View("CrearOrden", new GuardarOrdenCompraRequest
        {
            ProveedorId = result.Value.ProveedorId,
            Fecha = result.Value.Fecha,
            FechaEntregaEsperada = result.Value.FechaEntregaEsperada,
            Observaciones = result.Value.Observaciones,
            Lineas = result.Value.Detalle.Select(x => new GuardarOrdenCompraLineaRequest
            {
                ProductoId = x.ProductoId,
                Cantidad = x.Cantidad,
                PrecioUnitario = x.PrecioUnitario,
                AplicaIva = x.AplicaIva,
            }).ToList(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarOrden(int id, GuardarOrdenCompraRequest model, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        if (!ModelState.IsValid)
        {
            await CargarCatalogosOrdenAsync(eid, ct);
            ViewBag.OrdenId = id;
            return View("CrearOrden", model);
        }

        var result = await _ordenes.ActualizarAsync(eid, id, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "No se pudo actualizar la orden.");
            await CargarCatalogosOrdenAsync(eid, ct);
            ViewBag.OrdenId = id;
            return View("CrearOrden", model);
        }
        TempData["Success"] = "Orden de compra actualizada.";
        return RedirectToAction(nameof(DetalleOrden), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> DetalleOrden(int id, CancellationToken ct)
    {
        if (!Has("Compras.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _ordenes.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.PuedeGestionar = Has("Compras.Gestionar");
        ViewBag.PuedeAprobar = Has("Compras.Aprobar");
        ViewBag.TiposDocumento = TiposDocumento;
        ViewBag.CondicionesPago = CondicionesPago;
        ViewBag.IdempotencyKey = Guid.NewGuid().ToString("N");
        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmitirOrden(int id, CancellationToken ct)
        => await EjecutarAccionOrden(id, "Orden emitida.",
            (eid, actor, token) => _ordenes.EmitirAsync(eid, id, actor, token), ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarOrden(int id, CancellationToken ct)
        => await EjecutarAccionOrden(id, "Orden cancelada.",
            (eid, actor, token) => _ordenes.CancelarAsync(eid, id, actor, token), ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarOrden(int id, CancellationToken ct)
    {
        if (!Has("Compras.Aprobar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _ordenes.AprobarAsync(eid, id, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Orden aprobada y emitida." : r.Error;
        return RedirectToAction(nameof(DetalleOrden), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarOrden(int id, string? motivo, CancellationToken ct)
    {
        if (!Has("Compras.Aprobar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _ordenes.RechazarAsync(eid, id, motivo, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Orden rechazada; regresó a borrador." : r.Error;
        return RedirectToAction(nameof(DetalleOrden), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UmbralAprobacion(decimal? umbral, CancellationToken ct)
    {
        if (!Has("Compras.Aprobar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _ordenes.SetUmbralAprobacionAsync(eid, umbral, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Umbral de aprobación actualizado." : r.Error;
        return RedirectToAction(nameof(Ordenes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecibirOrden(int id, RegistrarRecepcionOrdenCompraRequest model, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(DetalleOrden), new { id });
        }
        var result = await _ordenes.RecibirAsync(eid, id, model, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Recepcion registrada e inventario actualizado."
            : result.Error;
        return RedirectToAction(nameof(DetalleOrden), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertirOrden(int id, ConvertirOrdenCompraRequest model, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _ordenes.ConvertirAFacturaAsync(eid, id, model, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Orden convertida a cuenta por pagar."
            : result.Error;
        return RedirectToAction(nameof(DetalleOrden), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? proveedorId, bool soloPendientes = false, string? search = null, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Compras.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.ListFacturasAsync(eid, proveedorId, soloPendientes, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        var resumen = await _compras.ResumenAsync(eid, ct);
        ViewBag.Search = search;
        ViewBag.SoloPendientes = soloPendientes;
        ViewBag.ProveedorId = proveedorId;
        ViewBag.Resumen = resumen.Value;
        ViewBag.PuedeGestionar = Has("Compras.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        if (!Has("Compras.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.GetFacturaAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.PuedeGestionar = Has("Compras.Gestionar");
        await CargarCuentasAsync(eid, ct);
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int? proveedorId, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarProveedoresAsync(eid, ct);
        await CargarProductosInventarioAsync(eid, ct);
        ViewBag.TiposDocumento = TiposDocumento;
        ViewBag.CondicionesPago = CondicionesPago;
        return View(new CrearFacturaCompraRequest
        {
            ProveedorId = proveedorId ?? 0,
            FechaEmision = DateOnly.FromDateTime(DateTime.UtcNow),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearFacturaCompraRequest model, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.CrearFacturaAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            await CargarProveedoresAsync(eid, ct);
            await CargarProductosInventarioAsync(eid, ct);
            ViewBag.TiposDocumento = TiposDocumento;
            ViewBag.CondicionesPago = CondicionesPago;
            return View(model);
        }
        TempData["Success"] = "Factura de compra registrada.";
        return RedirectToAction(nameof(Detalle), new { id = result.Value!.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _compras.AnularFacturaAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Factura anulada." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPago(RegistrarPagoProveedorRequest model, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.RegistrarPagoAsync(eid, model, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Pago registrado." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id = model.FacturaCompraId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnularPago(int pagoId, int facturaId, CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _compras.AnularPagoAsync(eid, pagoId, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Pago anulado." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id = facturaId });
    }

    private async Task CargarProveedoresAsync(int eid, CancellationToken ct)
    {
        var provs = await _compras.ListProveedoresAsync(eid, new PagedQuery { Page = 1, PageSize = 500 }, ct);
        ViewBag.Proveedores = provs.Value?.Items.Where(p => p.EstadoCodigo == ProveedorEstados.Activo).ToList() ?? new List<ProveedorDto>();
    }

    private async Task CargarProductosInventarioAsync(int eid, CancellationToken ct)
    {
        var prods = await _productos.GetListAsync(eid, new PagedQuery { Page = 1, PageSize = 1000 }, ct: ct);
        ViewBag.Productos = prods.Value?.Items
            .Where(p => p.EstadoCodigo == "ACTIVO" && !p.EsServicio).ToList() ?? new List<ProductoDto>();
    }

    private async Task CargarCatalogosOrdenAsync(int eid, CancellationToken ct)
    {
        await CargarProveedoresAsync(eid, ct);
        var prods = await _productos.GetListAsync(eid, new PagedQuery { Page = 1, PageSize = 1000 }, ct: ct);
        ViewBag.Productos = prods.Value?.Items.Where(p => p.EstadoCodigo == "ACTIVO").ToList()
            ?? new List<ProductoDto>();
    }

    private async Task<IActionResult> EjecutarAccionOrden(
        int id,
        string mensaje,
        Func<int, string?, CancellationToken, Task<Result<OrdenCompraDetalleDto>>> accion,
        CancellationToken ct)
    {
        if (!Has("Compras.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await accion(eid, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? mensaje : result.Error;
        return RedirectToAction(nameof(DetalleOrden), new { id });
    }

    private async Task CargarCuentasAsync(int eid, CancellationToken ct)
    {
        var cuentas = await _tesoreria.ListCuentasAsync(eid, new PagedQuery { Page = 1, PageSize = 200 }, ct);
        ViewBag.Cuentas = cuentas.Value?.Items.Where(c => c.EstadoCodigo == EstadosCuentaTesoreria.Activa).ToList()
            ?? new List<NeoSTP.Application.Tesoreria.Dtos.CuentaTesoreriaDto>();
        ViewBag.FormasPago = FormasPago;
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Compras opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
