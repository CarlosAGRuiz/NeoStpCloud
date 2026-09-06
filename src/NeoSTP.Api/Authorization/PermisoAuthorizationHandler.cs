using Microsoft.AspNetCore.Authorization;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Application.Auth;

namespace NeoSTP.Api.Authorization;

public class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (SessionClaims.IsPlatformAdministrator(context.User))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.Identity?.IsAuthenticated == true
            && !SessionClaims.IsPlatformPermission(requirement.Codigo)
            && context.User.HasClaim(JwtTokenService.ClaimPermiso, requirement.Codigo))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
