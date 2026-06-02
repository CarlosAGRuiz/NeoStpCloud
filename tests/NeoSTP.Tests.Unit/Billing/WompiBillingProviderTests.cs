using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Infrastructure.Billing;
using Xunit;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>
/// Pagos LATAM PL.2 — WompiBillingProvider: OAuth2 + creación de enlace de pago
/// hospedado, con respuestas HTTP simuladas.
/// </summary>
public class WompiBillingProviderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode, string)> _responder;
        public StubHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (code, body) = _responder(request);
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static WompiBillingProvider Build(Func<HttpRequestMessage, (HttpStatusCode, string)> responder)
    {
        var factory = new StubFactory(new StubHandler(responder));
        var opts = Options.Create(new BillingOptions
        {
            Wompi = new WompiOptions { AppId = "app", ApiSecret = "secret", BaseUrl = "https://api.wompi.sv", IdUrl = "https://id.wompi.sv" },
        });
        return new WompiBillingProvider(factory, opts, NullLogger<WompiBillingProvider>.Instance);
    }

    [Fact]
    public void ProviderName_EsWompi() => Build(_ => (HttpStatusCode.OK, "{}")).ProviderName.Should().Be("Wompi");

    [Fact]
    public async Task CreateCheckout_Exitoso_DevuelveUrlDelEnlace()
    {
        var svc = Build(req =>
            req.RequestUri!.AbsoluteUri.Contains("/connect/token")
                ? (HttpStatusCode.OK, "{\"access_token\":\"tok-123\"}")
                : (HttpStatusCode.OK, "{\"idEnlace\":\"ENL-9\",\"urlEnlace\":\"https://pay.wompi.sv/ENL-9\"}"));

        var r = await svc.CreateCheckoutSessionAsync("wompi_cus_1", "plan_1", "https://ok", "https://cancel");

        r.IsSuccess.Should().BeTrue();
        r.Value!.SessionId.Should().Be("ENL-9");
        r.Value.RedirectUrl.Should().Be("https://pay.wompi.sv/ENL-9");
    }

    [Fact]
    public async Task CreateCheckout_AuthFalla_DevuelveError()
    {
        var svc = Build(req =>
            req.RequestUri!.AbsoluteUri.Contains("/connect/token")
                ? (HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\"}")
                : (HttpStatusCode.OK, "{}"));

        var r = await svc.CreateCheckoutSessionAsync("c", "p", "https://ok", "https://cancel");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("WOMPI_AUTH_FAILED");
    }

    [Fact]
    public async Task CreateCheckout_EnlaceRechazado_DevuelveError()
    {
        var svc = Build(req =>
            req.RequestUri!.AbsoluteUri.Contains("/connect/token")
                ? (HttpStatusCode.OK, "{\"access_token\":\"tok\"}")
                : (HttpStatusCode.BadRequest, "{\"error\":\"monto invalido\"}"));

        var r = await svc.CreateCheckoutSessionAsync("c", "p", "https://ok", "https://cancel");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("WOMPI_LINK_FAILED");
    }

    [Fact]
    public async Task CreateCustomer_DevuelveIdSintetico()
    {
        var r = await Build(_ => (HttpStatusCode.OK, "{}")).CreateCustomerAsync("a@b.com", 7);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be("wompi_cus_7");
    }
}
