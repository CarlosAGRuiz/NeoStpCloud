using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;

namespace NeoSTP.Web.ViewComponents;

/// <summary>
/// Campana de notificaciones del topbar: muestra el conteo de alertas pendientes
/// del usuario actual dentro de su empresa. Best-effort: si no hay contexto o falla,
/// no rompe el layout (devuelve resumen vacío).
/// </summary>
public class AlertasBadgeViewComponent : ViewComponent
{
    private readonly IAlertaService _alertas;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public AlertasBadgeViewComponent(IAlertaService alertas, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _alertas = alertas;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var resumen = new AlertaResumenDto();
        if (_empresaContext.CurrentEmpresaId is int eid && _currentUser.UserId is int uid)
        {
            try { resumen = await _alertas.ResumenAsync(eid, uid); }
            catch { /* no romper el layout por un fallo de notificaciones */ }
        }
        return View(resumen);
    }
}
