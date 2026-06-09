using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Tesoreria;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOCOMPRAS — facturas de compra y cuentas por pagar. Permisos Compras.Ver / Compras.Gestionar.</summary>
[Authorize]
public class ComprasController : Controller
{
    public static readonly string[] TiposDocumento = TiposDocumentoCompra.All;
    public static readonly string[] CondicionesPago = CondicionesPagoCompra.All;
    public static readonly string[] FormasPago = ["EFECTIVO", "TRANSFERENCIA", "CHEQUE", "TARJETA", "QR", "OTRO"];

    private readonly ICompraService _compras;
    private readonly ITesoreriaService _tesoreria;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public ComprasController(ICompraService compras, ITesoreriaService tesoreria, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _compras = compras;
        _tesoreria = tesoreria;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
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
