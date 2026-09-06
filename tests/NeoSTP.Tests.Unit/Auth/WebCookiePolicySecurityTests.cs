using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Web.Auth;

namespace NeoSTP.Tests.Unit.Auth;

public sealed class WebCookiePolicySecurityTests
{
    [Theory]
    [InlineData(SsoAuthenticationExtensions.GoogleScheme)]
    [InlineData(SsoAuthenticationExtensions.MicrosoftScheme)]
    public async Task RegisteredPolicyPreservesCrossSiteOidcCookiesAndLocalCsrfProtection(string scheme)
    {
        using var services = Services();
        var oidc = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(scheme);
        var external = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(SsoAuthenticationExtensions.ExternalScheme);
        var antiforgery = services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        var context = Context(services);
        var options = services.GetRequiredService<IOptions<CookiePolicyOptions>>();

        var middleware = new CookiePolicyMiddleware(ctx => {
            ctx.Response.Cookies.Append("qa-nonce", "synthetic", oidc.NonceCookie.Build(ctx));
            ctx.Response.Cookies.Append("qa-correlation", "synthetic", oidc.CorrelationCookie.Build(ctx));
            ctx.Response.Cookies.Append("qa-external", "synthetic", external.Cookie.Build(ctx));
            ctx.Response.Cookies.Append("qa-auth", "synthetic", new CookieOptions { SameSite = SameSiteMode.Lax });
            ctx.Response.Cookies.Append("qa-antiforgery", "synthetic", antiforgery.Cookie.Build(ctx));
            return Task.CompletedTask;
        }, options, NullLoggerFactory.Instance);

        await middleware.Invoke(context);

        oidc.ResponseMode.Should().Be("form_post");
        options.Value.MinimumSameSitePolicy.Should().Be(SameSiteMode.Unspecified);
        var headers = context.Response.Headers.SetCookie.Select(x => x!.ToLowerInvariant()).ToArray();
        headers.Should().HaveCount(5);
        headers.Should().OnlyContain(x => x.Contains("; secure") && x.Contains("; httponly"));
        headers.Single(x => x.StartsWith("qa-nonce=")).Should().Contain("samesite=none");
        headers.Single(x => x.StartsWith("qa-correlation=")).Should().Contain("samesite=none");
        headers.Single(x => x.StartsWith("qa-external=")).Should().Contain("samesite=none");
        headers.Single(x => x.StartsWith("qa-auth=")).Should().Contain("samesite=lax");
        headers.Single(x => x.StartsWith("qa-antiforgery=")).Should().Contain("samesite=strict");
    }

    [Fact]
    public async Task AntiforgeryStillRejectsPostWithoutTokens()
    {
        using var services = Services();
        var context = Context(services);
        context.Request.Method = "POST";
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var validate = () => antiforgery.ValidateRequestAsync(context);
        await validate.Should().ThrowAsync<AntiforgeryValidationException>();
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAntiforgery();
        services.Configure<CookiePolicyOptions>(WebCookiePolicy.Configure);
        services.AddAuthentication().AddNeoStpSso(new SsoOptions {
            Enabled = true,
            Google = new() { Authority = "https://accounts.google.com", ClientId = "synthetic", ClientSecret = "synthetic-only" },
            Microsoft = new() { Authority = "https://login.microsoftonline.com/common/v2.0", ClientId = "synthetic", ClientSecret = "synthetic-only" }
        });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext Context(IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("synthetic-cookie-policy.test");
        return context;
    }
}
