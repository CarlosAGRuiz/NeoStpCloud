using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Centro de alertas y notificaciones (B-4). UI web sobre IAlertaService.
/// Cada usuario autenticado gestiona las alertas de su empresa; SuperAdmin debe seleccionar
/// empresa en modo soporte. Sin permiso dedicado (igual que la API).
/// </summary>
[Authorize]
[Route("[controller]")]
public class AlertasController : Controller
{
    public static readonly string[] Estados = ["PENDIENTE", "LEIDA", "RESUELTA"];

    private readonly IAlertaService _alertas;
    private readonly IAlertaGeneracionService _generacion;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public AlertasController(
        IAlertaService alertas,
        IAlertaGeneracionService generacion,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _alertas = alertas;
        _generacion = generacion;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? estado, string? tipo, int page = 1, CancellationToken ct = default)
    {
        if (!Ctx(out var eid, out var uid)) return RedirectToSoporte();

        var query = new AlertaQuery
        {
            EstadoCodigo = string.IsNullOrWhiteSpace(estado) ? null : estado,
            TipoCodigo = string.IsNullOrWhiteSpace(tipo) ? null : tipo,
            Page = page <= 0 ? 1 : page,
            PageSize = 20,
        };
        var result = await _alertas.ListarAsync(eid, uid, query, ct);
        if (result.IsFailure) TempData["Error"] = result.Error;

        return View(new AlertasIndexViewModel
        {
            Alertas = result.Value ?? Application.Common.PagedResult<AlertaDto>.Create(Array.Empty<AlertaDto>(), 0, query.Page, query.PageSize),
            Resumen = await _alertas.ResumenAsync(eid, uid, ct),
            Estado = estado,
            Tipo = tipo,
            Page = query.Page,
            Estados = Estados,
        });
    }

    [HttpPost("{id:int}/Leer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leer(int id, CancellationToken ct)
    {
        if (!Ctx(out var eid, out var uid)) return RedirectToSoporte();
        var result = await _alertas.MarcarLeidaAsync(eid, uid, id, ct);
        if (result.IsFailure) TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/Resolver")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolver(int id, CancellationToken ct)
    {
        if (!Ctx(out var eid, out var uid)) return RedirectToSoporte();
        var result = await _alertas.ResolverAsync(eid, uid, id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Alerta resuelta." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("LeerTodas")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LeerTodas(CancellationToken ct)
    {
        if (!Ctx(out var eid, out var uid)) return RedirectToSoporte();
        var result = await _alertas.MarcarTodasLeidasAsync(eid, uid, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Alertas marcadas como leídas." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Generar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(CancellationToken ct)
    {
        if (!Ctx(out var eid, out _)) return RedirectToSoporte();
        var creadas = await _generacion.GenerarAsync(eid, ct);
        TempData["Success"] = creadas > 0 ? $"Se generaron {creadas} alerta(s) nueva(s)." : "No hay alertas nuevas.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Preferencias")]
    public async Task<IActionResult> Preferencias(CancellationToken ct)
    {
        if (!Ctx(out var eid, out var uid)) return RedirectToSoporte();
        return View(await _alertas.GetPreferenciasAsync(eid, uid, ct));
    }

    [HttpPost("Preferencias")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preferencias(PreferenciaNotificacionDto model, CancellationToken ct)
    {
        if (!Ctx(out var eid, out var uid)) return RedirectToSoporte();
        var result = await _alertas.GuardarPreferenciasAsync(eid, uid, model, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Preferencias guardadas." : result.Error;
        return RedirectToAction(nameof(Preferencias));
    }

    /// <summary>Resuelve empresa (contexto) + usuario actual. False si falta empresa o usuario.</summary>
    private bool Ctx(out int empresaId, out int usuarioId)
    {
        empresaId = 0; usuarioId = 0;
        if (_empresaContext.CurrentEmpresaId is not int eid) return false;
        if (_currentUser.UserId is not int uid) return false;
        empresaId = eid; usuarioId = uid;
        return true;
    }

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN" && _empresaContext.CurrentEmpresaId is null)
        {
            TempData["Error"] = "Las alertas operan dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}

public class AlertasIndexViewModel
{
    public Application.Common.PagedResult<AlertaDto> Alertas { get; set; }
        = Application.Common.PagedResult<AlertaDto>.Create(Array.Empty<AlertaDto>(), 0, 1, 20);
    public AlertaResumenDto Resumen { get; set; } = new();
    public string? Estado { get; set; }
    public string? Tipo { get; set; }
    public int Page { get; set; } = 1;
    public string[] Estados { get; set; } = Array.Empty<string>();
}
