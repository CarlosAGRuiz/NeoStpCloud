using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NeoSTP.Application.Auth;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Web.Auth;

/// <summary>
/// Registra los esquemas OIDC de SSO (E3) junto al cookie principal. El SaaS usa una
/// sola app multi-tenant de Entra y un cliente de Google; el retorno del IdP aterriza
/// en un cookie intermedio "External" que <c>AccountController.ExternalCallback</c>
/// traduce a la sesión local. Sin <c>Sso:Enabled</c> no se registra nada.
/// </summary>
public static class SsoAuthenticationExtensions
{
    public const string ExternalScheme = "External";
    public const string MicrosoftScheme = "Microsoft";
    public const string GoogleScheme = "Google";

    /// <summary>Nombre de esquema OIDC para el código de proveedor (ENTRA/GOOGLE), o null si no aplica.</summary>
    public static string? EsquemaDeProveedor(string proveedor) => proveedor switch
    {
        SsoProveedores.Entra => MicrosoftScheme,
        SsoProveedores.Google => GoogleScheme,
        _ => null,
    };

    public static AuthenticationBuilder AddNeoStpSso(this AuthenticationBuilder builder, SsoOptions sso)
    {
        if (!sso.Enabled) return builder;

        // Cookie intermedio para el ida y vuelta con el IdP (cross-site → SameSite=None).
        builder.AddCookie(ExternalScheme, o =>
        {
            o.Cookie.Name = "NeoStp.External";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.None;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        });

        if (sso.Microsoft.IsConfigured)
        {
            builder.AddOpenIdConnect(MicrosoftScheme, options =>
            {
                options.SignInScheme = ExternalScheme;
                options.Authority = sso.Microsoft.Authority;
                options.ClientId = sso.Microsoft.ClientId;
                options.ClientSecret = sso.Microsoft.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.CallbackPath = "/signin-microsoft";
                options.SignedOutCallbackPath = "/signout-microsoft";
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                // App multi-tenant: el issuer varía por directorio; la restricción real
                // se hace por empresa con EmpresaSso.TenantIdExterno.
                options.TokenValidationParameters.ValidateIssuer = false;
            });
        }

        if (sso.Google.IsConfigured)
        {
            builder.AddOpenIdConnect(GoogleScheme, options =>
            {
                options.SignInScheme = ExternalScheme;
                options.Authority = sso.Google.Authority;
                options.ClientId = sso.Google.ClientId;
                options.ClientSecret = sso.Google.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.CallbackPath = "/signin-google";
                options.SignedOutCallbackPath = "/signout-google";
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
            });
        }

        return builder;
    }
}
