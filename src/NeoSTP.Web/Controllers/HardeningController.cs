using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Ops;

namespace NeoSTP.Web.Controllers;

/// <summary>Panel de hardening (solo SuperAdmin): backups, cuotas, IP allowlist y diagnóstico de correo.</summary>
[Authorize]
public class HardeningController : Controller
{
    private readonly IBackupService _backups;
    private readonly IApiQuotaService _quotas;
    private readonly IAdminIpAllowlistService _allowlist;
    private readonly ICurrentUser _currentUser;
    private readonly IEmailSender _email;
    private readonly EmailOptions _emailOptions;

    public HardeningController(
        IBackupService backups,
        IApiQuotaService quotas,
        IAdminIpAllowlistService allowlist,
        ICurrentUser currentUser,
        IEmailSender email,
        IOptions<EmailOptions> emailOptions)
    {
        _backups = backups;
        _quotas = quotas;
        _allowlist = allowlist;
        _currentUser = currentUser;
        _email = email;
        _emailOptions = emailOptions.Value;
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
            EmailProvider = _emailOptions.Provider,
            EmailFrom = $"{_emailOptions.From.DisplayName} <{_emailOptions.From.Address}>",
            EmailDestino = _emailOptions.Smtp.Host is { Length: > 0 } h ? $"{h}:{_emailOptions.Smtp.Port}" : _emailOptions.MockOutbox,
        };
        return View(vm);
    }

    /// <summary>Envía un correo de prueba para validar la configuración de correo (Mock o SMTP).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProbarCorreo(string destinatario, CancellationToken ct)
    {
        if (!PuedeAdministrar()) return Forbid();
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            TempData["Error"] = "Indica un correo destinatario para la prueba.";
            return RedirectToAction(nameof(Index));
        }

        var message = new EmailMessage
        {
            To = destinatario.Trim(),
            Subject = "Prueba de correo · NeoSTP Cloud",
            HtmlBody = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;color:#1e293b">
                  <h2 style="color:#131b2e;margin:0 0 8px">NeoSTP Cloud — correo de prueba</h2>
                  <p>Este es un correo de prueba enviado desde el panel de operación.</p>
                  <p style="color:#64748b;font-size:13px">Proveedor: <strong>{_emailOptions.Provider}</strong> ·
                     Fecha: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC ·
                     Enviado por: {_currentUser.Username}</p>
                  <p style="color:#64748b;font-size:13px">Si recibes este mensaje, la configuración de correo está operativa.</p>
                </div>
                """,
        };

        var result = await _email.EnviarAsync(message, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? $"Correo de prueba enviado a {destinatario} ({_emailOptions.Provider}). {result.Detalle}"
            : $"Falló el envío [{result.Mensaje}]: {result.Detalle}";
        return RedirectToAction(nameof(Index));
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

    // Diagnóstico de correo
    public string EmailProvider { get; set; } = "Mock";
    public string EmailFrom { get; set; } = string.Empty;
    public string EmailDestino { get; set; } = string.Empty;
}
