using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoSTP.Infrastructure.Diagnostics;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>Exercise deployment domain fragments through actual HTTP middleware, without the application database or network.</summary>
public sealed class ProductionDomainConfigurationTests
{
    [Theory]
    [InlineData("web", "app.neostp.com", true)]
    [InlineData("web", "APP.NEOSTP.COM", true)]
    [InlineData("web", "api.neostp.com", false)]
    [InlineData("web", "localhost", false)]
    [InlineData("web", "127.0.0.1", false)]
    [InlineData("web", "attacker.example", false)]
    [InlineData("web", "app.neostp.com.attacker.example", false)]
    [InlineData("api", "api.neostp.com", true)]
    [InlineData("api", "API.NEOSTP.COM", true)]
    [InlineData("api", "app.neostp.com", false)]
    [InlineData("api", "localhost", false)]
    [InlineData("api", "127.0.0.1", false)]
    [InlineData("api", "attacker.example", false)]
    [InlineData("api", "api.neostp.com.attacker.example", false)]
    public async Task Published_allowed_hosts_reject_unapproved_host_before_endpoint(string application, string host, bool allowed)
    {
        using var server = await Server(application);
        using var client = server.GetTestClient();
        client.BaseAddress = new Uri("https://test.invalid");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Host = host;

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(allowed ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        response.Headers.Contains("X-NeoSTP-Test-Endpoint").Should().Be(allowed);
        if (allowed) (await response.Content.ReadAsStringAsync()).Should().Be("domain-policy-probe");
    }

    [Theory]
    [InlineData("https://app.neostp.com", true)]
    [InlineData("http://app.neostp.com", false)]
    [InlineData("https://api.neostp.com", false)]
    [InlineData("https://app.neostp.com:444", false)]
    [InlineData("https://app.neostp.com.attacker.example", false)]
    [InlineData("https://attacker.example", false)]
    [InlineData("http://localhost", false)]
    [InlineData("null", false)]
    public async Task Api_preflight_grants_browser_access_only_to_configured_web_origin(string origin, bool allowed)
    {
        using var server = await Server("api");
        using var client = server.GetTestClient();
        client.BaseAddress = new Uri("https://test.invalid");
        using var request = new HttpRequestMessage(HttpMethod.Options, "/probe");
        request.Headers.Host = "api.neostp.com";
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Contains("X-NeoSTP-Test-Endpoint").Should().BeFalse();
        AssertCors(response, origin, allowed);
        response.Headers.Contains("Access-Control-Allow-Methods").Should().Be(allowed);
        response.Headers.Contains("Access-Control-Allow-Headers").Should().Be(allowed);
    }

    [Theory]
    [InlineData("https://app.neostp.com", true)]
    [InlineData("https://attacker.example", false)]
    public async Task Api_actual_response_has_cors_grant_only_for_web_origin(string origin, bool allowed)
    {
        using var server = await Server("api");
        using var client = server.GetTestClient();
        client.BaseAddress = new Uri("https://test.invalid");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Host = "api.neostp.com";
        request.Headers.Add("Origin", origin);

        using var response = await client.SendAsync(request);

        // CORS governs browser access; an origin denial does not replace endpoint authorization.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCors(response, origin, allowed);
    }

    [Fact]
    public async Task Approved_cors_origin_does_not_bypass_invalid_host()
    {
        using var server = await Server("api");
        using var client = server.GetTestClient();
        client.BaseAddress = new Uri("https://test.invalid");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Host = "attacker.example";
        request.Headers.Add("Origin", "https://app.neostp.com");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Contains("X-NeoSTP-Test-Endpoint").Should().BeFalse();
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Theory]
    [InlineData("web", "app.neostp.com", "GET")]
    [InlineData("web", "app.neostp.com", "POST")]
    [InlineData("api", "api.neostp.com", "GET")]
    [InlineData("api", "api.neostp.com", "POST")]
    public async Task Plain_http_returns_permanent_method_preserving_redirect_with_original_path_and_query(
        string application, string host, string method)
    {
        using var server = await Server(application);
        using var client = server.GetTestClient();
        const string pathAndQuery = "/probe/invoice%20draft?next=%2Fbilling%3Ftab%3Dpaid&company=42";
        using var request = new HttpRequestMessage(new HttpMethod(method), $"http://{host}{pathAndQuery}");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        response.Headers.Location!.AbsoluteUri.Should().Be($"https://{host}{pathAndQuery}");
        response.Headers.Contains("X-NeoSTP-Test-Endpoint").Should().BeFalse();
    }

    [Theory]
    [InlineData("web", "app.neostp.com")]
    [InlineData("api", "api.neostp.com")]
    public async Task Direct_https_reaches_endpoint_without_redirect(string application, string host)
    {
        using var server = await Server(application);
        using var client = server.GetTestClient();

        using var response = await client.GetAsync($"https://{host}/probe?mode=secure");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Location.Should().BeNull();
        response.Headers.GetValues("X-NeoSTP-Test-Scheme").Should().Equal("https");
    }

    [Theory]
    [InlineData("web", "app.neostp.com", "127.0.0.1")]
    [InlineData("web", "app.neostp.com", "::1")]
    [InlineData("api", "api.neostp.com", "127.0.0.1")]
    [InlineData("api", "api.neostp.com", "::1")]
    public async Task Loopback_proxy_https_header_prevents_redirect_loop(string application, string host, string peer)
    {
        using var server = await Server(application, peer);
        using var client = server.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}/probe?mode=tunnel");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "198.51.100.8");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Location.Should().BeNull();
        response.Headers.GetValues("X-NeoSTP-Test-Scheme").Should().Equal("https");
    }

    [Theory]
    [InlineData("web", "app.neostp.com")]
    [InlineData("api", "api.neostp.com")]
    public async Task Untrusted_peer_cannot_disable_https_redirect_with_forwarded_header(string application, string host)
    {
        using var server = await Server(application, "203.0.113.9");
        using var client = server.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}/probe?mode=untrusted");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "198.51.100.8");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        response.Headers.Location!.AbsoluteUri.Should().Be($"https://{host}/probe?mode=untrusted");
        response.Headers.Contains("X-NeoSTP-Test-Endpoint").Should().BeFalse();
    }

    [Theory]
    [InlineData("web")]
    [InlineData("api")]
    public async Task Malicious_http_host_is_rejected_before_creating_a_redirect_location(string application)
    {
        using var server = await Server(application);
        using var client = server.GetTestClient();

        using var response = await client.GetAsync("http://attacker.example/probe?next=%2Fbilling");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Location.Should().BeNull();
        response.Headers.Contains("X-NeoSTP-Test-Endpoint").Should().BeFalse();
    }

    private static void AssertCors(HttpResponseMessage response, string origin, bool allowed)
    {
        response.Headers.Contains("Access-Control-Allow-Origin").Should().Be(allowed);
        if (allowed) response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(origin);
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    private static async Task<IHost> Server(string application, string peer = "203.0.113.9")
    {
        var fragment = FragmentPath(application);
        var configuration = new ConfigurationBuilder().AddJsonFile(fragment, optional: false, reloadOnChange: false).Build();
        var builder = Host.CreateDefaultBuilder(Array.Empty<string>())
            .UseEnvironment("Production")
            .ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.Sources.Clear();
                configurationBuilder.AddJsonFile(fragment, optional: false, reloadOnChange: false);
            })
            .ConfigureWebHostDefaults(web => web.UseTestServer().ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddNeoStpForwardedHeaders();
                services.Configure<HttpsRedirectionOptions>(configuration.GetSection("HttpsRedirection"));
                if (application == "api")
                {
                    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                    services.AddCors(options => options.AddDefaultPolicy(policy =>
                    {
                        policy.AllowAnyHeader().AllowAnyMethod();
                        if (origins.Length > 0) policy.WithOrigins(origins);
                        else policy.SetIsOriginAllowed(_ => false);
                    }));
                }
            })
            .Configure(app =>
            {
                // HostFiltering installed by the defaults runs before this application pipeline.
                app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse(peer); return next(context); });
                app.UseForwardedHeaders();
                app.UseHttpsRedirection();
                app.UseRouting();
                if (application == "api") app.UseCors();
                app.UseEndpoints(endpoints => endpoints.MapMethods("/probe", ["GET", "POST"], async context =>
                {
                    context.Response.Headers["X-NeoSTP-Test-Endpoint"] = "reached";
                    context.Response.Headers["X-NeoSTP-Test-Scheme"] = context.Request.Scheme;
                    await context.Response.WriteAsync("domain-policy-probe");
                }));
            }));
        return await builder.StartAsync();
    }

    private static string FragmentPath(string application)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NeoSTP.slnx")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root was not found for deployment fragment tests.");
        return Path.Combine(directory.FullName, "tools", "ProductionDeployment", "config", application, "appsettings.Production.json");
    }
}
