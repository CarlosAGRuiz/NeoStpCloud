using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Infrastructure.Billing;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>Wompi checkout contract via an in-process HttpMessageHandler; no sockets or real credentials.</summary>
public sealed class WompiCheckoutProviderTests
{
    private const string Account = "synthetic-wompi-account";
    private const string Beneficiary = "synthetic-wompi-application-id";
    private const string App = "synthetic-wompi-app";
    private const string Secret = "synthetic-secret-never-publish";
    private const string Token = "synthetic-token-never-publish";
    private const string Success = "https://billing.example.invalid/success";
    private const string Cancel = "https://billing.example.invalid/cancel";

    [Fact]
    public async Task Numeric_provider_session_id_is_preserved_with_provider_account()
    {
        using var f = new Fixture();
        f.Transport.CheckoutBody = "{\"idEnlace\":12345,\"urlEnlace\":\"https://pay.wompi.sv/12345\",\"estaProductivo\":false}";

        var result = await f.Provider.CreateCheckoutAsync(Request());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Provider.Should().Be("Wompi");
        result.Value.ProviderAccountId.Should().Be(Account);
        result.Value.SessionId.Should().Be("12345");
        result.Value.RedirectUrl.Should().Be("https://pay.wompi.sv/12345");
        f.Transport.Requests.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"idEnlace\":0,\"urlEnlace\":\"https://pay.wompi.sv/12345\",\"estaProductivo\":false}")]
    [InlineData("{\"idEnlace\":12345,\"urlEnlace\":\"https://wompi.sv.untrusted.example.invalid/12345\",\"estaProductivo\":false}")]
    [InlineData("[]")]
    [InlineData("{\"idEnlace\":12345,\"estaProductivo\":false}")]
    [InlineData("{\"urlEnlace\":\"https://pay.wompi.sv/12345\",\"estaProductivo\":false}")]
    [InlineData("{\"idEnlace\":\"\",\"urlEnlace\":\"https://pay.wompi.sv/12345\",\"estaProductivo\":false}")]
    [InlineData("{\"idEnlace\":12345,\"urlEnlace\":\"http://pay.wompi.sv/12345\",\"estaProductivo\":false}")]
    [InlineData("{\"idEnlace\":12345,\"urlQrCodeEmv\":\"https://pay.wompi.sv/qr\",\"estaProductivo\":false}")]
    public async Task Malformed_ack_never_fabricates_session_or_falls_back_to_success_url(string response)
    {
        using var f = new Fixture();
        f.Transport.CheckoutBody = response;

        var result = await f.Provider.CreateCheckoutAsync(Request());

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        f.Transport.CheckoutCalls.Should().Be(1);
        f.AssertSanitized(result.Error);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("usd")]
    [InlineData("")]
    public async Task Currency_other_than_USD_is_rejected_before_authentication(string currency)
    {
        using var f = new Fixture();
        var result = await f.Provider.CreateCheckoutAsync(Request() with { Currency = currency });
        result.IsFailure.Should().BeTrue();
        f.Transport.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Account_mismatch_is_rejected_before_creating_payment_link()
    {
        using var f = new Fixture();
        var result = await f.Provider.CreateCheckoutAsync(Request() with { ProviderAccountId = "different-account" });
        result.IsFailure.Should().BeTrue();
        f.Transport.AuthCalls.Should().Be(1);
        f.Transport.CheckoutCalls.Should().Be(0);
    }

    [Fact]
    public async Task Checkout_timeout_is_not_retried_and_does_not_expose_exception_secrets()
    {
        using var f = new Fixture();
        f.Transport.CheckoutException = new TaskCanceledException("Synthetic timeout " + Secret + " " + Token);

        var result = await f.Provider.CreateCheckoutAsync(Request());

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        f.Transport.AuthCalls.Should().Be(1);
        f.Transport.CheckoutCalls.Should().Be(1);
        f.AssertSanitized(result.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task Provider_error_body_is_never_exposed_or_logged(HttpStatusCode status, bool authentication)
    {
        using var f = new Fixture();
        var sensitiveBody = JsonSerializer.Serialize(new { error = Secret, token = Token });
        if (authentication) { f.Transport.AuthStatus = status; f.Transport.AuthBody = sensitiveBody; }
        else { f.Transport.CheckoutStatus = status; f.Transport.CheckoutBody = sensitiveBody; }

        var result = await f.Provider.CreateCheckoutAsync(Request());

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        f.Transport.CheckoutCalls.Should().Be(authentication ? 0 : 1);
        f.AssertSanitized(result.Error);
    }

    [Fact]
    public async Task Distinct_OAuth_client_and_application_ids_create_single_payment_with_snapshot_amount_and_correlation()
    {
        using var f = new Fixture();
        var request = Request();
        var result = await f.Provider.CreateCheckoutAsync(request);
        result.IsSuccess.Should().BeTrue(result.Error);
        var auth = f.Transport.Requests.Single(r => r.Uri.AbsolutePath == "/connect/token");
        auth.Uri.Should().Be(new Uri("https://id.wompi.sv/connect/token"));
        auth.Method.Should().Be(HttpMethod.Post);
        auth.Authorization.Should().BeNull();
        auth.Body.Should().Contain("client_id=" + App).And.Contain("client_secret=" + Secret);
        var post = f.Transport.Requests.Single(r => r.Uri.AbsolutePath == "/EnlacePago");
        post.Uri.Should().Be(new Uri("https://api.wompi.sv/EnlacePago"));
        post.Method.Should().Be(HttpMethod.Post);
        post.Authorization.Should().Be("Bearer " + Token);
        post.Body.Should().NotContain(Secret).And.NotContain(Token);
        using var json = JsonDocument.Parse(post.Body);
        var root = json.RootElement;
        root.GetProperty("identificadorEnlaceComercio").GetString().Should().Be("neostp:checkout:" + request.CorrelationId.ToString("N"));
        root.GetProperty("idAplicativo").GetString().Should().Be(Beneficiary);
        root.GetProperty("monto").GetDecimal().Should().Be(request.Amount);
        root.GetProperty("nombreProducto").GetString().Should().Be(request.PlanName);
        root.GetProperty("formaPago").GetProperty("permitirTarjetaCreditoDebido").GetBoolean().Should().BeTrue();
        var config = root.GetProperty("configuracion");
        config.GetProperty("esMontoEditable").GetBoolean().Should().BeFalse();
        config.GetProperty("esCantidadEditable").GetBoolean().Should().BeFalse();
        config.GetProperty("cantidadPorDefecto").GetInt32().Should().Be(1);
        config.GetProperty("notificarTransaccionCliente").GetBoolean().Should().BeFalse();
        root.GetProperty("limitesDeUso").GetProperty("cantidadMaximaPagosExitosos").GetInt32().Should().Be(1);
        f.Transport.CheckoutCalls.Should().Be(1);
        f.AssertSanitized(result.Error);
    }

    [Theory]
    [InlineData("api-origin")]
    [InlineData("auth-origin")]
    [InlineData("api-http")]
    [InlineData("auth-query")]
    [InlineData("auth-userinfo")]
    [InlineData("auth-port")]
    [InlineData("auth-fragment")]
    [InlineData("production")]
    [InlineData("missing-webhook")]
    [InlineData("webhook-origin")]
    [InlineData("return-origin")]
    public async Task Untrusted_configuration_or_request_never_sends_credentials_or_creates_payment(string scenario)
    {
        using var f = new Fixture();
        var request = Request();
        switch (scenario)
        {
            case "api-origin": f.Options.Wompi.BaseUrl = "https://untrusted.example.invalid"; break;
            case "auth-origin": f.Options.Wompi.IdUrl = "https://untrusted.example.invalid"; break;
            case "api-http": f.Options.Wompi.BaseUrl = "http://api.wompi.sv"; break;
            case "auth-query": f.Options.Wompi.IdUrl = "https://id.wompi.sv?redirect=untrusted"; break;
            case "auth-userinfo": f.Options.Wompi.IdUrl = "https://synthetic:unused@id.wompi.sv"; break;
            case "auth-port": f.Options.Wompi.IdUrl = "https://id.wompi.sv:444"; break;
            case "auth-fragment": f.Options.Wompi.IdUrl = "https://id.wompi.sv#ignored"; break;
            case "production": f.Options.Wompi.IsProduction = true; break;
            case "missing-webhook": f.Options.Wompi.CheckoutWebhookUrl = ""; break;
            case "webhook-origin": f.Options.Wompi.CheckoutWebhookUrl = "https://other.example.invalid/webhook"; break;
            case "return-origin": request = request with { CancelUrl = "https://other.example.invalid/cancel" }; break;
        }
        var result = await f.Provider.CreateCheckoutAsync(request);
        result.IsFailure.Should().BeTrue();
        f.Transport.Requests.Should().BeEmpty();
        f.AssertSanitized(result.Error);
    }

    [Theory]
    [InlineData("wrong-account")]
    [InlineData("missing-account")]
    [InlineData("wrong-app")]
    [InlineData("wrong-client")]
    [InlineData("production-app")]
    public async Task Identity_or_environment_mismatch_prevents_payment_link_creation(string scenario)
    {
        using var f = new Fixture();
        switch (scenario)
        {
            case "wrong-account": f.Transport.AccountBody = "{\"idCuenta\":\"different-account\"}"; break;
            case "missing-account": f.Transport.AccountBody = "{}"; break;
            case "wrong-app": f.Transport.ApplicationBody = JsonSerializer.Serialize(new { idAplicativo = "different-app", clientIdApi = App, estaProductivo = false }); break;
            case "wrong-client": f.Transport.ApplicationBody = JsonSerializer.Serialize(new { idAplicativo = Beneficiary, clientIdApi = "different-client", estaProductivo = false }); break;
            case "production-app": f.Transport.ApplicationBody = JsonSerializer.Serialize(new { idAplicativo = Beneficiary, clientIdApi = App, estaProductivo = true }); break;
        }
        var result = await f.Provider.CreateCheckoutAsync(Request());
        result.IsFailure.Should().BeTrue();
        f.Transport.AuthCalls.Should().Be(1);
        f.Transport.CheckoutCalls.Should().Be(0);
        f.AssertSanitized(result.Error);
    }

    [Fact]
    public async Task Production_link_response_is_not_accepted_in_test_mode()
    {
        using var f = new Fixture();
        f.Transport.CheckoutBody = "{\"idEnlace\":12345,\"urlEnlace\":\"https://pay.wompi.sv/12345\",\"estaProductivo\":true}";
        var result = await f.Provider.CreateCheckoutAsync(Request());
        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        f.Transport.CheckoutCalls.Should().Be(1);
    }
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("12.345")]
    [InlineData("10000000000000000")]
    public async Task Invalid_amount_is_rejected_without_provider_effect(string value)
    {
        using var f = new Fixture();
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var result = await f.Provider.CreateCheckoutAsync(Request() with { Amount = amount });
        result.IsFailure.Should().BeTrue();
        f.Transport.Requests.Should().BeEmpty();
    }
    [Fact]
    public async Task Beneficiary_mismatch_is_discovered_from_application_identity_before_payment_link()
    {
        using var f = new Fixture();
        var result = await f.Provider.CreateCheckoutAsync(Request() with { BeneficiaryId = "different-application-id" });
        result.IsFailure.Should().BeTrue();
        f.Transport.AuthCalls.Should().Be(1);
        f.Transport.Requests.Should().Contain(r => r.Uri.AbsolutePath == "/Aplicativo");
        f.Transport.CheckoutCalls.Should().Be(0);
        f.AssertSanitized(result.Error);
    }
    [Fact]
    public async Task Nonmonthly_interval_is_rejected_before_any_http_request()
    {
        using var f = new Fixture();
        var result = await f.Provider.CreateCheckoutAsync(Request() with { BillingInterval = "YEAR" });
        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        f.Transport.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Oversized_ack_is_bounded_before_buffering_without_session_retry_or_content_exposure()
    {
        using var f = new Fixture();
        var marker = "synthetic-oversized-body-never-expose";
        f.Transport.CheckoutBody = JsonSerializer.Serialize(new
        {
            idEnlace = 12345,
            urlEnlace = "https://pay.wompi.sv/12345",
            estaProductivo = false,
            unexpectedPayload = marker + Secret + Token + new string('x', 262145)
        });
        Encoding.UTF8.GetByteCount(f.Transport.CheckoutBody).Should().BeGreaterThan(262144);

        var result = await f.Provider.CreateCheckoutAsync(Request());

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        f.Transport.AuthCalls.Should().Be(1);
        f.Transport.CheckoutCalls.Should().Be(1);
        (result.Error ?? "").Should().NotContain(marker);
        string.Join("\n", f.Logger.Messages).Should().NotContain(marker);
        f.AssertSanitized(result.Error);
    }
    private static ProviderCheckoutRequest Request() => new(
        Guid.Parse("bbbbbbbb-aaaa-4444-8888-cccccccccccc"), "synthetic-stable-checkout-key", 991001,
        Account, Beneficiary, "synthetic-plan-basic", null, "Basic mensual", 12.34m,
        "USD", "MONTH", Success, Cancel);

    private sealed class Fixture : IDisposable
    {
        public RecordingHandler Transport { get; } = new();
        public RecordingLogger Logger { get; } = new();
        public BillingOptions Options { get; } = new()
        {
            Provider = "Wompi",
            Wompi = new() { AppId = App, ApiSecret = Secret, BaseUrl = "https://api.wompi.sv", IdUrl = "https://id.wompi.sv", IsProduction = false, CheckoutWebhookUrl = "https://billing.example.invalid/api/billing/webhook/wompi" },
            Checkout = new() { Enabled = true, Provider = "Wompi", ProviderAccountId = Account,
                BeneficiaryId = Beneficiary, SuccessUrl = Success, CancelUrl = Cancel }
        };
        public IBillingCheckoutProvider Provider => new WompiBillingProvider(new StubFactory(Transport), Microsoft.Extensions.Options.Options.Create(Options), Logger);
        public void AssertSanitized(string? error)
        {
            (error ?? "").Should().NotContain(Secret).And.NotContain(Token);
            string.Join("\n", Logger.Messages).Should().NotContain(Secret).And.NotContain(Token);
        }
        public void Dispose() => Transport.Dispose();
    }

    private sealed record CapturedRequest(Uri Uri, HttpMethod Method, string? Authorization, string Body);
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];
        public int AuthCalls => Requests.Count(r => r.Uri.AbsolutePath == "/connect/token");
        public int CheckoutCalls => Requests.Count(r => r.Uri.AbsolutePath == "/EnlacePago");
        public HttpStatusCode AuthStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode CheckoutStatus { get; set; } = HttpStatusCode.OK;
        public string AuthBody { get; set; } = JsonSerializer.Serialize(new { access_token = Token });
        public string CheckoutBody { get; set; } = "{\"idEnlace\":12345,\"urlEnlace\":\"https://pay.wompi.sv/12345\",\"estaProductivo\":false}";
        public string AccountBody { get; set; } = JsonSerializer.Serialize(new { idCuenta = Account });
        public string ApplicationBody { get; set; } = JsonSerializer.Serialize(new { idAplicativo = Beneficiary, clientIdApi = App, estaProductivo = false });
        public Exception? CheckoutException { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(new(request.RequestUri!, request.Method, request.Headers.Authorization?.ToString(), body));
            if (request.RequestUri!.AbsolutePath == "/connect/token")
                return new(AuthStatus) { Content = new StringContent(AuthBody, Encoding.UTF8, "application/json") };
            if (request.RequestUri.AbsolutePath == "/Cuenta")
                return new(HttpStatusCode.OK) { Content = new StringContent(AccountBody, Encoding.UTF8, "application/json") };
            if (request.RequestUri.AbsolutePath == "/Aplicativo")
                return new(HttpStatusCode.OK) { Content = new StringContent(ApplicationBody, Encoding.UTF8, "application/json") };
            if (request.RequestUri.AbsolutePath != "/EnlacePago")
                throw new InvalidOperationException("Unexpected synthetic endpoint.");
            if (CheckoutException is not null) throw CheckoutException;
            return new(CheckoutStatus) { Content = new StringContent(CheckoutBody, Encoding.UTF8, "application/json") };
        }
    }
    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class RecordingLogger : ILogger<WompiBillingProvider>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception) + exception);
    }
}
