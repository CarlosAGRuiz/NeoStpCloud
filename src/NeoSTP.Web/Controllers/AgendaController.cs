using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Agenda;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Productos;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOAGENDA â€” calendario semanal de citas y comisiones por servicio.</summary>
[Authorize]
[RequireModulo("NEOAGENDA")]
public class AgendaController : Controller
{
    private readonly IAgendaService _agenda;
    private readonly IProductosService _productos;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;
    private readonly NeoSTP.Infrastructure.Persistence.NeoStpDbContext _db;

    public AgendaController(
        IAgendaService agenda,
        IProductosService productos,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext,
        NeoSTP.Infrastructure.Persistence.NeoStpDbContext db)
    {
        _agenda = agenda;
        _productos = productos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? fecha, int? empleadoId, CancellationToken ct)
    {
        if (!Has("Agenda.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var referencia = fecha ?? DateOnly.FromDateTime(DateTime.Now);
        // Semana lunes-domingo que contiene la fecha.
        var lunes = referencia.AddDays(-(((int)referencia.DayOfWeek + 6) % 7));
        var desde = lunes.ToDateTime(TimeOnly.MinValue);
        var hasta = lunes.AddDays(7).ToDateTime(TimeOnly.MinValue);

        var citas = await _agenda.ListAsync(eid, desde, hasta, empleadoId, ct);
        ViewBag.Lunes = lunes;
        ViewBag.EmpleadoId = empleadoId;
        ViewBag.PuedeGestionar = Has("Agenda.Gestionar");
        await CargarFormDataAsync(eid, ct);
        return View(citas.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearCitaRequest model, DateOnly? fecha, CancellationToken ct)
    {
        if (!Has("Agenda.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _agenda.CrearAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess
            ? $"Cita de {r.Value!.ClienteNombre} agendada para el {r.Value.FechaInicio:dd/MM HH:mm}."
            : r.Error;
        return RedirectToAction(nameof(Index), new { fecha });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Estado(int id, string estado, DateOnly? fecha, CancellationToken ct)
    {
        if (!Has("Agenda.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _agenda.CambiarEstadoAsync(eid, id, estado, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Estado actualizado." : r.Error;
        return RedirectToAction(nameof(Index), new { fecha });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprogramar(int id, DateTime fechaInicio, int? duracionMinutos, DateOnly? fecha, CancellationToken ct)
    {
        if (!Has("Agenda.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _agenda.ReprogramarAsync(eid, id, fechaInicio, duracionMinutos, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Cita reprogramada." : r.Error;
        return RedirectToAction(nameof(Index), new { fecha });
    }

    [HttpGet]
    public async Task<IActionResult> Comisiones(DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        if (!Has("Agenda.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var d = desde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var h = hasta ?? hoy;
        var r = await _agenda.ComisionesAsync(eid, d, h, ct);
        ViewBag.Desde = d;
        ViewBag.Hasta = h;
        return View(r.Value);
    }

    private async Task CargarFormDataAsync(int eid, CancellationToken ct)
    {
        ViewBag.Clientes = await _db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == eid && c.EstadoCodigo == "ACTIVO")
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre })
            .ToListAsync(ct);

        ViewBag.Empleados = await _db.Empleados.AsNoTracking()
            .Where(e => e.EmpresaId == eid && e.FechaEgreso == null)
            .OrderBy(e => e.Nombres)
            .Select(e => new { e.Id, Nombre = e.Nombres + " " + e.Apellidos })
            .ToListAsync(ct);

        var servicios = await _productos.GetListAsync(eid, new PagedQuery { Page = 1, PageSize = 500 }, ct: ct);
        ViewBag.Servicios = (servicios.Value?.Items ?? [])
            .Where(p => p.EstadoCodigo == "ACTIVO" && p.EsServicio)
            .Select(p => new { p.Id, p.Nombre, p.PrecioUnitario })
            .ToList();
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "La agenda opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
