using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Infrastructure.Billing;
using Xunit;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>
/// Pagos LATAM PL.3 — PayPalBillingProvider: OAuth2 + creación de Order v2 con
/// link de aprobación hospedado, con respuestas HTTP simuladas.
/// </summary>
public class PayPalBillingProviderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (code, body) = responder(request);
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static PayPalBillingProvider Build(Func<HttpRequestMessage, (HttpStatusCode, string)> responder)
    {
        var factory = new StubFactory(new StubHandler(responder));
        var opts = Options.Create(new BillingOptions
        {
            PayPal = new PayPalOptions { ClientId = "cid", Secret = "sec", BaseUrl = "https://api-m.sandbox.paypal.com" },
        });
        return new PayPalBillingProvider(factory, opts, NullLogger<PayPalBillingProvider>.Instance);
    }

    [Fact]
    public void ProviderName_EsPayPal() => Build(_ => (HttpStatusCode.OK, "{}")).ProviderName.Should().Be("PayPal");

    [Fact]
    public async Task CreateCheckout_Exitoso_DevuelveApproveLink()
    {
        var svc = Build(req =>
            req.RequestUri!.AbsoluteUri.Contains("/oauth2/token")
                ? (HttpStatusCode.OK, "{\"access_token\":\"A21\"}")
                : (HttpStatusCode.Created, "{\"id\":\"ORD-1\",\"links\":[{\"rel\":\"self\",\"href\":\"x\"},{\"rel\":\"approve\",\"href\":\"https://paypal.com/checkoutnow?token=ORD-1\"}]}"));

        var r = await svc.CreateCheckoutSessionAsync("paypal_cus_1", "plan_1", "https://ok", "https://cancel");

        r.IsSuccess.Should().BeTrue();
        r.Value!.SessionId.Should().Be("ORD-1");
        r.Value.RedirectUrl.Should().Be("https://paypal.com/checkoutnow?token=ORD-1");
    }

    [Fact]
    public async Task CreateCheckout_AuthFalla_DevuelveError()
    {
        var svc = Build(req =>
            req.RequestUri!.AbsoluteUri.Contains("/oauth2/token")
                ? (HttpStatusCode.Unauthorized, "{}")
                : (HttpStatusCode.Created, "{}"));

        var r = await svc.CreateCheckoutSessionAsync("c", "p", "https://ok", "https://cancel");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("PAYPAL_AUTH_FAILED");
    }

    [Fact]
    public async Task CreateCheckout_OrderRechazada_DevuelveError()
    {
        var svc = Build(req =>
            req.RequestUri!.AbsoluteUri.Contains("/oauth2/token")
                ? (HttpStatusCode.OK, "{\"access_token\":\"A21\"}")
                : (HttpStatusCode.UnprocessableEntity, "{\"name\":\"INVALID_REQUEST\"}"));

        var r = await svc.CreateCheckoutSessionAsync("c", "p", "https://ok", "https://cancel");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("PAYPAL_ORDER_FAILED");
    }
}
