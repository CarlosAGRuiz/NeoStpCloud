using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

// Isolated real SQL, fake verifier and synthetic HMAC material only. Never reads app configuration.
// Parent owns the lifecycle of the dedicated instance; this program owns only its GUID database.
const string connectionVariable = "NEOSTP_SQLSERVER_TEST_CONNECTION";
const string localDbServer = @"(localdb)\NeoStpAuthAudit_20260904";
var configuredRoot = Environment.GetEnvironmentVariable(connectionVariable);
var rootBuilder = string.IsNullOrWhiteSpace(configuredRoot)
    ? new SqlConnectionStringBuilder { DataSource = localDbServer, IntegratedSecurity = true }
    : new SqlConnectionStringBuilder(configuredRoot);
var server = rootBuilder.DataSource;
var suffix = Guid.NewGuid().ToString("N");
var database = "WompiWebhookAudit_" + suffix;
rootBuilder.InitialCatalog = database;
rootBuilder.TrustServerCertificate = true;
rootBuilder.ConnectTimeout = 10;
var connection = rootBuilder.ConnectionString;
NeoStpDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<NeoStpDbContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3)).AddInterceptors(interceptors).Options);
const string account = "synthetic-webhook-account";
const string beneficiary = "synthetic-webhook-beneficiary";
const string syntheticSecret = "synthetic-hmac-not-a-real-credential";
var options = Options.Create(new BillingOptions {
    Checkout = new() { Provider = "Wompi", ProviderAccountId = account, BeneficiaryId = beneficiary },
    Wompi = new() { WebhookEnabled = true, IsProduction = false, AppId = "synthetic-app", ApiSecret = syntheticSecret } });
var verifier = new SyntheticVerifier();
WompiWebhookReceiver Receiver(NeoStpDbContext db) => new(db, verifier, options);
string Sign(byte[] body) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(syntheticSecret), body));
var syntheticLinks = new Dictionary<Guid, int>();
int LinkFor(Guid correlation)
{
    if (!syntheticLinks.TryGetValue(correlation, out var link))
    { link = 100 + syntheticLinks.Count; syntheticLinks.Add(correlation, link); }
    return link;
}
byte[] Body(Guid correlation, Guid transaction, DateTimeOffset at, decimal amount = 10) => JsonSerializer.SerializeToUtf8Bytes(new {
    IdCuenta = account, Aplicativo = new { Id = beneficiary }, IdTransaccion = transaction.ToString(),
    EnlacePago = new { Id = LinkFor(correlation), IdentificadorEnlaceComercio = "neostp:checkout:" + correlation.ToString("N") },
    Monto = amount, FechaTransaccion = at.ToString("O"), Cantidad = 1, ResultadoTransaccion = "ExitosaAprobada", EsProductiva = false });
var checks = new List<string>();
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    checks.Add(label); Console.WriteLine("PASS: " + label);
}
var completed = false;
string[] migrations = [];
await using var schema = Db();
try
{
    await schema.Database.MigrateAsync();
    migrations = (await schema.Database.GetAppliedMigrationsAsync()).ToArray();
    Check(!await schema.BillingPaymentNotifications.AnyAsync(), "Real migration chain creates empty payment notification inbox");
    var plan = new Plan { Codigo = "WEBHOOK_SQL", Nombre = "Synthetic webhook plan", PrecioMensual = 10, MonedaCodigo = "USD" };
    schema.Planes.Add(plan); await schema.SaveChangesAsync();
    async Task<BillingCheckoutIntent> Intent(string code)
    {
        var company = new Empresa { Nit = "WEBHOOK-" + code, RazonSocial = "SYNTHETIC " + code, EstadoCodigo = "ACTIVA" };
        schema.Empresas.Add(company); await schema.SaveChangesAsync();
        var row = new BillingCheckoutIntent {
            CorrelationId = Guid.NewGuid(), EmpresaId = company.Id, PlanId = plan.Id,
            Provider = "Wompi", ProviderAccountId = account, BeneficiaryId = beneficiary, IsProduction = false,
            IdempotencyKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))),
            RequestFingerprint = new string('A', 64), PlanCode = plan.Codigo, PlanName = plan.Nombre, Amount = 10, Currency = "USD",
            ExternalPlanId = "synthetic-price", SuccessUrl = "https://billing.example.invalid/success", CancelUrl = "https://billing.example.invalid/cancel",
            Status = BillingCheckoutStatuses.Processing, LeaseId = Guid.NewGuid().ToString("N"), LeaseExpiresAt = DateTime.UtcNow.AddMinutes(2),
            CreatedAt = DateTime.UtcNow };
        schema.BillingCheckoutIntents.Add(row); await schema.SaveChangesAsync(); return row;
    }
    async Task<BillingPaymentNotification> Notification(Guid transaction)
    {
        await using var observer = Db();
        return await observer.BillingPaymentNotifications.AsNoTracking().SingleAsync(x => x.TransactionId == transaction
            && x.ProviderAccountId == account && !x.IsProduction);
    }
    var raceIntent = await Intent("RACE");
    var raceTransaction = Guid.NewGuid();
    var raceAt = DateTimeOffset.UtcNow;
    var raceBody = Body(raceIntent.CorrelationId, raceTransaction, raceAt);
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    verifier.OnVerify = async request =>
    {
        var receipt = await Notification(raceTransaction);
        Check(receipt.Status == BillingPaymentNotificationStatuses.Processing && receipt.BillingCheckoutIntentId == raceIntent.Id,
            "Independent SQL context sees inbox committed before verifier call");
        Check(receipt.ReceiptId != Guid.Empty && receipt.LeaseExpiresAt > DateTime.UtcNow && !string.IsNullOrEmpty(receipt.LeaseId),
            "Committed inbox includes durable receipt and live lease");
        Check(request.CorrelationId == raceIntent.CorrelationId && request.Amount == 10 && request.Currency == "USD"
            && request.ProviderAccountId == account && request.BeneficiaryId == beneficiary && !request.IsProduction,
            "Verifier receives scoped synthetic checkout expectation");
        entered.TrySetResult();
        await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
        return Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, raceAt));
    };
    var gate = new PairLockGate();
    await using (var firstDb = Db(gate))
    await using (var secondDb = Db(gate))
    {
        var first = Receiver(firstDb).ReceiveAsync(raceBody, Sign(raceBody));
        var second = Receiver(secondDb).ReceiveAsync(raceBody, Sign(raceBody));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var pending = await await Task.WhenAny(first, second).WaitAsync(TimeSpan.FromSeconds(30));
            Check(gate.Arrivals == 2, "Same-event SQL reservations overlap deterministically at application lock");
            Check(pending.ErrorCode == "WOMPI_VERIFICATION_PENDING" && pending.Value is not null,
                "Concurrent active delivery returns pending code mapped to HTTP 503 with durable receipt");
            Check(verifier.Calls == 1, "Concurrent delivery never repeats in-flight verifier call");
        }
        finally { release.TrySetResult(); }
        var outcomes = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));
        Check(outcomes.Count(x => x.IsSuccess) == 1, "One concurrent delivery completes verified receipt");
        Check(outcomes[0].Value!.ReceiptId == outcomes[1].Value!.ReceiptId, "Concurrent deliveries share one receipt identity");
    }
    var raceReceipt = await Notification(raceTransaction);
    Check(await schema.BillingPaymentNotifications.CountAsync(x => x.TransactionId == raceTransaction) == 1 && verifier.Calls == 1,
        "Same event leaves one SQL inbox row and one verification");
    Check(raceReceipt.Status == BillingPaymentNotificationStatuses.VerifiedSandbox && raceReceipt.VerifiedAt is not null
        && raceReceipt.ProviderPaidAt is not null, "Verified sandbox receipt is durable");
    await using (var replayDb = Db())
    {
        using var doc = JsonDocument.Parse(raceBody);
        var reordered = JsonSerializer.SerializeToUtf8Bytes(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        var replay = await Receiver(replayDb).ReceiveAsync(reordered, Sign(reordered));
        Check(replay.IsSuccess && replay.Value!.ReceiptId == raceReceipt.ReceiptId && verifier.Calls == 1,
            "Semantically identical signed replay ignores byte formatting without verification");
    }
    await using (var conflictDb = Db())
    {
        var conflictBody = Body(raceIntent.CorrelationId, raceTransaction, raceAt, 11);
        var conflict = await Receiver(conflictDb).ReceiveAsync(conflictBody, Sign(conflictBody));
        Check(conflict.ErrorCode == "WOMPI_EVENT_CONFLICT" && verifier.Calls == 1,
            "Changed semantics for same transaction conflicts with code mapped to HTTP 409");
    }
    var unknownTransaction = Guid.NewGuid();
    var unknownBody = Body(Guid.NewGuid(), unknownTransaction, DateTimeOffset.UtcNow);
    await using (var unknownDb = Db())
        Check((await Receiver(unknownDb).ReceiveAsync(unknownBody, Sign(unknownBody))).IsSuccess,
            "Unknown checkout notice receives a durable quarantine receipt");
    var unknown = await Notification(unknownTransaction);
    Check(unknown.Status == BillingPaymentNotificationStatuses.RequiresReconciliation && unknown.BillingCheckoutIntentId is null && verifier.Calls == 1,
        "Unknown checkout is quarantined without remote verification");

    var retryIntent = await Intent("RETRY");
    var retryTransaction = Guid.NewGuid();
    var retryAt = DateTimeOffset.UtcNow;
    var retryBody = Body(retryIntent.CorrelationId, retryTransaction, retryAt);
    verifier.OnVerify = _ => Task.FromResult(Result<WompiVerifiedPayment>.Fail("Synthetic read failure"));
    var beforeRetry = verifier.Calls;
    await using (var failedReadDb = Db())
        Check((await Receiver(failedReadDb).ReceiveAsync(retryBody, Sign(retryBody))).ErrorCode == "WOMPI_VERIFICATION_PENDING",
            "Verifier read failure remains retryable with pending delivery code");
    var failedRead = await Notification(retryTransaction);
    Check(failedRead.Status == BillingPaymentNotificationStatuses.RequiresReconciliation && failedRead.VerifiedAt is null,
        "Verifier failure persists unverified reconciliation receipt");
    verifier.OnVerify = request => Task.FromResult(Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, retryAt)));
    await using (var retryDb = Db())
    {
        var recovered = await Receiver(retryDb).ReceiveAsync(retryBody, Sign(retryBody));
        Check(recovered.IsSuccess && recovered.Value!.ReceiptId == failedRead.ReceiptId && verifier.Calls == beforeRetry + 2,
            "Durable retry repeats only read verification and resolves original receipt");
    }

    var commitIntent = await Intent("COMMIT");
    var commitTransaction = Guid.NewGuid();
    var commitAt = DateTimeOffset.UtcNow;
    var commitBody = Body(commitIntent.CorrelationId, commitTransaction, commitAt);
    verifier.OnVerify = request => Task.FromResult(Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, commitAt)));
    var commitFailure = new FailSecondCommit();
    var commitThrew = false;
    await using (var failureDb = Db(commitFailure))
    {
        try { await Receiver(failureDb).ReceiveAsync(commitBody, Sign(commitBody)); }
        catch (InvalidOperationException exception) when (exception.Message == FailSecondCommit.Marker) { commitThrew = true; }
    }
    Check(commitThrew && commitFailure.Failures == 1, "Final SQL transaction commit failure is actually injected after verifier success");
    var interrupted = await Notification(commitTransaction);
    await using (var observed = Db())
    {
        var intent = await observed.BillingCheckoutIntents.AsNoTracking().SingleAsync(x => x.Id == commitIntent.Id);
        Check(interrupted.Status == BillingPaymentNotificationStatuses.Processing && interrupted.VerifiedAt is null
            && intent.Status == BillingCheckoutStatuses.Processing, "Failed final commit rolls back receipt and intent atomically");
        await observed.BillingPaymentNotifications.Where(x => x.Id == interrupted.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
    }
    await using (var recoveryDb = Db())
    {
        var recovered = await Receiver(recoveryDb).ReceiveAsync(commitBody, Sign(commitBody));
        Check(recovered.IsSuccess && recovered.Value!.ReceiptId == interrupted.ReceiptId,
            "Expired lease retry recovers final commit failure using original receipt");
    }

    var uniqueTransaction = Guid.NewGuid();
    BillingPaymentNotification Unique(string merchant, bool production) => new() {
        ReceiptId = Guid.NewGuid(), ProviderAccountId = merchant, BeneficiaryId = beneficiary, IsProduction = production,
        TransactionId = uniqueTransaction, CheckoutCorrelationId = Guid.NewGuid(), ExternalCheckoutId = "987", Amount = 10,
        TransactionAt = DateTimeOffset.UtcNow, PayloadHash = new string('B', 64), SemanticHash = new string('C', 64),
        Status = BillingPaymentNotificationStatuses.RequiresReconciliation, LeaseId = Guid.NewGuid().ToString("N"), LeaseExpiresAt = DateTime.UtcNow };
    await using (var uniqueDb = Db())
    { uniqueDb.BillingPaymentNotifications.Add(Unique(account, false)); await uniqueDb.SaveChangesAsync(); }
    var duplicateRejected = false;
    await using (var duplicateDb = Db())
    {
        duplicateDb.BillingPaymentNotifications.Add(Unique(account, false));
        try { await duplicateDb.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        { duplicateRejected = true; }
    }
    Check(duplicateRejected, "SQL unique constraint rejects duplicate provider account mode transaction tuple");
    await using (var isolatedDb = Db())
    {
        // Schema-only synthetic rows; production receiver/provider mode is never enabled.
        isolatedDb.BillingPaymentNotifications.AddRange(Unique("synthetic-other-account", false), Unique(account, true));
        await isolatedDb.SaveChangesAsync();
        Check(await isolatedDb.BillingPaymentNotifications.CountAsync(x => x.TransactionId == uniqueTransaction) == 3,
            "Schema scopes transaction identity independently by account and production mode");
    }

    var canceledIntent = await Intent("CANCELED");
    var end = DateTime.UtcNow.AddDays(-1);
    var customer = new BillingCustomer { EmpresaId = canceledIntent.EmpresaId, Provider = "Wompi", Email = "synthetic@example.invalid" };
    schema.BillingCustomers.Add(customer); await schema.SaveChangesAsync();
    var subscription = new BillingSubscription { BillingCustomerId = customer.Id, PlanId = plan.Id, Status = SubscriptionStatus.Canceled,
        CanceledAt = end, CurrentPeriodEnd = end, CancelAtPeriodEnd = false };
    var license = new EmpresaPlan { EmpresaId = canceledIntent.EmpresaId, PlanId = plan.Id, FechaInicio = end.AddDays(-30), FechaFin = end, EstadoCodigo = "INACTIVO" };
    schema.BillingSubscriptions.Add(subscription); schema.EmpresaPlanes.Add(license); await schema.SaveChangesAsync();
    canceledIntent.BillingCustomerId = customer.Id; canceledIntent.BillingSubscriptionId = subscription.Id; canceledIntent.EmpresaPlanId = license.Id;
    await schema.SaveChangesAsync();
    var cancelAt = DateTimeOffset.UtcNow;
    verifier.OnVerify = request => Task.FromResult(Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, cancelAt)));
    var cancelBody = Body(canceledIntent.CorrelationId, Guid.NewGuid(), cancelAt);
    await using (var canceledDb = Db())
        Check((await Receiver(canceledDb).ReceiveAsync(cancelBody, Sign(cancelBody))).IsSuccess,
            "Synthetic verification can record late sandbox evidence for canceled subscription");
    await using (var finalDb = Db())
    {
        var savedLicense = await finalDb.EmpresaPlanes.AsNoTracking().SingleAsync(x => x.Id == license.Id);
        var savedSubscription = await finalDb.BillingSubscriptions.AsNoTracking().SingleAsync(x => x.Id == subscription.Id);
        Check(savedLicense.EstadoCodigo == "INACTIVO" && savedLicense.FechaFin == end
            && savedSubscription.Status == SubscriptionStatus.Canceled && savedSubscription.CurrentPeriodEnd == end,
            "Verified sandbox evidence never revives canceled subscription or revoked license");
        Check(!await finalDb.BillingPayments.AnyAsync() && await finalDb.EmpresaPlanes.CountAsync() == 1,
            "Verification creates no commercial payments or additional licenses");
        Check(await finalDb.BillingCheckoutIntents.CountAsync(x => x.Status == BillingCheckoutStatuses.PaymentVerifiedSandbox) == 4
            && !await finalDb.BillingCheckoutIntents.AnyAsync(x => x.Status == BillingCheckoutStatuses.Completed),
            "Verified intents reach PAYMENT_VERIFIED_SANDBOX and never commercial COMPLETED");
    }
    var callsBeforeBadHmac = verifier.Calls;
    var rowsBeforeBadHmac = await schema.BillingPaymentNotifications.CountAsync();
    await using (var badHmacDb = Db())
        Check((await Receiver(badHmacDb).ReceiveAsync(raceBody, new string('0', 64))).ErrorCode == "WOMPI_SIGNATURE_INVALID",
            "Invalid HMAC is rejected before inbox persistence");
    Check(verifier.Calls == callsBeforeBadHmac && await schema.BillingPaymentNotifications.CountAsync() == rowsBeforeBadHmac,
        "Invalid HMAC creates neither receipt nor verifier call");
    completed = true;
    Console.WriteLine($"Completed: {checks.Count}/{checks.Count}. Synthetic HMAC/verifier, real isolated SQL only.");
}
finally
{
    var actual = new SqlConnectionStringBuilder(schema.Database.GetConnectionString());
    if (actual.DataSource != server || actual.InitialCatalog != database || database != "WompiWebhookAudit_" + suffix
        || !Guid.TryParseExact(suffix, "N", out _))
        throw new InvalidOperationException("Refusing cleanup: unexpected server/database identity.");
    await schema.Database.EnsureDeletedAsync();
    Console.WriteLine("Deleted owned synthetic database: " + database);
    var evidence = Environment.GetEnvironmentVariable("NEOSTP_WOMPI_WEBHOOK_SQL_EVIDENCE");
    if (completed && !string.IsNullOrWhiteSpace(evidence))
    {
        var absolute = Path.GetFullPath(evidence);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, JsonSerializer.Serialize(new {
            generatedAtUtc = DateTime.UtcNow, syntheticServer = server, syntheticDatabase = database,
            migrationsApplied = true, appliedMigrations = migrations, syntheticDatabaseDeleted = true,
            passed = checks.Count, failed = 0, checks,
            scope = "Real LocalDB; synthetic HMAC/verifier only; HTTP status mapping is not exercised; no external calls or commercial payment/license application"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

sealed class SyntheticVerifier : IWompiPaymentVerifier
{
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public Func<WompiPaymentVerificationRequest, Task<Result<WompiVerifiedPayment>>>? OnVerify { get; set; }
    public Task<Result<WompiVerifiedPayment>> VerifyAsync(WompiPaymentVerificationRequest request, CancellationToken ct = default)
    { Interlocked.Increment(ref calls); return OnVerify?.Invoke(request) ?? throw new InvalidOperationException("Verifier fixture missing"); }
}
sealed class PairLockGate : DbCommandInterceptor
{
    private int arrivals;
    private readonly TaskCompletionSource both = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Arrivals => Volatile.Read(ref arrivals);
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("sys.sp_getapplock", StringComparison.Ordinal)
            && command.Parameters.Cast<DbParameter>().Any(p => p.Value is string s && s.StartsWith("NeoSTP:WOMPI:", StringComparison.Ordinal)))
        {
            if (Interlocked.Increment(ref arrivals) == 2) both.TrySetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        return result;
    }
}
sealed class FailSecondCommit : DbTransactionInterceptor
{
    public const string Marker = "Synthetic final webhook commit failure";
    private int commits;
    private int failures;
    public int Failures => Volatile.Read(ref failures);
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref commits) == 2)
        { Interlocked.Increment(ref failures); throw new InvalidOperationException(Marker); }
        return ValueTask.FromResult(result);
    }
}
