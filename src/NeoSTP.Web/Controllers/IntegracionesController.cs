using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Empresas;
using NeoSTP.Domain.Core.Connect;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// NeoConnect — gestión de API Keys, webhooks y consumo de la API para integradores.
/// Opera dentro de una empresa (igual que Clientes/Productos): SuperAdmin debe entrar en modo soporte.
/// </summary>
[Authorize]
public class IntegracionesController : Controller
{
    private readonly IConnectApiKeyService _apiKeys;
    private readonly IConnectWebhookService _webhooks;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public IntegracionesController(
        IConnectApiKeyService apiKeys,
        IConnectWebhookService webhooks,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _apiKeys = apiKeys;
        _webhooks = webhooks;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("Connect.ApiKeys.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var vm = new IntegracionesViewModel
        {
            ApiKeys = await _apiKeys.ListarAsync(eid, ct),
            Webhooks = await _webhooks.ListarAsync(eid, ct),
            Deliveries = await _webhooks.ListarDeliveriesAsync(eid, 25, ct),
            Usage = await _webhooks.ObtenerUsageAsync(eid, ct),
            PuedeAdministrarKeys = Has("Connect.ApiKeys.Administrar"),
            PuedeAdministrarWebhooks = Has("Connect.Webhooks.Administrar"),
            ScopesDisponibles = ConnectScopes.All,
            EventosDisponibles = ConnectEventos.All,
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearApiKey(string nombre, string[] scopes, CancellationToken ct)
    {
        if (!Has("Connect.ApiKeys.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var req = new CrearApiKeyRequest { EmpresaId = eid, Nombre = nombre, Scopes = scopes ?? [] };
        var r = await _apiKeys.CrearAsync(req, _currentUser.Username, ct);

        if (r.IsSuccess)
        {
            // La raw key se muestra UNA sola vez tras la creación.
            TempData["RawKey"] = r.Value!.RawKey;
            TempData["RawKeyNombre"] = r.Value.Key.Nombre;
            TempData["Success"] = "API Key creada. Cópiala ahora — no se volverá a mostrar.";
        }
        else
        {
            TempData["Error"] = r.Error;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevocarApiKey(int id, CancellationToken ct)
    {
        if (!Has("Connect.ApiKeys.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _apiKeys.RevocarAsync(id, eid, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "API Key revocada." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearWebhook(string url, string[] eventos, CancellationToken ct)
    {
        if (!Has("Connect.Webhooks.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var req = new CrearWebhookRequest { EmpresaId = eid, Url = url, Eventos = eventos ?? [] };
        var r = await _webhooks.CrearAsync(req, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Webhook creado." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarWebhook(int id, CancellationToken ct)
    {
        if (!Has("Connect.Webhooks.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _webhooks.EliminarAsync(id, eid, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Webhook eliminado." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestWebhook(int id, CancellationToken ct)
    {
        if (!Has("Connect.Webhooks.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _webhooks.TestAsync(id, eid, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess
            ? $"Test enviado correctamente (HTTP {r.Value!.HttpStatus})."
            : r.Error;
        return RedirectToAction(nameof(Index));
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Esta pantalla opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}

public class IntegracionesViewModel
{
    public IReadOnlyList<ConnectApiKeyDto> ApiKeys { get; set; } = Array.Empty<ConnectApiKeyDto>();
    public IReadOnlyList<ConnectWebhookDto> Webhooks { get; set; } = Array.Empty<ConnectWebhookDto>();
    public IReadOnlyList<ConnectWebhookDeliveryDto> Deliveries { get; set; } = Array.Empty<ConnectWebhookDeliveryDto>();
    public IReadOnlyList<ConnectUsageDto> Usage { get; set; } = Array.Empty<ConnectUsageDto>();
    public bool PuedeAdministrarKeys { get; set; }
    public bool PuedeAdministrarWebhooks { get; set; }
    public string[] ScopesDisponibles { get; set; } = Array.Empty<string>();
    public string[] EventosDisponibles { get; set; } = Array.Empty<string>();
}
