using Microsoft.Extensions.Hosting;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Infrastructure.Diagnostics;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class ReverseProxyConfigurationTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("203.0.113.9", false)]
    public async Task Only_loopback_proxy_can_set_https_and_client_address(string peer, bool trusted)
    {
        using var server = Server(peer);
        using var client = server.GetTestClient(); client.BaseAddress = new Uri("http://neo.example.test");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "198.51.100.8");
        request.Headers.Add("X-Forwarded-Host", "untrusted.example.test");
        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().Be($"{(trusted ? "https" : "http")}|{(trusted ? "198.51.100.8" : peer)}|neo.example.test");
        response.Headers.Contains("Strict-Transport-Security").Should().Be(trusted);
    }

    [Fact]
    public async Task Proxy_consumes_only_one_hop_and_preserves_earlier_untrusted_headers()
    {
        using var server = Server("127.0.0.1"); using var client = server.GetTestClient();
        client.BaseAddress = new Uri("http://neo.example.test");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-For", "192.0.2.5, 198.51.100.8");
        request.Headers.Add("X-Forwarded-Proto", "http, https");
        using var response = await client.SendAsync(request);
        (await response.Content.ReadAsStringAsync()).Should().Be("https|198.51.100.8|neo.example.test");
    }

    private static IHost Server(string peer)
        => new HostBuilder().UseEnvironment("Production").ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddNeoStpForwardedHeaders(); services.AddHsts(_ => { });
        }).Configure(app =>
        {
            app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse(peer); return next(context); });
            app.UseForwardedHeaders(); app.UseHsts();
            app.Run(context => context.Response.WriteAsync($"{context.Request.Scheme}|{context.Connection.RemoteIpAddress}|{context.Request.Host}"));
        })).Start();
}