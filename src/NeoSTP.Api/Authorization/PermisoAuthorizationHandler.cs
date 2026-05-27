using Microsoft.AspNetCore.Authorization;
using NeoSTP.Infrastructure.Auth;

namespace NeoSTP.Api.Authorization;

public class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (context.User.IsInRole("SUPERADMIN"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.HasClaim(JwtTokenService.ClaimPermiso, requirement.Codigo))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
