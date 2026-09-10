using Microsoft.AspNetCore.Http;
using NeoSTP.Application.Auth;
using NeoSTP.Shared;

namespace NeoSTP.Infrastructure.Auth;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowMfaChallengeAttribute(params string[] purposes) : Attribute
{
    public bool Allows(string purpose) => purposes.Contains(purpose, StringComparer.Ordinal);
}

/// <summary>Restricted credentials cannot reach business endpoints, even those allowing anonymous access.</summary>
public sealed class MfaChallengeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var purpose = context.User.FindFirst(SessionClaims.Purpose)?.Value;
        if (!SessionClaims.IsRestricted(context.User)
            || context.GetEndpoint()?.Metadata.GetMetadata<AllowMfaChallengeAttribute>()?.Allows(purpose!) == true)
        {
            await next(context);
            return;
        }
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.Path.StartsWithSegments("/api") && HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.Redirect(purpose == SessionClaims.MfaEnroll
                ? "/Account/MfaEnrollment" : "/Account/MfaVerification");
            return;
        }
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
            purpose == SessionClaims.MfaEnroll
                ? "Configura el segundo factor antes de continuar."
                : "Verifica el segundo factor antes de continuar.",
            traceId: context.TraceIdentifier), context.RequestAborted);
    }
}
