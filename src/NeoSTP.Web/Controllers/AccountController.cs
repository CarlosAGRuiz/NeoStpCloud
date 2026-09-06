using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NeoSTP.Application.Auth;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Web.Auth;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly IUsuariosService _usuarios;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Auth.SsoOptions _sso;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthService auth,
        IUsuariosService usuarios,
        ICurrentUser currentUser,
        Microsoft.Extensions.Options.IOptions<NeoSTP.Application.Auth.SsoOptions> sso,
        ILogger<AccountController> logger)
    {
        _auth = auth;
        _usuarios = usuarios;
        _currentUser = currentUser;
        _sso = sso.Value;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [AllowMfaChallenge(SessionClaims.MfaEnroll, SessionClaims.MfaVerify)]
    public IActionResult Login(string? returnUrl = null, string? motivo = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (SessionClaims.IsRestricted(User)) return RedirectToMfa(User.FindFirstValue(SessionClaims.Purpose)!);
            return RedirectSafe(returnUrl);
        }
        if (motivo == "suspendida")
        {
            ViewBag.Error = "La empresa está suspendida o inactiva. Regulariza tu suscripción o contacta a soporte.";
        }
        ViewBag.SsoMicrosoft = _sso.Enabled && _sso.Microsoft.IsConfigured;
        ViewBag.SsoGoogle = _sso.Enabled && _sso.Google.IsConfigured;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimiting.Login)]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _auth.LoginAsync(
            new LoginRequest { UsernameOrEmail = model.UsernameOrEmail, Password = model.Password, MfaCode = model.MfaCode?.Trim() },
            new AuthContext
            {
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                TraceId = HttpContext.TraceIdentifier,
            },
            ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "No se pudo iniciar sesión.");
            return View(model);
        }

        var user = result.Value!.User;
        await SignInCookieAsync(user, model.RememberMe,
            model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8));

        _logger.LogInformation("Usuario {Username} (id={Id}) inició sesión", user.Username, user.Id);
        return user.SessionPurpose == SessionClaims.Full ? RedirectSafe(model.ReturnUrl) : RedirectToMfa(user.SessionPurpose);
    }

    /// <summary>Inicia el flujo SSO (E3): redirige al proveedor OIDC (Microsoft/Google).</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var scheme = (provider ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "MICROSOFT" or "ENTRA" => SsoAuthenticationExtensions.MicrosoftScheme,
            "GOOGLE" => SsoAuthenticationExtensions.GoogleScheme,
            _ => null,
        };
        if (!_sso.Enabled || scheme is null
            || scheme == SsoAuthenticationExtensions.MicrosoftScheme && !_sso.Microsoft.IsConfigured
            || scheme == SsoAuthenticationExtensions.GoogleScheme && !_sso.Google.IsConfigured)
            return RedirectToAction(nameof(Login));

        var redirectUri = Url.Action(nameof(ExternalCallback), "Account", new { returnUrl });
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, scheme);
    }

    /// <summary>Retorno del proveedor OIDC: traduce la identidad federada a la sesión local (E3).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!_sso.Enabled) return RedirectToAction(nameof(Login));
        if (!string.IsNullOrEmpty(remoteError))
        {
            TempData["Error"] = "El proveedor de SSO no pudo completar el acceso. Inténtalo nuevamente.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var auth = await HttpContext.AuthenticateAsync(SsoAuthenticationExtensions.ExternalScheme);
        if (!auth.Succeeded || auth.Principal is null)
        {
            TempData["Error"] = "No se pudo completar el inicio de sesión con SSO.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var identity = ExternalIdentityReader.Read(auth);
        if (identity is null)
        {
            await HttpContext.SignOutAsync(SsoAuthenticationExtensions.ExternalScheme);
            TempData["Error"] = "El proveedor no entregó una identidad válida.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var login = await _auth.LoginExternoAsync(identity, BuildAuthContext(), HttpContext.RequestAborted);
        if (login.ErrorCode == "SSO_LINK_REQUIRED") return RedirectToAction(nameof(ExternalLink), new { returnUrl });
        await HttpContext.SignOutAsync(SsoAuthenticationExtensions.ExternalScheme);

        if (login.IsFailure)
        {
            TempData["Error"] = login.Error ?? "No se pudo iniciar sesión con SSO.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var user = login.Value!.User;
        await SignInCookieAsync(user, persistent: false);
        _logger.LogInformation("Usuario {Username} (id={Id}) inició sesión por SSO ({Proveedor})", user.Username, user.Id, identity.Proveedor);
        return user.SessionPurpose == SessionClaims.Full ? RedirectSafe(returnUrl) : RedirectToMfa(user.SessionPurpose);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLink(string? returnUrl = null)
    {
        if (!_sso.Enabled) return RedirectToAction(nameof(Login));
        var info = ExternalIdentityReader.Read(await HttpContext.AuthenticateAsync(SsoAuthenticationExtensions.ExternalScheme));
        if (info is null) return RedirectToAction(nameof(Login));
        return View(new LoginViewModel { UsernameOrEmail = info.Email ?? string.Empty, ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimiting.Login)]
    public async Task<IActionResult> ExternalLink(LoginViewModel model, CancellationToken ct)
    {
        if (!_sso.Enabled) return RedirectToAction(nameof(Login));
        var info = ExternalIdentityReader.Read(await HttpContext.AuthenticateAsync(SsoAuthenticationExtensions.ExternalScheme));
        if (info is null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid) return View(model);
        var result = await _auth.VincularExternoAsync(info, new LoginRequest
        {
            UsernameOrEmail = model.UsernameOrEmail, Password = model.Password, MfaCode = model.MfaCode?.Trim()
        }, BuildAuthContext(), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            model.Password = string.Empty;
            model.MfaCode = null;
            ModelState.Remove(nameof(model.Password));
            ModelState.Remove(nameof(model.MfaCode));
            return View(model);
        }
        await HttpContext.SignOutAsync(SsoAuthenticationExtensions.ExternalScheme);
        await SignInCookieAsync(result.Value!.User, persistent: false);
        return RedirectSafe(model.ReturnUrl);
    }

    /// <summary>Cambia la empresa activa (membresías E1): reemite la cookie con los claims de esa empresa.</summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEmpresa(int empresaId, CancellationToken ct)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId)) return RedirectToAction(nameof(Login));

        var result = await _auth.CambiarEmpresaAsync(userId, empresaId, BuildAuthContext(), ct);

        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return Redirect("/");
        }

        await SignInCookieAsync(result.Value!.User, persistent: false);
        TempData["Success"] = $"Ahora operas en otra empresa.";
        return Redirect("/");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [AllowMfaChallenge(SessionClaims.MfaEnroll, SessionClaims.MfaVerify)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _auth.LogoutAsync(null, BuildAuthContext(), ct);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimiting.Login)]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        if (_currentUser.UserId is not int userId) return Unauthorized();

        var result = await _usuarios.ChangePasswordAsync(userId,
            new ChangePasswordRequest { CurrentPassword = model.CurrentPassword, NewPassword = model.NewPassword },
            _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "No se pudo cambiar la contraseña.");
            return View(model);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Contraseña cambiada. Inicia sesión nuevamente con tu nueva contraseña.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectSafe(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }

    private AuthContext BuildAuthContext() => new()
    {
        SessionId = Guid.TryParse(User.FindFirstValue(SessionClaims.Id), out var sessionId) ? sessionId : null,
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent = Request.Headers.UserAgent.ToString(),
        TraceId = HttpContext.TraceIdentifier,
    };

    /// <summary>Emite la cookie de sesión local a partir del UserInfo (login normal, SSO y cambio de empresa).</summary>
    private async Task SignInCookieAsync(UserInfo user, bool persistent, DateTimeOffset? expiresUtc = null)
        => await SessionCookieSignIn.SignInAsync(HttpContext, user, persistent, expiresUtc);

    private IActionResult RedirectToMfa(string purpose) => Redirect(purpose == SessionClaims.MfaEnroll
        ? "/Account/MfaEnrollment" : "/Account/MfaVerification");
}
