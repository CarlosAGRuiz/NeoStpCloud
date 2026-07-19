using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Web.Auth;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly IUsuariosService _usuarios;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAuthService auth, IUsuariosService usuarios, ICurrentUser currentUser, ILogger<AccountController> logger)
    {
        _auth = auth;
        _usuarios = usuarios;
        _currentUser = currentUser;
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
        foreach (var rol in user.Roles) claims.Add(new Claim(ClaimTypes.Role, rol));
        foreach (var permiso in user.Permisos) claims.Add(new Claim(CookieCurrentUser.ClaimPermiso, permiso));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

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
}
