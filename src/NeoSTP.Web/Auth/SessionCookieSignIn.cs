using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Dtos;

namespace NeoSTP.Web.Auth;

public static class SessionCookieSignIn
{
    public static Task SignInAsync(HttpContext context, UserInfo user, bool persistent = false, DateTimeOffset? expiresUtc = null)
    {
        var restricted = user.SessionPurpose != SessionClaims.Full;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(CookieCurrentUser.ClaimTipoUsuario, user.TipoUsuarioCodigo),
            new(SessionClaims.Id, user.SessionId.ToString()),
            new(SessionClaims.Purpose, user.SessionPurpose)
        };
        if (user.EmpresaId is not null) claims.Add(new(CookieCurrentUser.ClaimEmpresaId, user.EmpresaId.Value.ToString()));
        foreach (var role in user.Roles) claims.Add(new(ClaimTypes.Role, role));
        foreach (var permission in user.Permisos) claims.Add(new(CookieCurrentUser.ClaimPermiso, permission));

        var expiry = restricted ? DateTimeOffset.UtcNow.AddMinutes(10) : expiresUtc ?? DateTimeOffset.UtcNow.AddHours(8);
        if (user.SessionExpiresAt != default && user.SessionExpiresAt < expiry.UtcDateTime)
            expiry = new DateTimeOffset(DateTime.SpecifyKind(user.SessionExpiresAt, DateTimeKind.Utc));
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = persistent && !restricted, ExpiresUtc = expiry, AllowRefresh = !restricted });
    }
}
