using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
[RequireModulo("NEORRHH")]
public sealed class PrestacionesController : Controller
{
    private readonly IPrestacionesRrhhService _prestaciones;
    private readonly IEmpleadosService _empleados;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public PrestacionesController(
        IPrestacionesRrhhService prestaciones,
        IEmpleadosService empleados,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _prestaciones = prestaciones;
        _empleados = empleados;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? anio, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Ver")) return Forbid();
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        var year = anio ?? DateTime.UtcNow.Year;
        var politica = await _prestaciones.GetPoliticaAsync(empresaId, ct);
        var vacaciones = await _prestaciones.ListVacacionesAsync(
            empresaId, null, null, new PagedQuery { Page = 1, PageSize = 100 }, ct);
        var aguinaldos = await _prestaciones.ListAguinaldosAsync(empresaId, year, ct);
        var empleados = await _empleados.GetListAsync(
            empresaId, new PagedQuery { Page = 1, PageSize = 200 }, ct);

        return View(new PrestacionesViewModel
        {
            Anio = year,
            PuedeGestionar = Has("Rrhh.Nomina.Gestionar"),
            Politica = politica.Value!,
            Vacaciones = vacaciones.Value?.Items ?? [],
            Aguinaldos = aguinaldos.Value ?? [],
            Empleados = empleados.Value?.Items.Where(x => x.EstadoCodigo == "ACTIVO").ToList() ?? [],
            NuevaVacacion = new CrearSolicitudVacacionRequest
            {
                FechaInicio = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                FechaFin = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Solicitar(CrearSolicitudVacacionRequest request, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        var result = await _prestaciones.SolicitarVacacionAsync(empresaId, request, _currentUser.Username, ct);
        Flash(result.IsSuccess, result.IsSuccess ? "Solicitud de vacaciones registrada." : result.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarVacacion(int id, string? nota, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        var result = await _prestaciones.AprobarVacacionAsync(
            empresaId, id, new ResolverSolicitudVacacionRequest { Nota = nota }, _currentUser.Username, ct);
        Flash(result.IsSuccess, result.IsSuccess ? "Vacaciones aprobadas." : result.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarVacacion(int id, string? nota, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        var result = await _prestaciones.RechazarVacacionAsync(
            empresaId, id, new ResolverSolicitudVacacionRequest { Nota = nota }, _currentUser.Username, ct);
        Flash(result.IsSuccess, result.IsSuccess ? "Solicitud rechazada." : result.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarVacacion(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        var result = await _prestaciones.CancelarVacacionAsync(empresaId, id, _currentUser.Username, ct);
        Flash(result.IsSuccess, result.IsSuccess ? "Vacaciones canceladas." : result.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CalcularAguinaldos(int anio, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        var result = await _prestaciones.CalcularAguinaldosAsync(empresaId, anio, _currentUser.Username, ct);
        Flash(result.IsSuccess, result.IsSuccess ? $"Aguinaldos {anio} calculados." : result.Error);
        return RedirectToAction(nameof(Index), new { anio });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprobarAguinaldos(int anio, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        var result = await _prestaciones.AprobarAguinaldosAsync(empresaId, anio, _currentUser.Username, ct);
        Flash(result.IsSuccess, result.IsSuccess ? $"{result.Value} aguinaldos aprobados." : result.Error);
        return RedirectToAction(nameof(Index), new { anio });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarPolitica(UpdatePoliticaPrestacionesRequest request, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        var result = await _prestaciones.UpdatePoliticaAsync(empresaId, request, _currentUser.Username, ct);
        Flash(result.IsSuccess, result.IsSuccess ? "Política de prestaciones actualizada." : result.Error);
        return RedirectToAction(nameof(Index));
    }

    private void Flash(bool success, string? message)
        => TempData[success ? "Success" : "Error"] = message ?? "No fue posible completar la operación.";

    private bool Has(string code) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(code);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "NeoRRHH opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
