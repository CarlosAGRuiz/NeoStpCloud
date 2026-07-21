using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Web.Auth;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

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
    public IActionResult Login(string? returnUrl = null, string? motivo = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
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
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _auth.LoginAsync(
            new LoginRequest { UsernameOrEmail = model.UsernameOrEmail, Password = model.Password },
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
        return RedirectSafe(model.ReturnUrl);
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
        if (scheme is null) return RedirectToAction(nameof(Login));

        var redirectUri = Url.Action(nameof(ExternalCallback), "Account", new { returnUrl });
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, scheme);
    }

    /// <summary>Retorno del proveedor OIDC: traduce la identidad federada a la sesión local (E3).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            TempData["Error"] = $"El proveedor de SSO devolvió un error: {remoteError}";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var auth = await HttpContext.AuthenticateAsync(SsoAuthenticationExtensions.ExternalScheme);
        if (!auth.Succeeded || auth.Principal is null)
        {
            TempData["Error"] = "No se pudo completar el inicio de sesión con SSO.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var scheme = auth.Properties?.Items.TryGetValue(".AuthScheme", out var s) == true ? s : null;
        var proveedor = scheme == SsoAuthenticationExtensions.GoogleScheme
            ? SsoProveedores.Google : SsoProveedores.Entra;

        var principal = auth.Principal;
        // Sujeto estable: "oid" (Entra, por directorio) o "sub"; correo y nombre según el proveedor.
        var subject = principal.FindFirstValue("oid")
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue("email")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("preferred_username");
        var nombre = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name);
        var tid = principal.FindFirstValue("tid");

        await HttpContext.SignOutAsync(SsoAuthenticationExtensions.ExternalScheme);

        if (string.IsNullOrWhiteSpace(subject))
        {
            TempData["Error"] = "El proveedor no entregó una identidad válida.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var login = await _auth.LoginExternoAsync(new ExternalLoginInfo
        {
            Proveedor = proveedor,
            Subject = subject,
            Email = email,
            NombreCompleto = nombre,
            TenantIdExterno = tid,
        }, BuildAuthContext(), HttpContext.RequestAborted);

        if (login.IsFailure)
        {
            TempData["Error"] = login.Error ?? "No se pudo iniciar sesión con SSO.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var user = login.Value!.User;
        await SignInCookieAsync(user, persistent: false);
        _logger.LogInformation("Usuario {Username} (id={Id}) inició sesión por SSO ({Proveedor})", user.Username, user.Id, proveedor);
        return RedirectSafe(returnUrl);
    }

    /// <summary>Cambia la empresa activa (membresías E1): reemite la cookie con los claims de esa empresa.</summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEmpresa(int empresaId, CancellationToken ct)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId)) return RedirectToAction(nameof(Login));

        var result = await _auth.CambiarEmpresaAsync(userId, empresaId, new AuthContext
        {
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            TraceId = HttpContext.TraceIdentifier,
        }, ct);

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
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _auth.LogoutAsync(null, new AuthContext
        {
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            TraceId = HttpContext.TraceIdentifier,
        }, ct);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
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

        TempData["Success"] = "Contraseña cambiada correctamente.";
        return RedirectToAction("Index", "Home");
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
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent = Request.Headers.UserAgent.ToString(),
        TraceId = HttpContext.TraceIdentifier,
    };

    /// <summary>Emite la cookie de sesión local a partir del UserInfo (login normal, SSO y cambio de empresa).</summary>
    private async Task SignInCookieAsync(UserInfo user, bool persistent, DateTimeOffset? expiresUtc = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(CookieCurrentUser.ClaimTipoUsuario, user.TipoUsuarioCodigo),
        };
        if (user.EmpresaId is not null)
        {
            claims.Add(new Claim(CookieCurrentUser.ClaimEmpresaId, user.EmpresaId.Value.ToString()));
        }
        foreach (var rol in user.Roles) claims.Add(new Claim(ClaimTypes.Role, rol));
        foreach (var permiso in user.Permisos) claims.Add(new Claim(CookieCurrentUser.ClaimPermiso, permiso));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties { IsPersistent = persistent };
        if (expiresUtc is not null) props.ExpiresUtc = expiresUtc;
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);
    }
}
