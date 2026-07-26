using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Certificacion;
using NeoSTP.Application.Dte.Certificacion.Dtos;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Dte.Eventos;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;

namespace NeoSTP.Web.Controllers;

[Authorize]
[RequireModulo("EVENTOSDTE")]
public class DteEventosController : Controller
{
    private readonly IDteEventoService _service;
    private readonly IDteEventoPdfService _pdf;
    private readonly IDteDocumentosService _documentos;
    private readonly ICertificacionDteService _certificacion;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public DteEventosController(IDteEventoService service, IDteEventoPdfService pdf,
        IDteDocumentosService documentos, ICertificacionDteService certificacion,
        ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _service = service;
        _pdf = pdf;
        _documentos = documentos;
        _certificacion = certificacion;
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

    // ----- Forms de creaciÃ³n -----

    [HttpGet]
    public async Task<IActionResult> CreateInvalidacion([FromQuery] int? certificacionEscenarioId, CancellationToken ct)
    {
        if (!Has("DTE.Invalidar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Procesado, ct);
        var model = new CrearEventoInvalidacionRequest
        {
            TipoAnulacion = 2,
            MotivoAnulacion = certificacionEscenarioId.HasValue ? "Prueba de certificacion DTE" : null,
            NombreResponsable = certificacionEscenarioId.HasValue ? "Responsable certificacion DTE" : null!,
            TipoDocResponsable = certificacionEscenarioId.HasValue ? "13" : null!,
            NumDocResponsable = certificacionEscenarioId.HasValue ? "00000000-0" : null!,
        };
        await AplicarCertificacionInvalidacionAsync(model, eid, certificacionEscenarioId, ct);
        return View(model);
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

        TempData["Success"] = $"Evento de invalidaciÃ³n transmitido. Sello: {result.Value!.SelloOEstado}";
        var certificacionRedirect = await AsociarCertificacionEventoAsync(result.Value!.EventoId, model, eid, ct);
        if (certificacionRedirect is not null) return certificacionRedirect;

        return result.Value.EventoId.HasValue
            ? RedirectToAction(nameof(Details), new { id = result.Value.EventoId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateContingencia([FromQuery] int? certificacionEscenarioId, CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Contingencia, ct);
        var model = new CrearEventoContingenciaRequest
        {
            TipoContingencia = 1,
            Motivo = certificacionEscenarioId.HasValue ? "Prueba de certificacion DTE" : null,
            NombreResponsable = certificacionEscenarioId.HasValue ? "Responsable certificacion DTE" : null!,
            TipoDocResponsable = certificacionEscenarioId.HasValue ? "13" : null!,
            NumeroDocResponsable = certificacionEscenarioId.HasValue ? "00000000-0" : null!,
        };
        await AplicarCertificacionContingenciaAsync(model, eid, certificacionEscenarioId, ct);
        return View(model);
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
        var certificacionRedirect = await AsociarCertificacionEventoAsync(result.Value!.EventoId, model, eid, ct);
        if (certificacionRedirect is not null) return certificacionRedirect;

        return result.Value.EventoId.HasValue
            ? RedirectToAction(nameof(Details), new { id = result.Value.EventoId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateRetorno([FromQuery] int? certificacionEscenarioId, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarDtesAsync(eid, soloEstado: DteEstadoCodigos.Procesado, ct);
        var model = new CrearEventoRetornoRequest();
        await AplicarCertificacionRetornoAsync(model, eid, certificacionEscenarioId, ct);
        return View(model);
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
        var certificacionRedirect = await AsociarCertificacionEventoAsync(result.Value!.EventoId, model, eid, ct);
        if (certificacionRedirect is not null) return certificacionRedirect;

        return result.Value.EventoId.HasValue
            ? RedirectToAction(nameof(Details), new { id = result.Value.EventoId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateOperacionesEspeciales([FromQuery] int? certificacionEscenarioId, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var model = new CrearEventoOperacionesEspecialesRequest
        {
            Monto = certificacionEscenarioId.HasValue ? 10m : 0m,
        };
        await AplicarCertificacionOperacionesAsync(model, eid, certificacionEscenarioId, ct);
        return View(model);
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
                ModelState.AddModelError(nameof(model.Descripcion), "DescripciÃ³n es requerida.");
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
        var certificacionRedirect = await AsociarCertificacionEventoAsync(result.Value!.EventoId, model, eid, ct);
        if (certificacionRedirect is not null) return certificacionRedirect;

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

    private async Task AplicarCertificacionInvalidacionAsync(
        CrearEventoInvalidacionRequest model,
        int empresaId,
        int? escenarioId,
        CancellationToken ct)
    {
        var escenario = await GetEscenarioCertificacionAsync(TipoEventoCodigos.Invalidacion, empresaId, escenarioId, ct);
        if (escenario is null) return;

        model.CertificacionEscenarioId = escenario.Id;
        model.CertificacionTipoCodigo = TipoEventoCodigos.Invalidacion;
        model.CertificacionEscenarioCodigo = escenario.Codigo;
        model.CertificacionEscenarioNombre = escenario.Nombre;
        model.MotivoAnulacion = $"Prueba certificacion {escenario.Codigo}: {escenario.Nombre}";
    }

    private async Task AplicarCertificacionContingenciaAsync(
        CrearEventoContingenciaRequest model,
        int empresaId,
        int? escenarioId,
        CancellationToken ct)
    {
        var escenario = await GetEscenarioCertificacionAsync(TipoEventoCodigos.Contingencia, empresaId, escenarioId, ct);
        if (escenario is null) return;

        model.CertificacionEscenarioId = escenario.Id;
        model.CertificacionTipoCodigo = TipoEventoCodigos.Contingencia;
        model.CertificacionEscenarioCodigo = escenario.Codigo;
        model.CertificacionEscenarioNombre = escenario.Nombre;
        model.Motivo = $"Prueba certificacion {escenario.Codigo}: {escenario.Nombre}";
    }

    private async Task AplicarCertificacionRetornoAsync(
        CrearEventoRetornoRequest model,
        int empresaId,
        int? escenarioId,
        CancellationToken ct)
    {
        var escenario = await GetEscenarioCertificacionAsync(TipoEventoCodigos.Retorno, empresaId, escenarioId, ct);
        if (escenario is null) return;

        model.CertificacionEscenarioId = escenario.Id;
        model.CertificacionTipoCodigo = TipoEventoCodigos.Retorno;
        model.CertificacionEscenarioCodigo = escenario.Codigo;
        model.CertificacionEscenarioNombre = escenario.Nombre;
    }

    private async Task AplicarCertificacionOperacionesAsync(
        CrearEventoOperacionesEspecialesRequest model,
        int empresaId,
        int? escenarioId,
        CancellationToken ct)
    {
        var escenario = await GetEscenarioCertificacionAsync(TipoEventoCodigos.OperacionesEspeciales, empresaId, escenarioId, ct);
        if (escenario is null) return;

        model.CertificacionEscenarioId = escenario.Id;
        model.CertificacionTipoCodigo = TipoEventoCodigos.OperacionesEspeciales;
        model.CertificacionEscenarioCodigo = escenario.Codigo;
        model.CertificacionEscenarioNombre = escenario.Nombre;
        model.Descripcion = $"Prueba certificacion {escenario.Codigo}: {escenario.Nombre}";
        model.Monto = model.Monto <= 0 ? 10m : model.Monto;
    }

    private async Task<CertificacionEscenarioDto?> GetEscenarioCertificacionAsync(
        string tipoCodigo,
        int empresaId,
        int? escenarioId,
        CancellationToken ct)
    {
        if (!escenarioId.HasValue) return null;

        var escenarios = await _certificacion.GetEscenariosAsync(tipoCodigo, empresaId, ct);
        return escenarios.Value?.FirstOrDefault(e => e.Id == escenarioId.Value);
    }

    private async Task<IActionResult?> AsociarCertificacionEventoAsync(
        int? eventoId,
        CrearEventoInvalidacionRequest model,
        int empresaId,
        CancellationToken ct)
        => await AsociarCertificacionEventoAsync(
            eventoId,
            model.CertificacionEscenarioId,
            model.CertificacionTipoCodigo,
            model.CertificacionEscenarioCodigo,
            empresaId,
            ct);

    private async Task<IActionResult?> AsociarCertificacionEventoAsync(
        int? eventoId,
        CrearEventoContingenciaRequest model,
        int empresaId,
        CancellationToken ct)
        => await AsociarCertificacionEventoAsync(
            eventoId,
            model.CertificacionEscenarioId,
            model.CertificacionTipoCodigo,
            model.CertificacionEscenarioCodigo,
            empresaId,
            ct);

    private async Task<IActionResult?> AsociarCertificacionEventoAsync(
        int? eventoId,
        CrearEventoRetornoRequest model,
        int empresaId,
        CancellationToken ct)
        => await AsociarCertificacionEventoAsync(
            eventoId,
            model.CertificacionEscenarioId,
            model.CertificacionTipoCodigo,
            model.CertificacionEscenarioCodigo,
            empresaId,
            ct);

    private async Task<IActionResult?> AsociarCertificacionEventoAsync(
        int? eventoId,
        CrearEventoOperacionesEspecialesRequest model,
        int empresaId,
        CancellationToken ct)
        => await AsociarCertificacionEventoAsync(
            eventoId,
            model.CertificacionEscenarioId,
            model.CertificacionTipoCodigo,
            model.CertificacionEscenarioCodigo,
            empresaId,
            ct);

    private async Task<IActionResult?> AsociarCertificacionEventoAsync(
        int? eventoId,
        int? escenarioId,
        string? tipoCodigo,
        string? escenarioCodigo,
        int empresaId,
        CancellationToken ct)
    {
        if (!eventoId.HasValue || !escenarioId.HasValue) return null;

        var result = await _certificacion.MarcarCompletadoPorEventoAsync(
            eventoId.Value,
            new MarcarCompletadoRequest
            {
                EscenarioId = escenarioId.Value,
                Notas = $"Evento creado desde certificacion: {escenarioCodigo ?? escenarioId.Value.ToString()}",
            },
            empresaId,
            _currentUser.Username,
            ct);

        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "El evento fue transmitido, pero no se pudo asociar a certificacion.";
            return RedirectToAction(nameof(Details), new { id = eventoId.Value });
        }

        TempData["Success"] = $"Evento asociado al escenario {result.Value!.EscenarioCodigo} (estado {result.Value.EstadoCodigo}).";
        return !string.IsNullOrWhiteSpace(tipoCodigo)
            ? RedirectToAction("Tipo", "Certificacion", new { codigo = tipoCodigo })
            : RedirectToAction(nameof(Details), new { id = eventoId.Value });
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
