using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Ops;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Panel de hardening (SuperAdmin): respaldos, cuotas de API y lista blanca de IP.
/// Permisos: <c>Ops.Hardening.Ver</c> (lectura) y <c>Ops.Hardening.Administrar</c> (operación).
/// </summary>
[Authorize]
[Route("api/hardening")]
public class HardeningController : ApiControllerBase
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

    // ----- Backups -----

    [HttpGet("backups")]
    [RequirePermiso("Ops.Hardening.Ver")]
    public async Task<IActionResult> Backups(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<BackupJobDto>>.Ok(await _backups.ListarAsync(50, ct), traceId: HttpContext.TraceIdentifier));

    [HttpPost("backups/ejecutar")]
    [RequirePermiso("Ops.Hardening.Administrar")]
    public async Task<IActionResult> EjecutarBackup([FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _backups.EjecutarBackupAsync(empresaId, "MANUAL", _currentUser.Username, ct));

    // ----- Cuotas -----

    [HttpGet("cuotas")]
    [RequirePermiso("Ops.Hardening.Ver")]
    public async Task<IActionResult> Cuotas(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ApiQuotaDto>>.Ok(await _quotas.ListarAsync(ct), traceId: HttpContext.TraceIdentifier));

    [HttpPost("cuotas")]
    [RequirePermiso("Ops.Hardening.Administrar")]
    public async Task<IActionResult> CrearCuota([FromBody] CrearApiQuotaRequest request, CancellationToken ct)
        => Respond(await _quotas.CrearAsync(request, _currentUser.Username, ct));

    [HttpDelete("cuotas/{id:int}")]
    [RequirePermiso("Ops.Hardening.Administrar")]
    public async Task<IActionResult> EliminarCuota(int id, CancellationToken ct)
        => Respond(await _quotas.EliminarAsync(id, _currentUser.Username, ct));

    // ----- IP allowlist -----

    [HttpGet("ip-allowlist")]
    [RequirePermiso("Ops.Hardening.Ver")]
    public async Task<IActionResult> IpAllowlist(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<AdminIpAllowlistDto>>.Ok(await _allowlist.ListarAsync(ct), traceId: HttpContext.TraceIdentifier));

    [HttpPost("ip-allowlist")]
    [RequirePermiso("Ops.Hardening.Administrar")]
    public async Task<IActionResult> AgregarIp([FromBody] AgregarIpRequest request, CancellationToken ct)
        => Respond(await _allowlist.AgregarAsync(request.IpCidr, request.Descripcion, _currentUser.Username, ct));

    [HttpPatch("ip-allowlist/{id:int}")]
    [RequirePermiso("Ops.Hardening.Administrar")]
    public async Task<IActionResult> ToggleIp(int id, [FromBody] ToggleIpRequest request, CancellationToken ct)
        => Respond(await _allowlist.ToggleAsync(id, request.Activo, _currentUser.Username, ct));

    [HttpDelete("ip-allowlist/{id:int}")]
    [RequirePermiso("Ops.Hardening.Administrar")]
    public async Task<IActionResult> EliminarIp(int id, CancellationToken ct)
        => Respond(await _allowlist.EliminarAsync(id, _currentUser.Username, ct));
}
