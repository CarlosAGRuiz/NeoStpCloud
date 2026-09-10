using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using NeoSTP.Application.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Shared;
using NeoSTP.Infrastructure.Auth;

namespace NeoSTP.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUsuariosService _usuarios;
    private readonly ICurrentUser _currentUser;
    private readonly IMfaService _mfa;

    public AuthController(IAuthService auth, IUsuariosService usuarios, ICurrentUser currentUser, IMfaService mfa)
    {
        _auth = auth;
        _usuarios = usuarios;
        _currentUser = currentUser;
        _mfa = mfa;
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimiting.Login)]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, BuildContext(), ct);
        return ToActionResult(result);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(AuthRateLimiting.Refresh)]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(request?.RefreshToken ?? string.Empty, BuildContext(), ct);
        return ToActionResult(result);
    }

    [HttpPost("logout")]
    [AllowMfaChallenge(SessionClaims.MfaEnroll, SessionClaims.MfaVerify)]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] LogoutRequest? request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request?.RefreshToken, BuildContext(), ct);
        return Ok(ApiResponse.Ok("Sesión cerrada.", HttpContext.TraceIdentifier));
    }

    /// <summary>Empresas donde el usuario puede operar (principal + membresías E1).</summary>
    [HttpGet("empresas")]
    [Authorize]
    public async Task<IActionResult> Empresas(CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId)
            return Unauthorized(ApiResponse.Fail("Sesión inválida.", null, HttpContext.TraceIdentifier));
        var result = await _auth.ListarEmpresasDisponiblesAsync(userId, ct);
        return ToActionResult(result);
    }

    /// <summary>
    /// Cambia la empresa activa: emite un token nuevo con los permisos del rol
    /// del usuario en esa empresa. El token anterior sigue vigente hasta expirar.
    /// </summary>
    [HttpPost("cambiar-empresa")]
    [Authorize]
    public async Task<IActionResult> CambiarEmpresa([FromBody] CambiarEmpresaRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId)
            return Unauthorized(ApiResponse.Fail("Sesión inválida.", null, HttpContext.TraceIdentifier));
        var result = await _auth.CambiarEmpresaAsync(userId, request.EmpresaId, BuildContext(), ct);
        return ToActionResult(result);
    }

    public sealed class CambiarEmpresaRequest
    {
        public int EmpresaId { get; set; }
    }

    [HttpPost("change-password")]
    [EnableRateLimiting(AuthRateLimiting.Login)]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId)
        {
            return Unauthorized(ApiResponse.Fail("No autenticado.", traceId: HttpContext.TraceIdentifier));
        }

        var result = await _usuarios.ChangePasswordAsync(userId, request, _currentUser.Username, ct);
        if (result.IsSuccess)
        {
            return Ok(ApiResponse.Ok("Contraseña cambiada.", HttpContext.TraceIdentifier));
        }

        var payload = ApiResponse.Fail(result.Error ?? "Error", result.ValidationErrors, HttpContext.TraceIdentifier);
        return result.ErrorCode switch
        {
            "PWD_INVALID" => Unauthorized(payload),
            "PWD_WEAK" or "VALIDATION" => BadRequest(payload),
            _ => BadRequest(payload),
        };
    }

    [HttpPost("mfa/enroll")]
    [AllowMfaChallenge(SessionClaims.MfaEnroll)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EnableRateLimiting(AuthRateLimiting.Mfa)]
    [Authorize]
    public async Task<IActionResult> MfaEnroll(CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId)
            return Unauthorized(ApiResponse.Fail("No autenticado.", traceId: HttpContext.TraceIdentifier));

        var result = await _mfa.IniciarEnrolamientoAsync(userId, ct);
        return ToActionResult(result);
    }

    [HttpPost("mfa/confirm")]
    [AllowMfaChallenge(SessionClaims.MfaEnroll)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [EnableRateLimiting(AuthRateLimiting.Mfa)]
    [Authorize]
    public async Task<IActionResult> MfaConfirm([FromBody] MfaCodeRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId)
            return Unauthorized(ApiResponse.Fail("No autenticado.", traceId: HttpContext.TraceIdentifier));

        var result = await _mfa.ConfirmarEnrolamientoAsync(userId, request?.Code ?? string.Empty, BuildContext(), ct);
        return ToActionResult(result);
    }

    [HttpPost("mfa/disable")]
    [EnableRateLimiting(AuthRateLimiting.Mfa)]
    [Authorize]
    public async Task<IActionResult> MfaDisable([FromBody] MfaCodeRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId)
            return Unauthorized(ApiResponse.Fail("No autenticado.", traceId: HttpContext.TraceIdentifier));

        var result = await _mfa.DeshabilitarAsync(userId, request?.Code ?? string.Empty, BuildContext(), ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Ok("Segundo factor deshabilitado.", HttpContext.TraceIdentifier));

        return BadRequest(ApiResponse.Fail(result.Error ?? "Error", result.ValidationErrors, HttpContext.TraceIdentifier));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId)
        {
            return Unauthorized(ApiResponse.Fail("No autenticado.", traceId: HttpContext.TraceIdentifier));
        }

        var result = await _auth.GetCurrentUserInfoAsync(userId, ct);
        return ToActionResult(result);
    }

    [HttpPost("mfa/verify")]
    [Authorize]
    [AllowMfaChallenge(SessionClaims.MfaVerify)]
    [EnableRateLimiting(AuthRateLimiting.Mfa)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MfaVerify([FromBody] MfaCodeRequest request, CancellationToken ct)
        => ToActionResult(await _auth.VerifyMfaChallengeAsync(request?.Code ?? string.Empty, BuildContext(), ct));

    private AuthContext BuildContext() => new()
    {
        SessionId = Guid.TryParse(User.FindFirstValue(SessionClaims.Id), out var sessionId) ? sessionId : null,
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent = Request.Headers.UserAgent.ToString(),
        TraceId = HttpContext.TraceIdentifier,
    };

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<T>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
        }

        var response = ApiResponse<T>.Fail(result.Error ?? "Error", result.ValidationErrors, HttpContext.TraceIdentifier);
        return result.ErrorCode switch
        {
            "AUTH_INVALID_CREDENTIALS" or "AUTH_USER_INACTIVE" or "AUTH_USER_LOCKED" or "AUTH_REFRESH_INVALID"
                or "AUTH_REFRESH_CONTEXT_REQUIRED" or "AUTH_INVALID_CONTEXT" or "AUTH_USER_DISABLED"
                or "AUTH_SESSION_INVALID"
                => Unauthorized(response),
            "EMPRESA_NO_MEMBRESIA" or "EMPRESA_SUSPENDIDA" => StatusCode(StatusCodes.Status403Forbidden, response),
            "AUTH_BAD_INPUT" => BadRequest(response),
            "AUTH_USER_NOT_FOUND" => NotFound(response),
            _ => BadRequest(response),
        };
    }
}
