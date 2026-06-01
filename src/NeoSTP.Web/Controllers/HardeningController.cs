using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Ops;

namespace NeoSTP.Web.Controllers;

/// <summary>Panel de hardening (solo SuperAdmin): backups, cuotas e IP allowlist.</summary>
[Authorize]
public class HardeningController : Controller
{
    private readonly IBackupService _backups;
    private readonly IApiQuotaService _quotas;
    private readonly IAdminIpAllowlistService _allowlist;
    private readonly ICurrentUser _currentUser;

    public HardeningController(
        IBackupService backups,
        IApiQuotaService quotas,
        IAdminIpAllowlistService allowlist,
        ICurrentUser currentUser)
    {
        _backups = backups;
        _quotas = quotas;
        _allowlist = allowlist;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!EsAdmin()) return Forbid();

        var vm = new HardeningViewModel
        {
            Backups = await _backups.ListarAsync(25, ct),
            Cuotas = await _quotas.ListarAsync(ct),
            Ips = await _allowlist.ListarAsync(ct),
            PuedeAdministrar = PuedeAdministrar(),
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EjecutarBackup(CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        var r = await _backups.EjecutarBackupAsync(null, "MANUAL", _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess
            ? $"Respaldo #{r.Value!.Id} completado ({r.Value.TamanoBytes} bytes)."
            : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCuota(CrearApiQuotaRequest request, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        var r = await _quotas.CrearAsync(request, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Cuota creada." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarCuota(int id, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        var r = await _quotas.EliminarAsync(id, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Cuota eliminada." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarIp(string ipCidr, string? descripcion, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        var r = await _allowlist.AgregarAsync(ipCidr, descripcion, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "IP agregada a la lista blanca." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleIp(int id, bool activo, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        await _allowlist.ToggleAsync(id, activo, _currentUser.Username, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarIp(int id, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        await _allowlist.EliminarAsync(id, _currentUser.Username, ct);
        return RedirectToAction(nameof(Index));
    }

    private bool EsAdmin() => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso("Ops.Hardening.Ver");
    private bool PuedeAdministrar() => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso("Ops.Hardening.Administrar");
}

public class HardeningViewModel
{
    public IReadOnlyList<BackupJobDto> Backups { get; set; } = Array.Empty<BackupJobDto>();
    public IReadOnlyList<ApiQuotaDto> Cuotas { get; set; } = Array.Empty<ApiQuotaDto>();
    public IReadOnlyList<AdminIpAllowlistDto> Ips { get; set; } = Array.Empty<AdminIpAllowlistDto>();
    public bool PuedeAdministrar { get; set; }
}
