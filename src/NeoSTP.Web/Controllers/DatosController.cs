using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Datos;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Portabilidad de datos (E8): el cliente se lleva todo lo suyo en un ZIP de CSVs.
/// </summary>
[Authorize]
public class DatosController : Controller
{
    private readonly IPortabilidadService _portabilidad;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Empresas.IEmpresaContext _empresaContext;

    public DatosController(
        IPortabilidadService portabilidad,
        ICurrentUser currentUser,
        NeoSTP.Application.Empresas.IEmpresaContext empresaContext)
    {
        _portabilidad = portabilidad;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!Has("Datos.Exportar")) return Forbid();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Exportar(CancellationToken ct)
    {
        if (!Has("Datos.Exportar")) return Forbid();
        if (_empresaContext.CurrentEmpresaId is not int eid) return Forbid();

        var r = await _portabilidad.ExportarAsync(eid, _currentUser.Username, ct);
        if (r.IsFailure)
        {
            TempData["Error"] = r.Error;
            return RedirectToAction(nameof(Index));
        }

        return File(r.Value!.Contenido, "application/zip", r.Value.NombreArchivo);
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
}
