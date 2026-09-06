using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Unit.Billing;

public sealed class WompiWebhookReceiverTests
{
    [Fact]
    public async Task Signed_notice_is_durable_before_verification_and_never_grants_payment_or_license()
    {
        await using var f = new Fixture();
        f.Verifier.Callback = async request =>
        {
            await using var observer = f.Observe();
            var receipt = await observer.BillingPaymentNotifications.SingleAsync();
            receipt.Status.Should().Be(BillingPaymentNotificationStatuses.Processing);
            receipt.ReceiptId.Should().NotBeEmpty();
            receipt.BillingCheckoutIntentId.Should().Be(f.Intent.Id);
            receipt.LeaseExpiresAt.Should().BeAfter(DateTime.UtcNow);
            receipt.PayloadHash.Should().HaveLength(64);
            receipt.SemanticHash.Should().HaveLength(64);
            request.Should().BeEquivalentTo(new WompiPaymentVerificationRequest(f.Transaction, "12345", f.Intent.CorrelationId,
                Fixture.Account, Fixture.Beneficiary, 12.34m, "USD", false));
            observer.BillingPayments.Should().BeEmpty();
            observer.EmpresaPlanes.Should().BeEmpty();
            return Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, f.At));
        };
        var json = f.Payload();
        json["EmpresaId"] = BillingSecurityFixture.EmpresaB;
        json["NombreCliente"] = "synthetic-sensitive-name";

        var result = await f.Send(json);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(BillingPaymentNotificationStatuses.VerifiedSandbox);
        var saved = await f.Db.BillingPaymentNotifications.SingleAsync();
        saved.VerifiedAt.Should().NotBeNull();
        saved.ProviderPaidAt.Should().Be(f.At);
        await using var persistedObserver = f.Observe();
        JsonSerializer.Serialize(await persistedObserver.BillingPaymentNotifications.AsNoTracking().SingleAsync()).Should().NotContain("synthetic-sensitive-name");
        f.Intent.Status.Should().Be(BillingCheckoutStatuses.PaymentVerifiedSandbox);
        f.Intent.EmpresaId.Should().Be(BillingSecurityFixture.EmpresaA);
        f.AssertNoCommercialPayment();
    }

    [Fact]
    public async Task Reordered_semantic_replay_with_changed_extra_PII_reuses_receipt_without_verification()
    {
        await using var f = new Fixture();
        var firstBody = f.Payload();
        firstBody["NombreCliente"] = "first synthetic name";
        var first = await f.Send(firstBody);
        var reordered = new JsonObject();
        foreach (var property in firstBody.Reverse()) reordered[property.Key] = property.Value?.DeepClone();
        reordered["NombreCliente"] = "updated synthetic name";
        reordered["FechaTransaccion"] = f.At.ToOffset(TimeSpan.FromHours(2)).ToString("O");

        var replay = await f.Send(reordered);

        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value.Should().BeEquivalentTo(first.Value);
        f.Verifier.Calls.Should().Be(1);
        f.Db.BillingPaymentNotifications.Should().ContainSingle();
    }

    [Fact]
    public async Task Same_transaction_with_changed_amount_conflicts_and_preserves_original_receipt()
    {
        await using var f = new Fixture();
        (await f.Send(f.Payload())).IsSuccess.Should().BeTrue();
        var conflicting = f.Payload(); conflicting["Monto"] = 99;
        var result = await f.Send(conflicting);
        result.ErrorCode.Should().Be("WOMPI_EVENT_CONFLICT");
        (await f.Db.BillingPaymentNotifications.SingleAsync()).Amount.Should().Be(12.34m);
        f.Verifier.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("unknown-intent")]
    [InlineData("amount")]
    [InlineData("intent-account")]
    [InlineData("intent-beneficiary")]
    [InlineData("link")]
    [InlineData("old-date")]
    public async Task Unknown_or_mismatched_intent_is_quarantined_durably_without_remote_verification(string scenario)
    {
        await using var f = new Fixture();
        var payload = f.Payload();
        switch (scenario)
        {
            case "unknown-intent": payload["EnlacePago"]!["IdentificadorEnlaceComercio"] = "neostp:checkout:" + Guid.NewGuid().ToString("N"); break;
            case "amount": payload["Monto"] = 12.35m; break;
            case "intent-account": f.Intent.ProviderAccountId = "different-account"; break;
            case "intent-beneficiary": f.Intent.BeneficiaryId = "different-application"; break;
            case "link": payload["EnlacePago"]!["Id"] = 54321; break;
            case "old-date": payload["FechaTransaccion"] = f.At.AddHours(-1).ToString("O"); break;
        }
        await f.Db.SaveChangesAsync();
        var result = await f.Send(payload);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(BillingPaymentNotificationStatuses.RequiresReconciliation);
        var receipt = await f.Db.BillingPaymentNotifications.SingleAsync();
        receipt.BillingCheckoutIntentId.Should().BeNull();
        receipt.LastErrorCode.Should().Be("WOMPI_CHECKOUT_MISMATCH");
        f.Verifier.Calls.Should().Be(0);
        f.AssertNoCommercialPayment();
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("production")]
    [InlineData("placeholder-secret")]
    public async Task Unavailable_configuration_rejects_before_inbox_or_verification(string scenario)
    {
        await using var f = new Fixture();
        switch (scenario)
        {
            case "disabled": f.Options.Wompi.WebhookEnabled = false; break;
            case "production": f.Options.Wompi.IsProduction = true; break;
            case "placeholder-secret": f.Options.Wompi.ApiSecret = "REPLACE_ME"; break;
        }
        var result = await f.Send(f.Payload());
        result.ErrorCode.Should().Be("WOMPI_WEBHOOK_UNAVAILABLE");
        f.AssertNoReceipt();
    }

    [Theory]
    [InlineData("account")]
    [InlineData("beneficiary")]
    [InlineData("production")]
    public async Task Signed_wrong_identity_or_environment_is_not_accepted(string scenario)
    {
        await using var f = new Fixture();
        var payload = f.Payload();
        if (scenario == "account") payload["IdCuenta"] = "foreign-account";
        else if (scenario == "beneficiary") payload["Aplicativo"]!["Id"] = "foreign-app";
        else payload["EsProductiva"] = true;
        var result = await f.Send(payload);
        result.ErrorCode.Should().Be("WOMPI_IDENTITY_MISMATCH");
        f.AssertNoReceipt();
    }

    [Theory]
    [InlineData("changed-byte")]
    [InlineData("empty")]
    [InlineData("nonhex")]
    [InlineData("wrong-key")]
    public async Task Invalid_signature_never_parses_or_persists_notification(string scenario)
    {
        await using var f = new Fixture();
        var body = Encoding.UTF8.GetBytes(f.Payload().ToJsonString());
        var signature = Fixture.Sign(body);
        switch (scenario)
        {
            case "changed-byte": body = body.Concat(new byte[] { 32 }).ToArray(); break;
            case "empty": signature = ""; break;
            case "nonhex": signature = new string('z', 64); break;
            case "wrong-key": signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("wrong-key"), body)); break;
        }
        var result = await f.Receiver.ReceiveAsync(body, signature);
        result.ErrorCode.Should().Be("WOMPI_SIGNATURE_INVALID");
        f.AssertNoReceipt();
    }

    [Theory]
    [InlineData("duplicate-root")]
    [InlineData("duplicate-nested")]
    [InlineData("missing-offset")]
    [InlineData("future")]
    [InlineData("quantity")]
    [InlineData("unapproved")]
    [InlineData("fractional-cent")]
    [InlineData("zero")]
    public async Task Signed_invalid_payload_is_rejected_before_inbox(string scenario)
    {
        await using var f = new Fixture();
        var payload = f.Payload();
        switch (scenario)
        {
            case "missing-offset": payload["FechaTransaccion"] = f.At.ToString("yyyy-MM-ddTHH:mm:ss"); break;
            case "future": payload["FechaTransaccion"] = f.At.AddHours(1).ToString("O"); break;
            case "quantity": payload["Cantidad"] = 2; break;
            case "unapproved": payload["ResultadoTransaccion"] = "Rechazada"; break;
            case "fractional-cent": payload["Monto"] = 12.345m; break;
            case "zero": payload["Monto"] = 0; break;
        }
        var raw = payload.ToJsonString();
        if (scenario == "duplicate-root") raw = "{\"Monto\":12.34," + raw[1..];
        if (scenario == "duplicate-nested") raw = raw.Replace("\"Aplicativo\":{", "\"Aplicativo\":{\"Id\":\"duplicate\",");
        var bytes = Encoding.UTF8.GetBytes(raw);
        var result = await f.Receiver.ReceiveAsync(bytes, Fixture.Sign(bytes));
        result.ErrorCode.Should().Be("WOMPI_PAYLOAD_INVALID");
        f.AssertNoReceipt();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_remote_verification_keeps_receipt_and_replay_can_reverify_without_payment(bool throws)
    {
        await using var f = new Fixture();
        f.Verifier.Callback = _ => throws ? throw new TimeoutException("synthetic-timeout")
            : Task.FromResult(Result<WompiVerifiedPayment>.Fail("Synthetic provider unavailable"));
        var first = await f.Send(f.Payload());
        first.ErrorCode.Should().Be("WOMPI_VERIFICATION_PENDING");
        first.Value!.ReceiptId.Should().NotBeEmpty();
        (await f.Db.BillingPaymentNotifications.SingleAsync()).Status.Should().Be(BillingPaymentNotificationStatuses.RequiresReconciliation);
        f.AssertNoCommercialPayment();
        f.Verifier.Callback = request => Task.FromResult(Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, f.At)));

        var replay = await f.Send(f.Payload());

        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value!.ReceiptId.Should().Be(first.Value.ReceiptId);
        f.Verifier.Calls.Should().Be(2);
        f.AssertNoCommercialPayment();
    }

    [Fact]
    public async Task Processing_replay_observed_from_second_context_does_not_verify_again()
    {
        await using var f = new Fixture();
        f.Verifier.Callback = async request =>
        {
            await using var observer = f.Observe();
            var receiver = f.ReceiverFor(observer);
            var body = Encoding.UTF8.GetBytes(f.Payload().ToJsonString());
            var replay = await receiver.ReceiveAsync(body, Fixture.Sign(body));
            replay.ErrorCode.Should().Be("WOMPI_VERIFICATION_PENDING");
            replay.Value!.ReceiptId.Should().NotBeEmpty();
            return Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, f.At));
        };
        (await f.Send(f.Payload())).IsSuccess.Should().BeTrue();
        f.Verifier.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("transaction")]
    [InlineData("paid-at")]
    public async Task Inconsistent_remote_evidence_remains_pending_without_commercial_payment(string mismatch)
    {
        await using var f = new Fixture();
        f.Verifier.Callback = request => Task.FromResult(Result<WompiVerifiedPayment>.Ok(new(
            mismatch == "transaction" ? Guid.NewGuid() : request.TransactionId,
            mismatch == "paid-at" ? f.At.AddMinutes(3) : f.At)));
        var result = await f.Send(f.Payload());
        result.ErrorCode.Should().Be("WOMPI_VERIFICATION_PENDING");
        result.Value!.ReceiptId.Should().NotBeEmpty();
        (await f.Db.BillingPaymentNotifications.SingleAsync()).VerifiedAt.Should().BeNull();
        f.Intent.Status.Should().Be(BillingCheckoutStatuses.AwaitingPayment);
        f.AssertNoCommercialPayment();
    }

    [Fact]
    public async Task Direct_receiver_rejects_oversized_body_before_verifier_or_inbox()
    {
        await using var f = new Fixture();
        var body = new byte[65537];
        var result = await f.Receiver.ReceiveAsync(body, Fixture.Sign(body));
        result.ErrorCode.Should().Be("WOMPI_PAYLOAD_TOO_LARGE");
        f.AssertNoReceipt();
    }
    [Fact]
    public async Task Decimal_scale_only_replay_reuses_receipt_without_new_remote_verification()
    {
        await using var f = new Fixture();
        var originalJson = f.Payload().ToJsonString();
        originalJson.Should().Contain("\"Monto\":12.34");
        var originalBytes = Encoding.UTF8.GetBytes(originalJson);
        var original = await f.Receiver.ReceiveAsync(originalBytes, Fixture.Sign(originalBytes));
        original.IsSuccess.Should().BeTrue(original.Error);
        var scaledJson = originalJson.Replace("\"Monto\":12.34", "\"Monto\":12.3400");
        scaledJson.Should().NotBe(originalJson);
        var scaledBytes = Encoding.UTF8.GetBytes(scaledJson);

        var replay = await f.Receiver.ReceiveAsync(scaledBytes, Fixture.Sign(scaledBytes));

        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value.Should().BeEquivalentTo(original.Value);
        f.Verifier.Calls.Should().Be(1);
        f.Db.BillingPaymentNotifications.Should().ContainSingle();
        f.AssertNoCommercialPayment();
    }
    [Fact]
    public async Task Verified_notice_recovers_provider_link_identity_after_checkout_ack_was_lost()
    {
        await using var f = new Fixture();
        f.Intent.ExternalCheckoutId = null;
        f.Intent.ProviderAcknowledgedAt = null;
        f.Intent.Status = BillingCheckoutStatuses.RequiresReconciliation;
        await f.Db.SaveChangesAsync();

        var result = await f.Send(f.Payload());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(BillingPaymentNotificationStatuses.VerifiedSandbox);
        await using var observer = f.Observe();
        var intent = await observer.BillingCheckoutIntents.SingleAsync();
        intent.ExternalCheckoutId.Should().Be("12345");
        intent.Status.Should().Be(BillingCheckoutStatuses.PaymentVerifiedSandbox);
        f.Verifier.Calls.Should().Be(1);
        f.AssertNoCommercialPayment();
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public const string Account = "synthetic-wompi-account", Beneficiary = "synthetic-application";
        public const string Secret = "synthetic-webhook-hmac-key-001";
        public BillingSecurityFixture Base { get; } = new();
        public NeoStpDbContext Db => Base.Db;
        public Guid Transaction { get; } = Guid.NewGuid();
        public DateTimeOffset At { get; } = DateTimeOffset.UtcNow;
        public BillingCheckoutIntent Intent { get; }
        public FakeVerifier Verifier { get; } = new();
        public BillingOptions Options { get; } = new()
        {
            Wompi = new() { AppId = "synthetic-oauth-client", ApiSecret = Secret, WebhookEnabled = true, IsProduction = false },
            Checkout = new() { Provider = "Wompi", ProviderAccountId = Account, BeneficiaryId = Beneficiary }
        };
        public WompiWebhookReceiver Receiver => ReceiverFor(Db);
        public Fixture()
        {
            Intent = new() { CorrelationId = Guid.NewGuid(), EmpresaId = BillingSecurityFixture.EmpresaA,
                PlanId = BillingSecurityFixture.Basic, Provider = "Wompi", ProviderAccountId = Account, BeneficiaryId = Beneficiary,
                Amount = 12.34m, Currency = "USD", ExternalCheckoutId = "12345", Status = BillingCheckoutStatuses.AwaitingPayment,
                CreatedAt = At.UtcDateTime.AddMinutes(-1), IsProduction = false };
            Db.BillingCheckoutIntents.Add(Intent); Db.SaveChanges();
            Verifier.Callback = request => Task.FromResult(Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, At)));
        }
        public JsonObject Payload() => JsonSerializer.SerializeToNode(new
        {
            IdCuenta = Account, Aplicativo = new { Id = Beneficiary }, IdTransaccion = Transaction,
            EnlacePago = new { Id = 12345, IdentificadorEnlaceComercio = "neostp:checkout:" + Intent.CorrelationId.ToString("N") },
            Monto = 12.34m, FechaTransaccion = At.ToString("O"), ResultadoTransaccion = "ExitosaAprobada", Cantidad = 1, EsProductiva = false
        })!.AsObject();
        public Task<Result<WompiWebhookReceipt>> Send(JsonObject payload)
        {
            var body = Encoding.UTF8.GetBytes(payload.ToJsonString());
            return Receiver.ReceiveAsync(body, Sign(body));
        }
        public static string Sign(byte[] body) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body));
        public NeoStpDbContext Observe() => new(new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase(Base.DatabaseName, Base.Store).Options);
        public WompiWebhookReceiver ReceiverFor(NeoStpDbContext db) => new(db, Verifier, Microsoft.Extensions.Options.Options.Create(Options));
        public void AssertNoReceipt() { Db.BillingPaymentNotifications.Should().BeEmpty(); Verifier.Calls.Should().Be(0); }
        public void AssertNoCommercialPayment() { Db.BillingPayments.Should().BeEmpty(); Db.EmpresaPlanes.Should().BeEmpty(); }
        public ValueTask DisposeAsync() => Base.DisposeAsync();
    }
    private sealed class FakeVerifier : IWompiPaymentVerifier
    {
        public int Calls { get; private set; }
        public Func<WompiPaymentVerificationRequest, Task<Result<WompiVerifiedPayment>>> Callback { get; set; } = null!;
        public Task<Result<WompiVerifiedPayment>> VerifyAsync(WompiPaymentVerificationRequest request, CancellationToken ct = default)
        { Calls++; return Callback(request); }
    }
}
