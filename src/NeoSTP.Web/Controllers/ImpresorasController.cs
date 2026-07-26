using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Domain.Core.Pos;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOPOS — configuración de impresoras de tickets. Permiso Pos.Configurar (ver: Pos.Ver).</summary>
[Authorize]
[RequireModulo("NEOPOS")]
public class ImpresorasController : Controller
{
    public static readonly string[] Conexiones = ConexionImpresora.All;

    private readonly IPosConfigService _posConfig;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public ImpresorasController(IPosConfigService posConfig, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _posConfig = posConfig;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("Pos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _posConfig.ListImpresorasAsync(eid, ct);
        ViewBag.PuedeConfigurar = Has("Pos.Configurar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int? id, CancellationToken ct)
    {
        if (!Has("Pos.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        ViewBag.Conexiones = Conexiones;
        ViewBag.ImpresoraId = id;
        if (id is int existing)
        {
            var r = await _posConfig.GetImpresoraAsync(eid, existing, ct);
            if (r.IsFailure) return NotFound();
            var i = r.Value!;
            return View(new GuardarImpresoraRequest
            {
                Nombre = i.Nombre, Conexion = i.Conexion, AnchoMm = i.AnchoMm, Ip = i.Ip, Puerto = i.Puerto,
                CorteAutomatico = i.CorteAutomatico, EsPredeterminada = i.EsPredeterminada, Notas = i.Notas,
            });
        }
        return View(new GuardarImpresoraRequest { Conexion = "NAVEGADOR", AnchoMm = 80, Puerto = 9100, CorteAutomatico = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int? id, GuardarImpresoraRequest model, CancellationToken ct)
    {
        if (!Has("Pos.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _posConfig.GuardarImpresoraAsync(eid, id, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            ViewBag.Conexiones = Conexiones;
            ViewBag.ImpresoraId = id;
            return View(model);
        }
        TempData["Success"] = "Impresora guardada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        if (!Has("Pos.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _posConfig.EliminarImpresoraAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Impresora eliminada." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Probar(int id, CancellationToken ct)
    {
        if (!Has("Pos.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _posConfig.ProbarImpresoraAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Ticket de prueba enviado a la impresora." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Imprime el ticket de una venta en una impresora de red (ESC/POS).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImprimirRed(int ventaId, int impresoraId, CancellationToken ct)
    {
        if (!Has("Pos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _posConfig.ImprimirVentaEnRedAsync(eid, ventaId, impresoraId, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Ticket enviado a la impresora de red." : result.Error;
        return RedirectToAction("Detalle", "Pos", new { id = ventaId });
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "La configuración de impresoras opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
