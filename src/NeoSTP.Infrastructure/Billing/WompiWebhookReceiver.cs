using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>Signed inbox followed by remote reads. This increment never applies a commercial payment/license.</summary>
public sealed class WompiWebhookReceiver(NeoStpDbContext db, IWompiPaymentVerifier verifier,
    IOptions<BillingOptions> options) : IWompiWebhookReceiver
{
    public const int MaxBodyBytes = 65536;
    private BillingOptions Config => options.Value;

    public async Task<Result<WompiWebhookReceipt>> ReceiveAsync(ReadOnlyMemory<byte> body, string signature, CancellationToken ct = default)
    {
        var wompi = Config.Wompi;
        if (wompi is null || !wompi.WebhookEnabled || wompi.IsProduction
            || Config.Checkout is null || Config.Checkout.Provider != "Wompi"
            || !Identity(Config.Checkout.ProviderAccountId) || !Identity(Config.Checkout.BeneficiaryId)
            || !Identity(wompi.AppId) || string.IsNullOrWhiteSpace(wompi.ApiSecret)
            || wompi.ApiSecret != wompi.ApiSecret.Trim()
            || wompi.ApiSecret.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)
            || wompi.ApiSecret.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase))
            return Failure("WOMPI_WEBHOOK_UNAVAILABLE");
        if (body.Length > MaxBodyBytes) return Failure("WOMPI_PAYLOAD_TOO_LARGE");
        if (body.Length == 0 || signature is null || signature.Length != 64
            || signature.Any(c => !Uri.IsHexDigit(c))) return Failure("WOMPI_SIGNATURE_INVALID");
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(wompi.ApiSecret), body.Span);
        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature)))
            return Failure("WOMPI_SIGNATURE_INVALID");
        var notice = Parse(body);
        if (notice is null) return Failure("WOMPI_PAYLOAD_INVALID");
        if (notice.Account != Config.Checkout.ProviderAccountId || notice.Beneficiary != Config.Checkout.BeneficiaryId
            || notice.IsProduction) return Failure("WOMPI_IDENTITY_MISMATCH");

        var reservation = await Once(() => ReserveAsync(notice, body, ct));
        if (reservation.IsFailure)
            return Result<WompiWebhookReceipt>.FailWithValue(reservation.Value?.Receipt!, "La notificación requiere revisión.", reservation.ErrorCode);
        var work = reservation.Value!;
        if (!work.Dispatch) return Result<WompiWebhookReceipt>.Ok(work.Receipt);
        Result<WompiVerifiedPayment> verified;
        try
        {
            verified = await verifier.VerifyAsync(new(notice.TransactionId, notice.LinkId, notice.Correlation,
                notice.Account, notice.Beneficiary, notice.Amount, "USD", notice.IsProduction), ct);
        }
        catch (Exception)
        {
            return await FinishAsync(work, null);
        }
        var payment = verified.IsSuccess ? verified.Value : null;
        if (payment is not null && (payment.TransactionId != notice.TransactionId
            || (payment.PaidAt - notice.At).Duration() > TimeSpan.FromMinutes(1))) payment = null;
        return await FinishAsync(work, payment);
    }

    private sealed record Notice(string Account, string Beneficiary, Guid TransactionId, Guid Correlation,
        string LinkId, decimal Amount, DateTimeOffset At, bool IsProduction);
    private sealed record Reservation(int Id, string LeaseId, int? EmpresaId, WompiWebhookReceipt Receipt, bool Dispatch);

    private async Task<Result<Reservation>> ReserveAsync(Notice n, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        await using var tx = await LockAsync($"NeoSTP:WOMPI:{n.Account}:{n.TransactionId:N}", ct);
        var semantic = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            n.Account, n.Beneficiary, n.TransactionId, n.Correlation, n.LinkId,
            Amount = n.Amount.ToString("F2", CultureInfo.InvariantCulture),
            At = n.At.ToUniversalTime(), n.IsProduction,
        })));
        var row = await db.BillingPaymentNotifications.SingleOrDefaultAsync(x => x.Provider == "Wompi"
            && x.ProviderAccountId == n.Account && x.TransactionId == n.TransactionId && x.IsProduction == n.IsProduction, ct);
        if (row is not null)
        {
            await db.Entry(row).ReloadAsync(ct);
            if (row.SemanticHash != semantic) return Result<Reservation>.Fail("Evento incompatible.", "WOMPI_EVENT_CONFLICT");
            if (row.Status == BillingPaymentNotificationStatuses.VerifiedSandbox)
                return Result<Reservation>.Ok(new(row.Id, row.LeaseId, null, Receipt(row), false));
            if (row.Status == BillingPaymentNotificationStatuses.Processing && row.LeaseExpiresAt > DateTime.UtcNow)
                return Result<Reservation>.FailWithValue(new(row.Id, row.LeaseId, null, Receipt(row), false),
                    "La verificación está en curso.", "WOMPI_VERIFICATION_PENDING");
        }
        var intent = await db.BillingCheckoutIntents.AsNoTracking().SingleOrDefaultAsync(x => x.CorrelationId == n.Correlation, ct);
        var matches = Matches(intent, n);
        if (row is null)
        {
            row = new BillingPaymentNotification
            {
                ReceiptId = Guid.NewGuid(), ProviderAccountId = n.Account, BeneficiaryId = n.Beneficiary,
                IsProduction = n.IsProduction, TransactionId = n.TransactionId, CheckoutCorrelationId = n.Correlation,
                ExternalCheckoutId = n.LinkId, Amount = n.Amount, TransactionAt = n.At,
                PayloadHash = Convert.ToHexString(SHA256.HashData(body.Span)), SemanticHash = semantic,
                CreatedAt = DateTime.UtcNow,
            };
            db.BillingPaymentNotifications.Add(row);
        }
        row.BillingCheckoutIntentId = matches ? intent!.Id : null;
        row.LeaseId = Guid.NewGuid().ToString("N");
        row.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(2);
        row.Status = matches ? BillingPaymentNotificationStatuses.Processing : BillingPaymentNotificationStatuses.RequiresReconciliation;
        row.LastErrorCode = matches ? null : "WOMPI_CHECKOUT_MISMATCH";
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Result<Reservation>.Ok(new(row.Id, row.LeaseId, matches ? intent!.EmpresaId : null, Receipt(row), matches));
    }

    private async Task<Result<WompiWebhookReceipt>> FinishAsync(Reservation work, WompiVerifiedPayment? payment)
    {
        // Persist result even when delivery request was cancelled after provider verification.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        return await Once(async () =>
        {
            await using var tx = await LockAsync($"NeoSTP:BILLING:{work.EmpresaId}", timeout.Token);
            var row = await db.BillingPaymentNotifications.SingleAsync(x => x.Id == work.Id, timeout.Token);
            await db.Entry(row).ReloadAsync(timeout.Token);
            if (row.Status == BillingPaymentNotificationStatuses.VerifiedSandbox)
                return Result<WompiWebhookReceipt>.Ok(Receipt(row));
            if (row.LeaseId != work.LeaseId || row.Status != BillingPaymentNotificationStatuses.Processing)
                return Pending(row);
            var intent = row.BillingCheckoutIntentId is int intentId
                ? await db.BillingCheckoutIntents.SingleOrDefaultAsync(x => x.Id == intentId, timeout.Token) : null;
            if (intent is not null) await db.Entry(intent).ReloadAsync(timeout.Token);
            var notice = new Notice(row.ProviderAccountId, row.BeneficiaryId, row.TransactionId,
                row.CheckoutCorrelationId, row.ExternalCheckoutId, row.Amount, row.TransactionAt, row.IsProduction);
            if (payment is null || !Matches(intent, notice))
            {
                row.Status = BillingPaymentNotificationStatuses.RequiresReconciliation;
                row.LastErrorCode = "WOMPI_VERIFICATION_PENDING";
            }
            else
            {
                // Verified TEST evidence is not a paid commercial subscription. H05 owns license application.
                row.Status = BillingPaymentNotificationStatuses.VerifiedSandbox;
                row.VerifiedAt = DateTime.UtcNow; row.ProviderPaidAt = payment.PaidAt; row.LastErrorCode = null;
                intent!.ExternalCheckoutId = row.ExternalCheckoutId;
                intent.Status = BillingCheckoutStatuses.PaymentVerifiedSandbox;
                intent.LastErrorCode = null; intent.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(timeout.Token);
            if (tx is not null) await tx.CommitAsync(timeout.Token);
            return row.Status == BillingPaymentNotificationStatuses.VerifiedSandbox ? Result<WompiWebhookReceipt>.Ok(Receipt(row)) : Pending(row);
        });
    }

    private static bool Matches(BillingCheckoutIntent? intent, Notice n)
        => intent is not null && intent.Provider == "Wompi" && intent.ProviderAccountId == n.Account
            && intent.BeneficiaryId == n.Beneficiary && intent.IsProduction == n.IsProduction
            && intent.Amount == n.Amount && intent.Currency == "USD"
            && intent.Status != BillingCheckoutStatuses.Completed
            && (intent.ExternalCheckoutId is null || intent.ExternalCheckoutId == n.LinkId)
            && n.At.UtcDateTime >= intent.CreatedAt.AddMinutes(-5);

    private static Notice? Parse(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicates(root)) return null;
            var account = root.GetProperty("IdCuenta").GetString();
            var app = root.GetProperty("Aplicativo").GetProperty("Id").GetString();
            var transaction = root.GetProperty("IdTransaccion").GetString();
            var link = root.GetProperty("EnlacePago");
            var correlation = link.GetProperty("IdentificadorEnlaceComercio").GetString();
            const string prefix = "neostp:checkout:";
            var amount = root.GetProperty("Monto").GetDecimal();
            var date = root.GetProperty("FechaTransaccion").GetString();
            if (!Identity(account) || !Identity(app) || !Guid.TryParse(transaction, out var transactionId) || transactionId == Guid.Empty
                || correlation is null || !correlation.StartsWith(prefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(correlation[prefix.Length..], "N", out var id) || id == Guid.Empty
                || !link.GetProperty("Id").TryGetInt32(out var linkId) || linkId <= 0
                || root.GetProperty("Cantidad").GetInt32() != 1
                || root.GetProperty("ResultadoTransaccion").GetString() != "ExitosaAprobada"
                || amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount
                || date is null || !Regex.IsMatch(date, @"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)
                || !DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                || at > DateTimeOffset.UtcNow.AddMinutes(5)) return null;
            return new(account!, app!, transactionId, id, linkId.ToString(CultureInfo.InvariantCulture), amount, at,
                root.GetProperty("EsProductiva").GetBoolean());
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return null; }
    }

    private static bool HasDuplicates(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject())
                if (!names.Add(p.Name) || HasDuplicates(p.Value)) return true;
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) if (HasDuplicates(item)) return true;
        return false;
    }
    private static bool Identity(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200
        && value == value.Trim() && !value.Any(char.IsControl);
    private Task<T> Once<T>(Func<Task<T>> operation) => new SingleAttempt(db).ExecuteAsync(operation);
    private sealed class SingleAttempt(DbContext context) : ExecutionStrategy(context, 0, TimeSpan.Zero)
    { protected override bool ShouldRetryOn(Exception exception) => false; }
    private async Task<IDbContextTransaction?> LockAsync(string resource, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @r int;
                    EXEC @r = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                    IF @r < 0 THROW 50001, 'Payment notification reservation unavailable.', 1;
                    """, ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    private static WompiWebhookReceipt Receipt(BillingPaymentNotification row) => new(row.ReceiptId, row.Status);
    private static Result<WompiWebhookReceipt> Pending(BillingPaymentNotification row)
        => Result<WompiWebhookReceipt>.FailWithValue(Receipt(row), "Verificación pendiente; conserve la referencia.", "WOMPI_VERIFICATION_PENDING");
    private static Result<WompiWebhookReceipt> Failure(string code) => Result<WompiWebhookReceipt>.Fail("Notificación no aceptada.", code);
}