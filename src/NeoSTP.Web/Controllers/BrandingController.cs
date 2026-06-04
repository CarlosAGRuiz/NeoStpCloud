using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Branding de la empresa: logo y firma usados en el PDF del DTE y en el correo.
/// Opera dentro de una empresa (SuperAdmin debe entrar en modo soporte).
/// </summary>
[Authorize]
[Route("branding")]
public class BrandingController : Controller
{
    private const long MaxUpload = 1_048_576; // 1 MB

    private readonly IBrandingService _branding;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public BrandingController(IBrandingService branding, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _branding = branding;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        return View(await _branding.GetAsync(eid, ct));
    }

    [HttpGet("logo")]
    public async Task<IActionResult> Logo(CancellationToken ct)
    {
        if (RequireEmpresa() is not int eid) return NotFound();
        var img = await _branding.GetLogoAsync(eid, ct);
        return img is null ? NotFound() : File(img.Contenido, img.ContentType);
    }

    [HttpGet("firma")]
    public async Task<IActionResult> Firma(CancellationToken ct)
    {
        if (RequireEmpresa() is not int eid) return NotFound();
        var img = await _branding.GetFirmaAsync(eid, ct);
        return img is null ? NotFound() : File(img.Contenido, img.ContentType);
    }

    [HttpPost("logo")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2_000_000)]
    public Task<IActionResult> SubirLogo(IFormFile? archivo, CancellationToken ct)
        => SubirAsync(archivo, esLogo: true, ct);

    [HttpPost("firma")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2_000_000)]
    public Task<IActionResult> SubirFirma(IFormFile? archivo, CancellationToken ct)
        => SubirAsync(archivo, esLogo: false, ct);

    private async Task<IActionResult> SubirAsync(IFormFile? archivo, bool esLogo, CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        if (archivo is null || archivo.Length == 0)
        {
            TempData["Error"] = "Selecciona una imagen (PNG, JPG o WEBP).";
            return RedirectToAction(nameof(Index));
        }
        if (archivo.Length > MaxUpload)
        {
            TempData["Error"] = "La imagen excede 1 MB.";
            return RedirectToAction(nameof(Index));
        }

        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var r = esLogo
            ? await _branding.GuardarLogoAsync(eid, bytes, archivo.ContentType, archivo.FileName, _currentUser.Username, ct)
            : await _branding.GuardarFirmaAsync(eid, bytes, archivo.ContentType, archivo.FileName, _currentUser.Username, ct);

        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess
            ? $"{(esLogo ? "Logo" : "Firma")} actualizado."
            : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("firma-texto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarFirmaTexto(string? firmaTexto, CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var r = await _branding.GuardarFirmaTextoAsync(eid, firmaTexto, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Texto de firma guardado." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("logo/quitar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarLogo(CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var r = await _branding.EliminarLogoAsync(eid, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Logo eliminado." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("firma/quitar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarFirma(CancellationToken ct)
    {
        if (!Has("DTE.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var r = await _branding.EliminarFirmaAsync(eid, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Firma eliminada." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "El branding opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
