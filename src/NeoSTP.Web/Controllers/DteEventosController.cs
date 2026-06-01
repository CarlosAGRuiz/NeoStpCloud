using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Dte.Eventos;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class DteEventosController : Controller
{
    private readonly IDteEventoService _service;
    private readonly IDteEventoPdfService _pdf;
    private readonly IDteDocumentosService _documentos;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public DteEventosController(IDteEventoService service, IDteEventoPdfService pdf,
        IDteDocumentosService documentos, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _service = service;
        _pdf = pdf;
        _documentos = documentos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    // ----- Lectura -----

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? tipo, [FromQuery] string? estado, CancellationToken ct)
    {
        if (!Has("DTE.Eventos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.GetListAsync(eid, tipo, estado, ct);
        ViewBag.Tipo = tipo;
        ViewBag.Estado = estado;
        return View(result.Value ?? Array.Empty<DteEventoListDto>());
    }

    [HttpGet("DteEventos/Details/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        if (!Has("DTE.Eventos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.GetByIdAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        return View(result.Value);
    }

    [HttpGet("DteEventos/Json/{id:int}")]
    public async Task<IActionResult> Json(int id, CancellationToken ct)
    {
        if (!Has("DTE.Eventos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.GetJsonAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        return File(System.Text.Encoding.UTF8.GetBytes(result.Value!.JsonSinFirmar),
            "application/json", $"evento-{id}.json");
    }

    [HttpGet("DteEventos/Pdf/{id:int}")]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        if (!Has("DTE.Eventos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _pdf.GenerarAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }

    // ----- Forms de creación -----

    [HttpGet]
    public async Task<IActionResult> CreateInvalidacion(CancellationToken ct)
    {
        if (!Has("DTE.Invalidar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Procesado, ct);
        return View(new CrearEventoInvalidacionRequest { TipoAnulacion = 2 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvalidacion(CrearEventoInvalidacionRequest model, CancellationToken ct)
    {
        if (!Has("DTE.Invalidar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        if (!ModelState.IsValid)
        {
            await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Procesado, ct);
            return View(model);
        }

        var result = await _service.CrearInvalidacionAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error al crear evento.");
            await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Procesado, ct);
            return View(model);
        }

        TempData["Success"] = $"Evento de invalidación transmitido. Sello: {result.Value!.SelloOEstado}";
        return result.Value.EventoId.HasValue
            ? RedirectToAction(nameof(Details), new { id = result.Value.EventoId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateContingencia(CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Contingencia, ct);
        return View(new CrearEventoContingenciaRequest { TipoContingencia = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContingencia(CrearEventoContingenciaRequest model, CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        if (!ModelState.IsValid || model.DocumentoIds.Count == 0)
        {
            if (model.DocumentoIds.Count == 0)
                ModelState.AddModelError(nameof(model.DocumentoIds), "Selecciona al menos un DTE en contingencia.");
            await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Contingencia, ct);
            return View(model);
        }

        var result = await _service.CrearContingenciaAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error al crear evento.");
            await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Contingencia, ct);
            return View(model);
        }

        TempData["Success"] = $"Evento de contingencia transmitido. Sello: {result.Value!.SelloOEstado}";
        return result.Value.EventoId.HasValue
            ? RedirectToAction(nameof(Details), new { id = result.Value.EventoId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateRetorno(CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Procesado, ct);
        return View(new CrearEventoRetornoRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRetorno(CrearEventoRetornoRequest model, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        if (!ModelState.IsValid || model.DocumentoOrigenId <= 0)
        {
            if (model.DocumentoOrigenId <= 0)
                ModelState.AddModelError(nameof(model.DocumentoOrigenId), "Selecciona un DTE origen.");
            await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Procesado, ct);
            return View(model);
        }

        var result = await _service.CrearRetornoAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error al crear evento.");
            await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Procesado, ct);
            return View(model);
        }

        TempData["Success"] = $"Evento de retorno transmitido. Sello: {result.Value!.SelloOEstado}";
        return result.Value.EventoId.HasValue
            ? RedirectToAction(nameof(Details), new { id = result.Value.EventoId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult CreateOperacionesEspeciales()
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is null) return RedirectToSoporte();
        return View(new CrearEventoOperacionesEspecialesRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOperacionesEspeciales(CrearEventoOperacionesEspecialesRequest model, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Descripcion) || model.Monto <= 0)
        {
            if (string.IsNullOrWhiteSpace(model.Descripcion))
                ModelState.AddModelError(nameof(model.Descripcion), "Descripción es requerida.");
            if (model.Monto <= 0)
                ModelState.AddModelError(nameof(model.Monto), "Monto debe ser mayor a 0.");
            return View(model);
        }

        var result = await _service.CrearOperacionesEspecialesAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error al crear evento.");
            return View(model);
        }

        TempData["Success"] = $"Operaciones especiales transmitidas. Sello: {result.Value!.SelloOEstado}";
        return result.Value.EventoId.HasValue
            ? RedirectToAction(nameof(Details), new { id = result.Value.EventoId.Value })
            : RedirectToAction(nameof(Index));
    }

    // ----- Helpers -----

    private async Task CargarDtesAsync(int empresaId, string soloEstado, CancellationToken ct)
    {
        var result = await _documentos.GetListAsync(empresaId,
            new DteListQuery { Page = 1, PageSize = 100, EstadoCodigo = soloEstado }, ct);
        ViewBag.Dtes = result.Value?.Items ?? new List<DteDocumentoListItemDto>();
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Selecciona una empresa en modo soporte para gestionar eventos.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
