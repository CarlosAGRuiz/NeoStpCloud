using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoSTP.Infrastructure.Auth;

namespace NeoSTP.Tests.Unit.Auth;

public class AuthRateLimitingTests
{
    private static IHost Host(int window = 60)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:AuthRateLimit:WindowSeconds"] = window.ToString(),
            ["Security:AuthRateLimit:LoginPermits"] = "2",
            ["Security:AuthRateLimit:MfaPermits"] = "1",
            ["Security:AuthRateLimit:RefreshPermits"] = "3"
        }).Build();
        return new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddRouting();
            services.AddAuthRateLimiting(config);
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.ForwardLimit = 1;
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
                o.KnownProxies.Add(IPAddress.Loopback);
                o.KnownProxies.Add(IPAddress.IPv6Loopback);
            });
        }).Configure(app =>
        {
            // Test-only transport address. No real sockets, app startup, configuration or database.
            app.Use((context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["Test-Remote-IP"].FirstOrDefault() ?? "203.0.113.10");
                return next(context);
            });
            app.UseForwardedHeaders();
            app.UseRouting();
            app.UseRateLimiter();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/api/auth/login", c => c.Response.WriteAsync("login")).RequireRateLimiting(AuthRateLimiting.Login);
                endpoints.MapPost("/Account/Login", c => c.Response.WriteAsync("login")).RequireRateLimiting(AuthRateLimiting.Login);
                endpoints.MapPost("/api/auth/mfa/enroll", c => c.Response.WriteAsync("mfa")).RequireRateLimiting(AuthRateLimiting.Mfa);
                endpoints.MapPost("/api/auth/mfa/confirm", c => c.Response.WriteAsync("mfa")).RequireRateLimiting(AuthRateLimiting.Mfa);
                endpoints.MapPost("/api/auth/refresh", c => c.Response.WriteAsync("refresh")).RequireRateLimiting(AuthRateLimiting.Refresh);
                endpoints.MapGet("/health", c => c.Response.WriteAsync("ok"));
            });
        })).Start();
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string path = "/api/auth/login",
        string? ip = null, string? forwarded = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (ip is not null) request.Headers.Add("Test-Remote-IP", ip);
        if (forwarded is not null) request.Headers.Add("X-Forwarded-For", forwarded);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Api_ExhaustedLimit_Returns429RetryAfterAndClearJson()
    {
        using var server = Host();
        using var client = server.GetTestClient();
        (await Post(client)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(client)).StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await Post(client);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("Demasiados intentos").And.Contain("\"success\":false");
    }

    [Fact]
    public async Task Web_ExhaustedLimit_ReturnsSpanishHtmlWithoutEchoingInput()
    {
        using var server = Host();
        using var client = server.GetTestClient();
        await Post(client, "/Account/Login");
        await Post(client, "/Account/Login");
        var response = await Post(client, "/Account/Login?password=must-not-echo");
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Contain("Volver al inicio").And.NotContain("must-not-echo");
    }

    [Fact]
    public async Task DifferentClients_AndDifferentPolicies_HaveIndependentBudgets()
    {
        using var server = Host();
        using var client = server.GetTestClient();
        await Post(client);
        await Post(client);
        (await Post(client)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await Post(client, ip: "203.0.113.11")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(client, "/api/auth/refresh")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MfaEndpoints_ShareBudget_ToPreventSwitchingRouteBypass()
    {
        using var server = Host();
        using var client = server.GetTestClient();
        (await Post(client, "/api/auth/mfa/enroll")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(client, "/api/auth/mfa/confirm")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UntrustedForwardedHeaders_CannotEvadeLimit()
    {
        using var server = Host();
        using var client = server.GetTestClient();
        await Post(client, forwarded: "198.51.100.1");
        await Post(client, forwarded: "198.51.100.2");
        (await Post(client, forwarded: "198.51.100.3")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task LoopbackProxy_UsesForwardedClient_ButIgnoresSpoofedLeftmostHop()
    {
        using var server = Host();
        using var client = server.GetTestClient();
        await Post(client, ip: "127.0.0.1", forwarded: "198.51.100.1, 203.0.113.20");
        await Post(client, ip: "127.0.0.1", forwarded: "198.51.100.2, 203.0.113.20");
        (await Post(client, ip: "127.0.0.1", forwarded: "198.51.100.3, 203.0.113.20")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await Post(client, ip: "127.0.0.1", forwarded: "203.0.113.21")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IPv4AndMappedIPv6_ShareSameBudget()
    {
        using var server = Host();
        using var client = server.GetTestClient();
        await Post(client, ip: "203.0.113.30");
        await Post(client, ip: "::ffff:203.0.113.30");
        (await Post(client, ip: "203.0.113.30")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ExhaustedWindow_Replenishes()
    {
        using var server = Host(1);
        using var client = server.GetTestClient();
        await Post(client);
        await Post(client);
        (await Post(client)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        await Task.Delay(1200);
        (await Post(client)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("WindowSeconds", "0")]
    [InlineData("WindowSeconds", "3601")]
    [InlineData("LoginPermits", "0")]
    [InlineData("MfaPermits", "-1")]
    [InlineData("RefreshPermits", "1001")]
    public void InvalidSettings_FailClosed(string setting, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { [$"Security:AuthRateLimit:{setting}"] = value }).Build();
        var action = () => new ServiceCollection().AddAuthRateLimiting(configuration);
        action.Should().Throw<InvalidOperationException>();
    }
}
