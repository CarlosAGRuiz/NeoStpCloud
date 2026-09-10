using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Web.Auth;

public static class ExternalIdentityReader
{
    // Only call with the protected External cookie issued by the OIDC middleware.
    public static ExternalLoginInfo? Read(AuthenticateResult result)
    {
        if (!result.Succeeded || result.Principal is not { } user) return null;
        var scheme = result.Properties?.Items.TryGetValue(".AuthScheme", out var value) == true ? value : null;
        var provider = scheme switch
        {
            SsoAuthenticationExtensions.MicrosoftScheme => SsoProveedores.Entra,
            SsoAuthenticationExtensions.GoogleScheme => SsoProveedores.Google,
            _ => null
        };
        if (provider is null) return null;
        var subject = user.FindFirstValue(provider == SsoProveedores.Entra ? "oid" : "sub");
        var issuer = user.FindFirstValue("iss");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer)) return null;
        return new ExternalLoginInfo
        {
            Proveedor = provider, Subject = subject, Issuer = issuer,
            Email = user.FindFirstValue("email") ?? user.FindFirstValue("preferred_username"),
            NombreCompleto = user.FindFirstValue("name"), TenantIdExterno = user.FindFirstValue("tid"),
            EmailVerified = string.Equals(user.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            HostedDomain = user.FindFirstValue("hd")
        };
    }
}
