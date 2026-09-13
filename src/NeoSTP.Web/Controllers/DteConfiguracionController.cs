using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
[Route("dte/configuracion")]
public class DteConfiguracionController : Controller
{
    private readonly IDteConfiguracionService _service;
    private readonly ICatalogosService _catalogos;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Empresas.IEmpresaContext _empresaContext;

    public DteConfiguracionController(
        IDteConfiguracionService service,
        ICatalogosService catalogos,
        ICurrentUser currentUser,
        NeoSTP.Application.Empresas.IEmpresaContext empresaContext)
    {
        _service = service;
        _catalogos = catalogos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.GetAsync(eid, ct);
        await LoadCatalogosAsync(eid, ct);
        var model = ToViewModel(result.Value!);
        await LoadVersionesAsync(eid, model, ct);
        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DteConfiguracionViewModel model, CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        if (!ModelState.IsValid)
        {
            await LoadCatalogosAsync(eid, ct);
            await LoadVersionesAsync(eid, model, ct);
            return View(nameof(Index), model);
        }

        var result = await _service.SaveAsync(eid, new SaveDteConfiguracionRequest
        {
            AmbienteCodigo = model.AmbienteCodigo,
            UsuarioMh = model.UsuarioMh,
            PasswordMh = model.PasswordMh,
            TipoEstablecimientoCodigo = model.TipoEstablecimientoCodigo,
            CodigoEstablecimientoMh = model.CodigoEstablecimientoMh,
            CodigoPuntoVentaMh = model.CodigoPuntoVentaMh,
        }, _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            await LoadCatalogosAsync(eid, ct);
            await LoadVersionesAsync(eid, model, ct);
            return View(nameof(Index), model);
        }

        TempData["Success"] = "Configuración DTE guardada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("certificado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadCertificado(UploadCertificadoViewModel model, CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        if (model.Archivo is null || model.Archivo.Length == 0)
        {
            TempData["Error"] = "Selecciona un archivo de certificado.";
            return RedirectToAction(nameof(Index));
        }

        using var ms = new MemoryStream();
        await model.Archivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var result = await _service.UploadCertificadoAsync(eid, new UploadCertificadoRequest
        {
            Nombre = model.Archivo.FileName,
            ContenidoBase64 = Convert.ToBase64String(bytes),
            Password = model.Password,
            Emitido = model.Emitido,
            Vence = model.Vence,
        }, _currentUser.Username, ct);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Certificado '{model.Archivo.FileName}' cargado ({bytes.Length:N0} bytes)."
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("certificado/eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarCertificado(CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.EliminarCertificadoAsync(eid, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Certificado eliminado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("probar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProbarConexion(CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _service.ProbarConexionAsync(eid, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
        }
        else if (result.Value!.Exitoso)
        {
            TempData["Success"] = $"Conexión OK ({result.Value.Detalle})";
        }
        else
        {
            TempData["Error"] = $"Falló la prueba [{result.Value.CodigoHttp}]: {result.Value.Mensaje} — {result.Value.Detalle}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("recuperar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecuperarConfiguracion(int versionId, CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.RecuperarVersionAsync(eid, versionId, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Configuración recuperada. Prueba nuevamente la conexión con Hacienda antes de emitir."
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private static DteConfiguracionViewModel ToViewModel(DteConfiguracionDto d) => new()
    {
        AmbienteCodigo = d.AmbienteCodigo,
        UsuarioMh = d.UsuarioMh,
        TienePasswordMh = d.TienePasswordMh,
        TipoEstablecimientoCodigo = d.TipoEstablecimientoCodigo,
        CodigoEstablecimientoMh = d.CodigoEstablecimientoMh,
        CodigoPuntoVentaMh = d.CodigoPuntoVentaMh,
        TieneCertificado = d.TieneCertificado,
        CertificadoNombre = d.CertificadoNombre,
        CertificadoHuella = d.CertificadoHuella,
        CertificadoVence = d.CertificadoVence,
        UltimaPruebaAt = d.UltimaPruebaAt,
        UltimaPruebaResultado = d.UltimaPruebaResultado,
        UltimaPruebaDetalle = d.UltimaPruebaDetalle,
        EsCompleto = d.EsCompleto,
    };

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Esta pantalla opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }

    private async Task LoadCatalogosAsync(int empresaId, CancellationToken ct)
    {
        async Task<IReadOnlyList<NeoSTP.Application.Catalogos.Dtos.CatalogoItemDto>> Items(string code)
            => (await _catalogos.GetItemsAsync(code, empresaId, ct: ct)).Value
               ?? new List<NeoSTP.Application.Catalogos.Dtos.CatalogoItemDto>();

        ViewBag.Ambientes = await Items("AMBIENTE_DTE");
        var tiposEstablecimiento = new List<SelectListItem>();
        foreach (var item in await Items("TIPO_ESTABLECIMIENTO"))
        {
            if (DteTiposEstablecimiento.TryNormalize(item.Codigo, out var codigoMh) && codigoMh is not null)
            {
                tiposEstablecimiento.Add(new SelectListItem(item.Valor, codigoMh));
            }
        }

        ViewBag.TiposEstablecimiento = tiposEstablecimiento;
    }

    private async Task LoadVersionesAsync(int empresaId, DteConfiguracionViewModel model, CancellationToken ct)
    {
        model.Versiones = (await _service.GetVersionesAsync(empresaId, ct)).Value
            ?? Array.Empty<DteConfiguracionVersionDto>();
    }
}
