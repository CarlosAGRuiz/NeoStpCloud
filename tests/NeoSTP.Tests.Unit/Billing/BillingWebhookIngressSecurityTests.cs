using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Api.Controllers;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;
using NSubstitute;
using Stripe;

namespace NeoSTP.Tests.Unit.Billing;

// TestServer only: real API routing/controller and Stripe signature utility, synthetic handler.
// No Program, providers, database, sockets or external credentials.
public sealed class BillingWebhookIngressSecurityTests
{
    private const string ValidSecret = "whsec_synthetic_audit_2026";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("REPLACE_WITH_STRIPE_SECRET")]
    [InlineData("whsec_REPLACE_ME")]
    [InlineData("sk_live_not_a_webhook_secret")]
    [InlineData(" whsec_synthetic_audit_2026 ")]
    [InlineData("whsec_short")]
    public async Task MissingPlaceholderOrMalformedSecretFailsClosedBeforeHandler(string secret)
    {
        using var fixture = new HttpFixture(secret);
        const string payload = "{\"id\":\"evt_sensitive_body\",\"type\":\"invoice.payment_succeeded\"}";

        using var response = await fixture.Post(payload);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("evt_sensitive_body");
        fixture.Handler.ReceivedCalls().Should().BeEmpty();
        fixture.Log.Values.Should().NotContain(value => value.Contains(payload));
    }

    [Fact]
    public async Task InvalidSignatureReturnsGenericBadRequestAndNeverInvokesHandler()
    {
        using var fixture = new HttpFixture(ValidSecret);
        const string payload = "{\"id\":\"evt_sensitive_invalid\",\"type\":\"invoice.payment_succeeded\"}";

        using var response = await fixture.Post(payload, "t=1,v1=invalid-signature");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("evt_sensitive_invalid").And.NotContain(ValidSecret);
        fixture.Handler.ReceivedCalls().Should().BeEmpty();
        fixture.Log.Values.Should().NotContain(value => value.Contains(payload) || value.Contains(ValidSecret));
    }

    [Fact]
    public async Task ValidCurrentSignatureInvokesHandlerExactlyOnceWithVerifiedIdentity()
    {
        using var fixture = new HttpFixture(ValidSecret);
        var payload = Payload("evt_synthetic_verified");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = EventUtility.ComputeSignature(ValidSecret, timestamp, payload);

        using var response = await fixture.Post(payload, $"t={timestamp},v1={signature}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await fixture.Handler.Received(1).HandleAsync("Stripe", "invoice.payment_succeeded",
            "evt_synthetic_verified", payload, Arg.Any<CancellationToken>());
    }

    private static string Payload(string id) => JsonSerializer.Serialize(new
    {
        id,
        @object = "event",
        api_version = StripeConfiguration.ApiVersion,
        created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        livemode = false,
        type = "invoice.payment_succeeded",
        data = new { @object = new { id = "in_synthetic" } },
    });

    private sealed class HttpFixture : IDisposable
    {
        public IBillingWebhookHandler Handler { get; } = Substitute.For<IBillingWebhookHandler>();
        public CaptureLogger Log { get; } = new();
        private readonly IHost _host;
        private readonly HttpClient _client;

        public HttpFixture(string secret)
        {
            Handler.HandleAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>()).Returns(Result.Ok());
            _host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(BillingWebhookController).Assembly);
                services.AddSingleton(Handler);
                services.AddSingleton<ILogger<BillingWebhookController>>(Log);
                services.AddSingleton<IOptions<BillingOptions>>(Options.Create(new BillingOptions {
                    Stripe = new NeoSTP.Application.Billing.StripeOptions { WebhookSecret = secret }
                }));
            }).Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            })).Start();
            _client = _host.GetTestClient();
        }

        public async Task<HttpResponseMessage> Post(string payload, string? signature = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhooks/stripe") {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            if (signature is not null) request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
            return await _client.SendAsync(request);
        }

        public void Dispose()
        {
            _client.Dispose();
            _host.Dispose();
        }
    }

    private sealed class CaptureLogger : ILogger<BillingWebhookController>
    {
        public List<string> Values { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Values.Add(formatter(state, exception));
    }
}
