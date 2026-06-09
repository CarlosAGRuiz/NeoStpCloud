using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Common;
using NeoSTP.Application.Crm;
using NeoSTP.Application.Crm.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Productos;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOCRM — pipeline, contactos, actividades y cotizaciones. Permisos Crm.*.</summary>
[Authorize]
public class CrmController : Controller
{
    private readonly ICrmService _crm;
    private readonly IClientesService _clientes;
    private readonly IProductosService _productos;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public CrmController(ICrmService crm, IClientesService clientes, IProductosService productos, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _crm = crm;
        _clientes = clientes;
        _productos = productos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    // ── Pipeline (dashboard) ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("Crm.Oportunidades.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var etapas = await _crm.ListEtapasAsync(eid, ct);
        var abiertas = await _crm.ListOportunidadesAsync(eid, "ABIERTA", null, null, new PagedQuery { Page = 1, PageSize = 200 }, ct);
        var resumen = await _crm.ResumenAsync(eid, ct);
        ViewBag.Etapas = etapas.Value ?? new List<EtapaPipelineCrmDto>();
        ViewBag.Resumen = resumen.Value;
        ViewBag.PuedeGestionar = Has("Crm.Oportunidades.Gestionar");
        return View(abiertas.Value?.Items ?? new List<OportunidadCrmDto>());
    }

    // ── Oportunidades ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> NuevaOportunidad(int? clienteId, CancellationToken ct)
    {
        if (!Has("Crm.Oportunidades.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        await CargarRefsAsync(eid, ct);
        return View(new CrearOportunidadCrmRequest { ClienteId = clienteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevaOportunidad(CrearOportunidadCrmRequest model, CancellationToken ct)
    {
        if (!Has("Crm.Oportunidades.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _crm.CrearOportunidadAsync(eid, model, _currentUser.Username, ct);
        if (r.IsFailure)
        {
            ModelState.AddModelError(string.Empty, r.Error ?? "Error.");
            await CargarRefsAsync(eid, ct);
            return View(model);
        }
        TempData["Success"] = "Oportunidad creada.";
        return RedirectToAction(nameof(Oportunidad), new { id = r.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Oportunidad(int id, CancellationToken ct)
    {
        if (!Has("Crm.Oportunidades.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _crm.GetOportunidadAsync(eid, id, ct);
        if (r.IsFailure) return NotFound();
        var etapas = await _crm.ListEtapasAsync(eid, ct);
        var cotizaciones = await _crm.ListCotizacionesAsync(eid, null, id, new PagedQuery { Page = 1, PageSize = 50 }, ct);
        ViewBag.Etapas = etapas.Value ?? new List<EtapaPipelineCrmDto>();
        ViewBag.Cotizaciones = cotizaciones.Value?.Items ?? new List<CotizacionCrmDto>();
        ViewBag.PuedeGestionar = Has("Crm.Oportunidades.Gestionar");
        ViewBag.PuedeActividades = Has("Crm.Actividades.Gestionar");
        return View(r.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEtapa(int id, int etapaPipelineCrmId, string? motivoPerdida, CancellationToken ct)
    {
        if (!Has("Crm.Oportunidades.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _crm.CambiarEtapaAsync(eid, id, new CambiarEtapaOportunidadRequest
        { EtapaPipelineCrmId = etapaPipelineCrmId, MotivoPerdida = motivoPerdida }, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Etapa actualizada." : r.Error;
        var origen = Request.Headers.Referer.ToString();
        return origen.Contains("/Crm/Oportunidad/", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Oportunidad), new { id })
            : RedirectToAction(nameof(Index));
    }

    // ── Contactos ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Contactos(string? search, int page = 1, int? editId = null, CancellationToken ct = default)
    {
        if (!Has("Crm.Contactos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _crm.ListContactosAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ContactoCrmDto? editar = null;
        if (editId is int cid)
        {
            var c = await _crm.GetContactoAsync(eid, cid, ct);
            if (c.IsSuccess) editar = c.Value;
        }
        await CargarClientesAsync(eid, ct);
        ViewBag.Search = search;
        ViewBag.Editar = editar;
        ViewBag.PuedeGestionar = Has("Crm.Contactos.Gestionar");
        return View(r.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContactoGuardar(int? id, UpsertContactoCrmRequest model, CancellationToken ct)
    {
        if (!Has("Crm.Contactos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = id is int cid
            ? await _crm.ActualizarContactoAsync(eid, cid, model, _currentUser.Username, ct)
            : await _crm.CrearContactoAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Contacto guardado." : r.Error;
        return RedirectToAction(nameof(Contactos));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContactoInactivar(int id, CancellationToken ct)
    {
        if (!Has("Crm.Contactos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _crm.InactivarContactoAsync(eid, id, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Contacto inactivado." : r.Error;
        return RedirectToAction(nameof(Contactos));
    }

    // ── Actividades ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Actividades(bool soloPendientes = true, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Crm.Actividades.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _crm.ListActividadesAsync(eid, soloPendientes, null, new PagedQuery { Page = page, PageSize = 30 }, ct);
        ViewBag.SoloPendientes = soloPendientes;
        ViewBag.PuedeGestionar = Has("Crm.Actividades.Gestionar");
        return View(r.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActividadCrear(CrearActividadCrmRequest model, string? volverA, CancellationToken ct)
    {
        if (!Has("Crm.Actividades.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _crm.CrearActividadAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Actividad creada." : r.Error;
        return Volver(volverA, model.OportunidadCrmId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActividadCompletar(int id, string? resultado, string? volverA, int? oportunidadId, CancellationToken ct)
    {
        if (!Has("Crm.Actividades.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _crm.CompletarActividadAsync(eid, id, new CompletarActividadCrmRequest { Resultado = resultado }, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Actividad completada." : r.Error;
        return Volver(volverA, oportunidadId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActividadCancelar(int id, string? volverA, int? oportunidadId, CancellationToken ct)
    {
        if (!Has("Crm.Actividades.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _crm.CancelarActividadAsync(eid, id, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Actividad cancelada." : r.Error;
        return Volver(volverA, oportunidadId);
    }

    private IActionResult Volver(string? volverA, int? oportunidadId)
        => volverA == "oportunidad" && oportunidadId is int oid
            ? RedirectToAction(nameof(Oportunidad), new { id = oid })
            : RedirectToAction(nameof(Actividades));

    // ── Cotizaciones ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Cotizaciones(string? estado, string? search, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Crm.Cotizaciones.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _crm.ListCotizacionesAsync(eid, estado, null, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ViewBag.Estado = estado;
        ViewBag.Search = search;
        ViewBag.PuedeGestionar = Has("Crm.Cotizaciones.Gestionar");
        return View(r.Value);
    }

    [HttpGet]
    public async Task<IActionResult> NuevaCotizacion(int? oportunidadId, int? clienteId, CancellationToken ct)
    {
        if (!Has("Crm.Cotizaciones.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        await CargarClientesAsync(eid, ct);
        await CargarProductosAsync(eid, ct);
        return View(new CrearCotizacionCrmRequest
        {
            OportunidadCrmId = oportunidadId,
            ClienteId = clienteId,
            Lineas = { new CrearCotizacionCrmLineaRequest() },
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevaCotizacion(CrearCotizacionCrmRequest model, CancellationToken ct)
    {
        if (!Has("Crm.Cotizaciones.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        model.Lineas = (model.Lineas ?? new())
            .Where(l => l.ProductoId.HasValue || (l.PrecioUnitario ?? 0) > 0)
            .ToList();
        if (model.Lineas.Count == 0)
            ModelState.AddModelError(string.Empty, "Agrega al menos una línea con producto o precio.");

        if (!ModelState.IsValid)
        {
            await CargarClientesAsync(eid, ct);
            await CargarProductosAsync(eid, ct);
            return View(model);
        }

        var r = await _crm.CrearCotizacionAsync(eid, model, _currentUser.Username, ct);
        if (r.IsFailure)
        {
            ModelState.AddModelError(string.Empty, r.Error ?? "Error.");
            await CargarClientesAsync(eid, ct);
            await CargarProductosAsync(eid, ct);
            return View(model);
        }
        TempData["Success"] = $"Cotización {r.Value!.Numero} creada.";
        return RedirectToAction(nameof(Cotizacion), new { id = r.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Cotizacion(int id, CancellationToken ct)
    {
        if (!Has("Crm.Cotizaciones.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _crm.GetCotizacionAsync(eid, id, ct);
        if (r.IsFailure) return NotFound();
        ViewBag.PuedeGestionar = Has("Crm.Cotizaciones.Gestionar");
        ViewBag.PuedeEmitirDte = Has("DTE.Emitir");
        return View(r.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CotizacionEstado(int id, string estadoCodigo, CancellationToken ct)
    {
        if (!Has("Crm.Cotizaciones.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _crm.CambiarEstadoCotizacionAsync(eid, id, new CambiarEstadoCotizacionRequest { EstadoCodigo = estadoCodigo }, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? $"Cotización marcada {estadoCodigo}." : r.Error;
        return RedirectToAction(nameof(Cotizacion), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CotizacionConvertir(int id, string tipoDteCodigo, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _crm.ConvertirCotizacionADteAsync(eid, id, new ConvertirCotizacionRequest { TipoDteCodigo = tipoDteCodigo }, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess
            ? $"Cotización convertida: DTE #{r.Value!.DteDocumentoId}."
            : r.Error;
        return RedirectToAction(nameof(Cotizacion), new { id });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task CargarRefsAsync(int eid, CancellationToken ct)
    {
        await CargarClientesAsync(eid, ct);
        var contactos = await _crm.ListContactosAsync(eid, new PagedQuery { Page = 1, PageSize = 500 }, ct);
        ViewBag.Contactos = contactos.Value?.Items ?? new List<ContactoCrmDto>();
        var etapas = await _crm.ListEtapasAsync(eid, ct);
        ViewBag.Etapas = etapas.Value ?? new List<EtapaPipelineCrmDto>();
    }

    private async Task CargarClientesAsync(int eid, CancellationToken ct)
    {
        var clientes = await _clientes.GetListAsync(eid, new PagedQuery { Page = 1, PageSize = 1000 }, ct);
        ViewBag.Clientes = clientes.Value?.Items ?? new List<NeoSTP.Application.Clientes.Dtos.ClienteDto>();
    }

    private async Task CargarProductosAsync(int eid, CancellationToken ct)
    {
        var productos = await _productos.GetListAsync(eid, new PagedQuery { Page = 1, PageSize = 1000 }, ct);
        ViewBag.Productos = productos.Value?.Items
            .Where(p => p.EstadoCodigo == "ACTIVO").ToList() ?? new List<NeoSTP.Application.Productos.Dtos.ProductoDto>();
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "El CRM opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
