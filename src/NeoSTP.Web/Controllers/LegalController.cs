using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Legal;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Páginas legales públicas: /legal/terms · /legal/privacy · /legal/cookies · /legal/dpa
/// No requieren autenticación.
/// </summary>
public class LegalController : Controller
{
    private readonly ILegalDocumentService _legal;

    public LegalController(ILegalDocumentService legal) => _legal = legal;

    // GET /legal/terms
    // GET /legal/privacy
    // GET /legal/cookies
    // GET /legal/dpa
    [Route("legal/{slug}")]
    public IActionResult Index(string slug)
    {
        var contenido = _legal.ObtenerContenido(slug);
        if (contenido is null) return NotFound();

        var titulo = slug.ToLowerInvariant() switch
        {
            "terms"   => "Términos y Condiciones",
            "privacy" => "Política de Privacidad",
            "cookies" => "Política de Cookies",
            "dpa"     => "Acuerdo de Procesamiento de Datos (DPA)",
            _         => slug
        };

        ViewBag.Titulo    = titulo;
        ViewBag.Contenido = contenido;
        ViewBag.Slug      = slug.ToLowerInvariant();
        return View("Documento");
    }
}
