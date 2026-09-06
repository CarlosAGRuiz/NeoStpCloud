using Microsoft.AspNetCore.CookiePolicy;

namespace NeoSTP.Web.Auth;

public static class WebCookiePolicy
{
    public static void Configure(CookiePolicyOptions options)
    {
        // Preserve the cookie-specific policy: auth=Lax, antiforgery=Strict,
        // OIDC nonce/correlation/external=None for cross-site form_post callbacks.
        options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
        options.Secure = CookieSecurePolicy.Always;
        options.HttpOnly = HttpOnlyPolicy.Always;
    }
}
