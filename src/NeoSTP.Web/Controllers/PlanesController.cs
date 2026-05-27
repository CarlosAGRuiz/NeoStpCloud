using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Licenciamiento;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class PlanesController : Controller
{
    private readonly IPlanesService _planes;

    public PlanesController(IPlanesService planes) { _planes = planes; }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await _planes.GetListAsync(ct);
        return View(result.Value);
    }
}
