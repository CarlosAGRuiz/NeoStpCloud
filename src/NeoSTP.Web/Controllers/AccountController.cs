using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Web.Auth;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAuthService auth, ILogger<AccountController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectSafe(returnUrl);
        }
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
        foreach (var rol in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, rol));
        }
        foreach (var permiso in user.Permisos)
        {
            claims.Add(new Claim(CookieCurrentUser.ClaimPermiso, permiso));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProps = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8),
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);

        _logger.LogInformation("Usuario {Username} (id={Id}) inició sesión", user.Username, user.Id);
        return RedirectSafe(model.ReturnUrl);
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
}
