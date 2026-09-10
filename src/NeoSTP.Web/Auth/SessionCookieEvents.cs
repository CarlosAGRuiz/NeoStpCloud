using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NeoSTP.Application.Auth.Abstractions;

namespace NeoSTP.Web.Auth;

public sealed class SessionCookieEvents(IAuthSessionService sessions) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var result = await sessions.ValidatePrincipalAsync(context.Principal!, context.HttpContext.RequestAborted);
        if (result.IsSuccess) return;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
