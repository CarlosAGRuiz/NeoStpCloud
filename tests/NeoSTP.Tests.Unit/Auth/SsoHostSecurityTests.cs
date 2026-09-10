using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Web.Auth;
using NeoSTP.Web.Controllers;
using NeoSTP.Web.Models;

namespace NeoSTP.Tests.Unit.Auth;

public class SsoHostSecurityTests
{
    [Theory]
    [InlineData("Microsoft")]
    [InlineData("Google")]
    public void OidcUsesIssuerValidationPkceAndNoTokenPersistence(string scheme)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDataProtection();
        services.AddAuthentication().AddNeoStpSso(new SsoOptions
        {
            Enabled = true,
            Microsoft = new() { Authority = "https://login.microsoftonline.com/organizations/v2.0", ClientId = "synthetic", ClientSecret = "not-a-secret" },
            Google = new() { Authority = "https://accounts.google.com", ClientId = "synthetic", ClientSecret = "not-a-secret" }
        });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(scheme);
        options.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        options.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        options.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
        options.TokenValidationParameters.RequireSignedTokens.Should().BeTrue();
        options.ProtocolValidator.RequireNonce.Should().BeTrue();
        options.UsePkce.Should().BeTrue(); options.SaveTokens.Should().BeFalse();
        options.GetClaimsFromUserInfoEndpoint.Should().BeFalse();
        if (scheme == "Microsoft")
        {
            options.TokenValidationParameters.IssuerValidator.Should().NotBeNull();
            options.TokenValidationParameters.IssuerSigningKeyValidatorUsingConfiguration.Should().NotBeNull();
        }
        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("External");
        cookie.SlidingExpiration.Should().BeFalse();
        cookie.ExpireTimeSpan.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Theory]
    [InlineData(null, true, true, false)]
    [InlineData("unknown", true, true, false)]
    [InlineData("Microsoft", false, true, false)]
    [InlineData("Microsoft", true, false, false)]
    [InlineData("Microsoft", true, true, true)]
    [InlineData("Google", true, true, true)]
    public void ExternalCookieRequiresKnownSchemeIssuerAndImmutableSubject(string? scheme, bool issuer, bool subject, bool valid)
    {
        var claims = new List<Claim> { new("email", "fixture@example.test"), new("tid", SsoTestIdentity.Tenant) };
        if (issuer) claims.Add(new("iss", SsoTestIdentity.Issuer));
        if (subject) claims.Add(new(scheme == "Microsoft" ? "oid" : "sub", "immutable"));
        var properties = new AuthenticationProperties();
        if (scheme is not null) properties.Items[".AuthScheme"] = scheme;
        var result = AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "External")), properties, "External"));
        (ExternalIdentityReader.Read(result) is not null).Should().Be(valid);
    }

    [Fact]
    public void LinkPostRequiresCsrfAndRateLimitAndCannotBindExternalIdentity()
    {
        var post = typeof(AccountController).GetMethod(nameof(AccountController.ExternalLink), [typeof(LoginViewModel), typeof(CancellationToken)])!;
        post.IsDefined(typeof(HttpPostAttribute), true).Should().BeTrue();
        post.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true).Should().BeTrue();
        post.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Cast<EnableRateLimitingAttribute>().Single().PolicyName.Should().Be(AuthRateLimiting.Login);
        typeof(LoginViewModel).GetProperties().Select(p => p.Name).Should().NotContain(["Issuer", "Subject", "Proveedor", "EmpresaId"]);
        typeof(AccountController).GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>().Single().NoStore.Should().BeTrue();
    }
}
