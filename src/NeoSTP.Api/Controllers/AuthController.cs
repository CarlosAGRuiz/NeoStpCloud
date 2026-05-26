using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;

    public AuthController(IAuthService auth, ICurrentUser currentUser)
    {
        _auth = auth;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, BuildContext(), ct);
        return ToActionResult(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(request?.RefreshToken ?? string.Empty, BuildContext(), ct);
        return ToActionResult(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request?.RefreshToken, BuildContext(), ct);
        return Ok(ApiResponse.Ok("Sesión cerrada.", HttpContext.TraceIdentifier));
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

    private AuthContext BuildContext() => new()
    {
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
                => Unauthorized(response),
            "AUTH_BAD_INPUT" => BadRequest(response),
            "AUTH_USER_NOT_FOUND" => NotFound(response),
            _ => BadRequest(response),
        };
    }
}
