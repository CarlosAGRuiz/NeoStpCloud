using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Billing;
using Polly.Timeout;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>Real named-client resilience + in-process primary transport. No hosts, SQL, workers or network.</summary>
public sealed class WompiPaymentVerifierTests
{
    private const string Account = "synthetic-account";
    private const string Beneficiary = "synthetic-application";
    private const string ClientId = "synthetic-oauth-client";
    private const string Secret = "synthetic-secret-never-expose";
    private const string Token = "synthetic-token-never-expose";
    private const string TokenPath = "/connect/token";
    private const string LinkPath = "/EnlacePago/12345";
    private static readonly Guid Transaction = Guid.Parse("8947c09e-91cd-4c02-9124-a3dff97249c7");
    private static readonly Guid Correlation = Guid.Parse("13325f2f-b4f3-48ea-9278-d6b3247f41c8");
    private static readonly string TransactionPath = $"/TransaccionCompra/{Transaction:D}";
    private static readonly string[] Paths = [TokenPath, "/Cuenta", "/Aplicativo", LinkPath, TransactionPath];
    private const string PaidDate = "2026-09-04T21:30:00-06:00";

    [Fact]
    public async Task Matching_evidence_returns_provider_transaction_and_explicit_payment_time_without_business_post()
    {
        await using var f = new Fixture();

        var result = await f.Verifier.VerifyAsync(Request());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.TransactionId.Should().Be(Transaction);
        result.Value.PaidAt.Should().Be(new DateTimeOffset(2026, 9, 4, 21, 30, 0, TimeSpan.FromHours(-6)));
        result.Value.PaidAt.Offset.Should().Be(TimeSpan.FromHours(-6));
        f.Transport.Attempts.Select(a => a.Path).Should().Equal(Paths);
        f.AssertNoBusinessMutation();
        var tokenRequest = f.Transport.Attempts.Single(a => a.Path == TokenPath);
        tokenRequest.Host.Should().Be("id.wompi.sv");
        tokenRequest.Authorization.Should().BeNull();
        tokenRequest.Body.Should().Contain("client_id=" + ClientId).And.Contain("client_secret=" + Secret);
        f.Transport.Attempts.Where(a => a.Method == "GET").Should()
            .OnlyContain(a => a.Host == "api.wompi.sv" && a.Authorization == "Bearer " + Token && a.Body == string.Empty);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("application")]
    [InlineData("oauth-client")]
    [InlineData("application-production")]
    [InlineData("link-id")]
    [InlineData("link-application")]
    [InlineData("link-correlation")]
    [InlineData("link-amount")]
    [InlineData("link-amount-string")]
    [InlineData("link-production")]
    [InlineData("membership-missing")]
    [InlineData("membership-null")]
    [InlineData("membership-duplicate")]
    [InlineData("member-id")]
    [InlineData("member-production")]
    [InlineData("member-unapproved")]
    [InlineData("member-amount")]
    [InlineData("transaction-id")]
    [InlineData("transaction-production")]
    [InlineData("transaction-unapproved")]
    [InlineData("transaction-amount")]
    [InlineData("transaction-amount-string")]
    [InlineData("transaction-date-missing")]
    [InlineData("transaction-date-invalid")]
    [InlineData("transaction-date-no-offset")]
    public async Task Mismatching_remote_identity_membership_mode_money_or_date_never_verifies(string mismatch)
    {
        await using var f = new Fixture();
        var account = f.Transport.Read("/Cuenta");
        var application = f.Transport.Read("/Aplicativo");
        var link = f.Transport.Read(LinkPath);
        var member = link["transacciones"]![0]!.AsObject();
        var transaction = f.Transport.Read(TransactionPath);
        switch (mismatch)
        {
            case "account": account["idCuenta"] = "different-account"; break;
            case "application": application["idAplicativo"] = "different-application"; break;
            case "oauth-client": application["clientIdApi"] = "different-client"; break;
            case "application-production": application["estaProductivo"] = true; break;
            case "link-id": link["idEnlace"] = 12346; break;
            case "link-application": link["idAplicativo"] = "different-application"; break;
            case "link-correlation": link["nombreEnlace"] = "neostp:checkout:" + Guid.NewGuid().ToString("N"); break;
            case "link-amount": link["monto"] = 0.01m; break;
            case "link-amount-string": link["monto"] = "15.50"; break;
            case "link-production": link["estaProductivo"] = true; break;
            case "membership-missing": link.Remove("transacciones"); link["transaccionCompra"] = member.DeepClone(); break;
            case "membership-null": link["transacciones"] = null; break;
            case "membership-duplicate": link["transacciones"]!.AsArray().Add(member.DeepClone()); break;
            case "member-id": member["idTransaccion"] = Guid.NewGuid().ToString(); break;
            case "member-production": member["esReal"] = true; break;
            case "member-unapproved": member["esAprobada"] = false; break;
            case "member-amount": member["monto"] = 0.01m; break;
            case "transaction-id": transaction["idTransaccion"] = Guid.NewGuid().ToString(); break;
            case "transaction-production": transaction["esReal"] = true; break;
            case "transaction-unapproved": transaction["esAprobada"] = false; break;
            case "transaction-amount": transaction["monto"] = 0.01m; break;
            case "transaction-amount-string": transaction["monto"] = "15.50"; break;
            case "transaction-date-missing": transaction.Remove("fechaTransaccion"); break;
            case "transaction-date-invalid": transaction["fechaTransaccion"] = "not-a-date"; break;
            case "transaction-date-no-offset": transaction["fechaTransaccion"] = "2026-09-04T21:30:00"; break;
        }
        f.Transport.Bodies["/Cuenta"] = account.ToJsonString();
        f.Transport.Bodies["/Aplicativo"] = application.ToJsonString();
        f.Transport.Bodies[LinkPath] = link.ToJsonString();
        f.Transport.Bodies[TransactionPath] = transaction.ToJsonString();

        var result = await f.Verifier.VerifyAsync(Request());

        result.IsFailure.Should().BeTrue(mismatch);
        result.Value.Should().BeNull();
        result.ErrorCode.Should().BeOneOf("WOMPI_PAYMENT_IDENTITY_MISMATCH", "WOMPI_PAYMENT_NOT_VERIFIED");
        f.AssertNoBusinessMutation();
        if (!mismatch.StartsWith("transaction-", StringComparison.Ordinal))
            f.Transport.Attempts.Should().NotContain(a => a.Path == TransactionPath);
    }

    [Theory]
    [InlineData("request-production")]
    [InlineData("configured-production")]
    [InlineData("currency")]
    [InlineData("zero")]
    [InlineData("precision")]
    [InlineData("transaction-id")]
    [InlineData("correlation-id")]
    [InlineData("checkout-path")]
    [InlineData("checkout-zero")]
    [InlineData("account")]
    [InlineData("beneficiary")]
    [InlineData("api-origin")]
    [InlineData("auth-origin")]
    [InlineData("secret")]
    public async Task Invalid_request_or_unsafe_configuration_is_rejected_before_any_http(string invalid)
    {
        await using var f = new Fixture();
        var request = Request();
        switch (invalid)
        {
            case "request-production": request = request with { IsProduction = true }; break;
            case "configured-production": f.Options.Wompi.IsProduction = true; break;
            case "currency": request = request with { Currency = "EUR" }; break;
            case "zero": request = request with { Amount = 0 }; break;
            case "precision": request = request with { Amount = 15.501m }; break;
            case "transaction-id": request = request with { TransactionId = Guid.Empty }; break;
            case "correlation-id": request = request with { CorrelationId = Guid.Empty }; break;
            case "checkout-path": request = request with { ExternalCheckoutId = "../TransaccionCompra" }; break;
            case "checkout-zero": request = request with { ExternalCheckoutId = "0" }; break;
            case "account": request = request with { ProviderAccountId = "" }; break;
            case "beneficiary": request = request with { BeneficiaryId = "" }; break;
            case "api-origin": f.Options.Wompi.BaseUrl = "https://api.wompi.sv.untrusted.invalid"; break;
            case "auth-origin": f.Options.Wompi.IdUrl = "https://untrusted.invalid"; break;
            case "secret": f.Options.Wompi.ApiSecret = "CHANGEME"; break;
        }

        var result = await f.Verifier.VerifyAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        f.Transport.Attempts.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Malformed_response_stops_verification_without_repeating_business_work(int stage)
    {
        await using var f = new Fixture();
        f.Transport.Bodies[Paths[stage]] = "malformed " + Secret + " " + Token;

        var result = await f.Verifier.VerifyAsync(Request());

        result.ErrorCode.Should().Be("WOMPI_VERIFICATION_UNAVAILABLE");
        result.Value.Should().BeNull();
        f.AssertSanitized(result.Error);
        f.Transport.Attempts.Should().HaveCount(stage + 1);
        f.AssertNoBusinessMutation();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exhausted_network_or_timeout_read_returns_sanitized_failure_and_never_creates_payment(bool timeout)
    {
        await using var f = new Fixture();
        f.Transport.FailurePath = TransactionPath;
        f.Transport.Failure = timeout ? new TimeoutRejectedException(Secret + Token) : new HttpRequestException(Secret + Token);

        var result = await f.Verifier.VerifyAsync(Request());

        result.ErrorCode.Should().Be("WOMPI_VERIFICATION_UNAVAILABLE");
        result.Value.Should().BeNull();
        f.AssertSanitized(result.Error);
        f.Transport.Attempts.Count(a => a.Path == TransactionPath).Should().Be(3, "only read-only requests use the real DI retry policy");
        f.AssertNoBusinessMutation();
    }

    [Fact]
    public async Task Transient_read_503_retries_same_get_and_keeps_authentication_single_attempt()
    {
        await using var f = new Fixture();
        f.Transport.TransientPath = LinkPath;
        f.Transport.TransientFailures = 1;

        var result = await f.Verifier.VerifyAsync(Request());

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Transport.Attempts.Count(a => a.Path == LinkPath).Should().Be(2);
        f.Transport.Attempts.Should().HaveCount(6);
        f.AssertNoBusinessMutation();
    }

    [Fact]
    public async Task OAuth_503_is_not_retried_and_no_resource_is_consulted()
    {
        await using var f = new Fixture();
        f.Transport.TransientPath = TokenPath;
        f.Transport.TransientFailures = 1;

        var result = await f.Verifier.VerifyAsync(Request());

        result.ErrorCode.Should().Be("WOMPI_VERIFICATION_UNAVAILABLE");
        f.Transport.Attempts.Should().ContainSingle();
        f.AssertNoBusinessMutation();
    }

    [Fact]
    public async Task Oversized_resource_is_bounded_and_cannot_become_verified()
    {
        await using var f = new Fixture();
        f.Transport.Bodies[TransactionPath] = "{\"padding\":\"" + new string('x', 262145) + Secret + "\"}";

        var result = await f.Verifier.VerifyAsync(Request());

        result.ErrorCode.Should().Be("WOMPI_VERIFICATION_UNAVAILABLE");
        result.Value.Should().BeNull();
        f.AssertSanitized(result.Error);
        f.Transport.Attempts.Count(a => a.Path == TransactionPath).Should().Be(1);
        f.AssertNoBusinessMutation();
    }

    [Fact]
    public async Task Caller_cancellation_propagates_without_sending_a_request()
    {
        await using var f = new Fixture();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Func<Task> act = async () => await f.Verifier.VerifyAsync(Request(), canceled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        f.Transport.Attempts.Should().BeEmpty();
    }

    private static WompiPaymentVerificationRequest Request()
        => new(Transaction, "12345", Correlation, Account, Beneficiary, 15.50m, "USD", false);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        public SyntheticTransport Transport { get; } = new();
        public BillingOptions Options { get; } = new() { Wompi = new() { AppId = ClientId, ApiSecret = Secret } };
        public WompiPaymentVerifier Verifier { get; }

        public Fixture()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:NeoStpDb"] = "Server=synthetic.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true",
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure(configuration);
            services.PostConfigureAll<HttpStandardResilienceOptions>(o => { o.Retry.Delay = TimeSpan.FromMilliseconds(1); o.Retry.UseJitter = false; });
            services.AddHttpClient(WompiBillingProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Transport);
            _services = services.BuildServiceProvider();
            Verifier = new(_services.GetRequiredService<IHttpClientFactory>(), Microsoft.Extensions.Options.Options.Create(Options));
        }

        public void AssertNoBusinessMutation()
        {
            Transport.Attempts.Where(a => a.Method != "GET").Should().ContainSingle()
                .Which.Should().Match<Attempt>(a => a.Method == "POST" && a.Path == TokenPath);
        }

        public void AssertSanitized(string? message)
            => message.Should().NotContain(Secret).And.NotContain(Token).And.NotContain("padding");

        public ValueTask DisposeAsync() => _services.DisposeAsync();
    }

    private sealed record Attempt(string Method, string Host, string Path, string? Authorization, string Body);

    private sealed class SyntheticTransport : HttpMessageHandler
    {
        public ConcurrentQueue<Attempt> Attempts { get; } = new();
        public Dictionary<string, string> Bodies { get; } = new()
        {
            [TokenPath] = JsonSerializer.Serialize(new { access_token = Token }),
            ["/Cuenta"] = JsonSerializer.Serialize(new { idCuenta = Account }),
            ["/Aplicativo"] = JsonSerializer.Serialize(new { idAplicativo = Beneficiary, clientIdApi = ClientId, estaProductivo = false }),
            [LinkPath] = JsonSerializer.Serialize(new
            {
                idEnlace = 12345, idAplicativo = Beneficiary, nombreEnlace = $"neostp:checkout:{Correlation:N}",
                monto = 15.50m, estaProductivo = false,
                transacciones = new[] { new { idTransaccion = Transaction.ToString(), esReal = false, esAprobada = true, monto = 15.50m } },
            }),
            [TransactionPath] = JsonSerializer.Serialize(new
            {
                idTransaccion = Transaction.ToString(), esReal = false, esAprobada = true, monto = 15.50m, fechaTransaccion = PaidDate,
            }),
        };
        public string? FailurePath { get; set; }
        public Exception? Failure { get; set; }
        public string? TransientPath { get; set; }
        public int TransientFailures { get; set; }
        public JsonObject Read(string path) => JsonNode.Parse(Bodies[path])!.AsObject();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var path = request.RequestUri!.AbsolutePath;
            Attempts.Enqueue(new(request.Method.Method, request.RequestUri.Host, path,
                request.Headers.Authorization?.ToString(), request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct)));
            if (path == FailurePath && Failure is not null) throw Failure;
            if (path == TransientPath && TransientFailures > 0)
            {
                TransientFailures--;
                return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("synthetic unavailable " + Secret) };
            }
            if (!Bodies.TryGetValue(path, out var body)) throw new InvalidOperationException("Unexpected synthetic endpoint.");
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
