using Microsoft.AspNetCore.Authentication.JwtBearer;
using NeoSTP.Application.Auth.Abstractions;

namespace NeoSTP.Api.Auth;

public sealed class SessionJwtEvents(IAuthSessionService sessions) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var result = await sessions.ValidatePrincipalAsync(context.Principal!, context.HttpContext.RequestAborted);
        if (result.IsFailure) context.Fail("La sesión ya no es válida. Inicia sesión nuevamente.");
    }
}
