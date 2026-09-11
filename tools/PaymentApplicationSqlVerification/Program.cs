using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

// Synthetic captured-production rows ONLY inside a new owned database. No actual payment/provider or host.
// Payment application is enabled only on this in-memory options object, never in application configuration.
const string connectionVariable = "NEOSTP_SQLSERVER_TEST_CONNECTION";
const string localDbServer = @"(localdb)\NeoStpAuthAudit_20260904";
var configuredRoot = Environment.GetEnvironmentVariable(connectionVariable);
var rootBuilder = string.IsNullOrWhiteSpace(configuredRoot)
    ? new SqlConnectionStringBuilder { DataSource = localDbServer, IntegratedSecurity = true }
    : new SqlConnectionStringBuilder(configuredRoot);
var server = rootBuilder.DataSource;
var suffix = Guid.NewGuid().ToString("N");
var database = "PaymentApplicationAudit_" + suffix;
rootBuilder.InitialCatalog = database;
rootBuilder.TrustServerCertificate = true;
rootBuilder.ConnectTimeout = 10;
var connection = rootBuilder.ConnectionString;
NeoStpDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<NeoStpDbContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3)).AddInterceptors(interceptors).Options);
var options = Options.Create(new BillingOptions { PaymentApplication = new() { Enabled = true } });
BillingPaymentApplicationProcessor Processor(NeoStpDbContext db) => new(db, options);
var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks.Add(name); Console.WriteLine("PASS: " + name);
}
var completed = false;
string[] migrations = [];
await using var schema = Db();
try
{
    await schema.Database.MigrateAsync();
    migrations = (await schema.Database.GetAppliedMigrationsAsync()).ToArray();
    Check(!await schema.BillingPaymentApplications.AnyAsync(), "Real migration chain creates empty payment application ledger");
    var plan = new Plan { Codigo = "APPLICATION_SQL", Nombre = "Synthetic application plan", PrecioMensual = 10, MonedaCodigo = "USD" };
    var module = new Modulo { Codigo = "APPLICATION_SQL_MODULE", Nombre = "Synthetic application module", Activo = true };
    schema.Planes.Add(plan); schema.Modulos.Add(module); await schema.SaveChangesAsync();
    schema.PlanModulos.Add(new() { PlanId = plan.Id, ModuloId = module.Id, Activo = true }); await schema.SaveChangesAsync();
    async Task<(BillingCheckoutIntent Intent, BillingPaymentNotification Receipt)> Seed(string code)
    {
        var company = new Empresa { Nit = "APP-" + code, RazonSocial = "SYNTHETIC " + code, EstadoCodigo = "ACTIVA", Correo = "synthetic@example.invalid" };
        schema.Empresas.Add(company); await schema.SaveChangesAsync();
        var now = DateTime.UtcNow;
        var intent = new BillingCheckoutIntent {
            CorrelationId = Guid.NewGuid(), EmpresaId = company.Id, PlanId = plan.Id,
            Provider = "Wompi", ProviderAccountId = "synthetic-application-account", BeneficiaryId = "synthetic-beneficiary", IsProduction = true,
            IdempotencyKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))), RequestFingerprint = new string('A', 64),
            PlanCode = plan.Codigo, PlanName = plan.Nombre, Amount = 10, Currency = "USD", BillingInterval = "MONTH", ExternalPlanId = "synthetic-price",
            SuccessUrl = "https://billing.example.invalid/success", CancelUrl = "https://billing.example.invalid/cancel",
            Status = BillingCheckoutStatuses.AwaitingPayment, LeaseId = Guid.NewGuid().ToString("N"), LeaseExpiresAt = now.AddMinutes(2),
            ExternalCheckoutId = "synthetic-link-" + code, RedirectUrl = "https://pay.example.invalid/checkout", ProviderAcknowledgedAt = now,
            CreatedAt = now };
        schema.BillingCheckoutIntents.Add(intent); await schema.SaveChangesAsync();
        intent.CommercialSnapshotJson = await BillingCommercialSnapshot.CaptureAsync(schema, intent);
        await schema.SaveChangesAsync();
        var receipt = ReceiptFor(intent, new DateTimeOffset(now));
        schema.BillingPaymentNotifications.Add(receipt); await schema.SaveChangesAsync();
        return (intent, receipt);
    }
    BillingPaymentNotification ReceiptFor(BillingCheckoutIntent intent, DateTimeOffset paidAt) => new() {
        ReceiptId = Guid.NewGuid(), Provider = intent.Provider, ProviderAccountId = intent.ProviderAccountId,
        BeneficiaryId = intent.BeneficiaryId, IsProduction = true, TransactionId = Guid.NewGuid(), CheckoutCorrelationId = intent.CorrelationId,
        BillingCheckoutIntentId = intent.Id, ExternalCheckoutId = intent.ExternalCheckoutId!, Amount = intent.Amount, Currency = "USD",
        TransactionAt = paidAt, PayloadHash = new string('B', 64), SemanticHash = new string('C', 64),
        Status = BillingPaymentNotificationStatuses.VerifiedCapturedProduction, VerifiedAt = DateTime.UtcNow, ProviderPaidAt = paidAt,
        LeaseId = Guid.NewGuid().ToString("N"), LeaseExpiresAt = DateTime.UtcNow.AddMinutes(2), CreatedAt = DateTime.UtcNow };
    async Task<BillingPaymentApplication> Application(BillingCheckoutIntent intent)
    {
        await using var observer = Db();
        return await observer.BillingPaymentApplications.AsNoTracking().SingleAsync(x => x.BillingCheckoutIntentId == intent.Id);
    }
    async Task CheckOnePeriod(BillingCheckoutIntent intent, DateTimeOffset paidAt, string label)
    {
        await using var observer = Db();
        var ledger = await observer.BillingPaymentApplications.AsNoTracking().SingleAsync(x => x.BillingCheckoutIntentId == intent.Id);
        var payment = await observer.BillingPayments.AsNoTracking().SingleAsync(x => x.Id == ledger.BillingPaymentId);
        var license = await observer.EmpresaPlanes.AsNoTracking().SingleAsync(x => x.EmpresaId == intent.EmpresaId);
        var subscription = await observer.BillingSubscriptions.AsNoTracking().SingleAsync(x => x.Customer.EmpresaId == intent.EmpresaId);
        Check(ledger.PeriodStart == paidAt.UtcDateTime && ledger.PeriodEnd == paidAt.UtcDateTime.AddMonths(1)
            && license.FechaFin == ledger.PeriodEnd && subscription.CurrentPeriodEnd == ledger.PeriodEnd,
            label + ": one exact finite paid period");
        Check(payment.Amount == 10 && payment.Status == "SUCCEEDED" && payment.Currency == "USD"
            && await observer.BillingPayments.CountAsync(x => x.Subscription.Customer.EmpresaId == intent.EmpresaId) == 1,
            label + ": one correlated payment");
        Check(await observer.EmpresaModulos.CountAsync(x => x.EmpresaId == intent.EmpresaId && x.ModuloId == module.Id && x.Activo) == 1
            && ledger.ModuleIdsJson == JsonSerializer.Serialize(new[] { module.Id }) && ledger.CommercialSnapshotJson == intent.CommercialSnapshotJson,
            label + ": module entitlement and commercial snapshot preserved in ledger");
    }

    var same = await Seed("SAME");
    var sameGate = new PairLockGate();
    await using (var a = Db(sameGate))
    await using (var b = Db(sameGate))
    {
        var results = await Task.WhenAll(Processor(a).ApplyVerifiedPaymentAsync(same.Receipt.ReceiptId),
            Processor(b).ApplyVerifiedPaymentAsync(same.Receipt.ReceiptId)).WaitAsync(TimeSpan.FromSeconds(40));
        Check(sameGate.Arrivals == 2 && results.All(x => x.IsSuccess), "Same-receipt SQL race overlaps at company lock and both deliveries succeed idempotently");
    }
    Check(await schema.BillingPaymentApplications.CountAsync(x => x.BillingCheckoutIntentId == same.Intent.Id) == 1,
        "Same-receipt race creates one application ledger row");
    await CheckOnePeriod(same.Intent, same.Receipt.ProviderPaidAt!.Value, "Same receipt race");

    var doubleReceipt = await Seed("DOUBLE");
    var anotherReceipt = ReceiptFor(doubleReceipt.Intent, doubleReceipt.Receipt.ProviderPaidAt!.Value);
    schema.BillingPaymentNotifications.Add(anotherReceipt); await schema.SaveChangesAsync();
    var doubleGate = new PairLockGate();
    await using (var a = Db(doubleGate))
    await using (var b = Db(doubleGate))
    {
        var results = await Task.WhenAll(Processor(a).ApplyVerifiedPaymentAsync(doubleReceipt.Receipt.ReceiptId),
            Processor(b).ApplyVerifiedPaymentAsync(anotherReceipt.ReceiptId)).WaitAsync(TimeSpan.FromSeconds(40));
        Check(doubleGate.Arrivals == 2 && results.Count(x => x.IsSuccess) == 1
            && results.Count(x => x.ErrorCode == "BILLING_ADDITIONAL_PAYMENT_RECONCILIATION") == 1,
            "Two receipts for one checkout permit one application and quarantine additional capture");
    }
    await CheckOnePeriod(doubleReceipt.Intent, doubleReceipt.Receipt.ProviderPaidAt!.Value, "Two receipts race");

    var rollback = await Seed("ROLLBACK");
    var injected = new FailCommitOnce();
    await using (var failedDb = Db(injected))
        Check((await Processor(failedDb).ApplyVerifiedPaymentAsync(rollback.Receipt.ReceiptId)).ErrorCode == "BILLING_APPLICATION_RETRY_REQUIRED"
            && injected.Failures == 1, "Final SQL commit failure is injected and reported as retry required");
    await using (var observer = Db())
    {
        var companyId = rollback.Intent.EmpresaId;
        Check(!await observer.BillingPaymentApplications.AnyAsync(x => x.BillingCheckoutIntentId == rollback.Intent.Id)
            && !await observer.BillingPayments.AnyAsync(x => x.Subscription.Customer.EmpresaId == companyId)
            && !await observer.EmpresaPlanes.AnyAsync(x => x.EmpresaId == companyId), "Failed commit rolls back ledger payment and license");
        Check(!await observer.BillingCustomers.AnyAsync(x => x.EmpresaId == companyId)
            && !await observer.BillingSubscriptions.AnyAsync(x => x.Customer.EmpresaId == companyId)
            && !await observer.EmpresaModulos.AnyAsync(x => x.EmpresaId == companyId), "Failed commit rolls back customer subscription and modules");
        var intent = await observer.BillingCheckoutIntents.AsNoTracking().SingleAsync(x => x.Id == rollback.Intent.Id);
        Check(intent.Status == BillingCheckoutStatuses.AwaitingPayment && intent.CompletedAt is null,
            "Failed commit leaves checkout open without claiming completed application");
    }
    await using (var retryDb = Db())
        Check((await Processor(retryDb).ApplyVerifiedPaymentAsync(rollback.Receipt.ReceiptId)).IsSuccess,
            "Independent SQL context retries the original receipt after rollback");
    await CheckOnePeriod(rollback.Intent, rollback.Receipt.ProviderPaidAt!.Value, "Rollback recovery");

    var cancelRace = await Seed("CANCEL");
    var pause = new PauseCommitOnce();
    var cancelEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var canceledAt = DateTime.UtcNow;
    await using (var applicationDb = Db(pause))
    {
        var application = Processor(applicationDb).ApplyVerifiedPaymentAsync(cancelRace.Receipt.ReceiptId);
        await pause.Entered.WaitAsync(TimeSpan.FromSeconds(30));
        var cancellation = CancelUnderSameLock(cancelRace.Intent.EmpresaId, canceledAt, cancelEntered);
        try
        {
            await cancelEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Check(await CompanyLockUnavailable(cancelRace.Intent.EmpresaId),
                "Independent SQL connection confirms application holds the exact company lock before commit");
            Check(!cancellation.IsCompleted, "Cancellation under identical company lock waits for pending application commit");
        }
        finally { pause.Release(); }
        Check((await application.WaitAsync(TimeSpan.FromSeconds(30))).IsSuccess, "Application wins its transaction before serialized cancellation");
        await cancellation.WaitAsync(TimeSpan.FromSeconds(30));
    }
    await using (var replayDb = Db())
        Check((await Processor(replayDb).ApplyVerifiedPaymentAsync(cancelRace.Receipt.ReceiptId)).IsSuccess,
            "Receipt replay after cancellation acknowledges original application");
    await using (var canceledDb = Db())
    {
        var sub = await canceledDb.BillingSubscriptions.AsNoTracking().SingleAsync(x => x.Customer.EmpresaId == cancelRace.Intent.EmpresaId);
        var license = await canceledDb.EmpresaPlanes.AsNoTracking().SingleAsync(x => x.EmpresaId == cancelRace.Intent.EmpresaId);
        Check(sub.Status == SubscriptionStatus.Canceled && sub.CanceledAt == canceledAt && license.EstadoCodigo == "INACTIVO"
            && !await canceledDb.EmpresaModulos.AnyAsync(x => x.EmpresaId == cancelRace.Intent.EmpresaId && x.Activo),
            "Replay never revives canceled subscription license or modules");
        Check(await canceledDb.BillingPaymentApplications.CountAsync(x => x.BillingCheckoutIntentId == cancelRace.Intent.Id) == 1
            && await canceledDb.BillingPayments.CountAsync(x => x.Subscription.Customer.EmpresaId == cancelRace.Intent.EmpresaId) == 1,
            "Cancellation and replay preserve exactly one original payment and application");
    }

    var uniqueColumns = await schema.Database.SqlQueryRaw<string>("""
        SELECT c.name AS [Value] FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.object_id=OBJECT_ID(N'dbo.Billing_PaymentApplications') AND i.is_unique=1 AND i.is_primary_key=0
        """).ToArrayAsync();
    Check(new[] { "BillingCheckoutIntentId", "BillingPaymentNotificationId", "BillingPaymentId" }.All(uniqueColumns.Contains)
        && uniqueColumns.Length == 3, "Migrated SQL ledger independently enforces unique checkout receipt and payment identities");
    var original = await Application(same.Intent);
    var duplicateRejected = false;
    await using (var duplicateDb = Db())
    {
        duplicateDb.BillingPaymentApplications.Add(new() { BillingCheckoutIntentId = original.BillingCheckoutIntentId,
            BillingPaymentNotificationId = original.BillingPaymentNotificationId, BillingPaymentId = original.BillingPaymentId,
            BillingSubscriptionId = original.BillingSubscriptionId, EmpresaPlanId = original.EmpresaPlanId,
            PeriodStart = original.PeriodStart, PeriodEnd = original.PeriodEnd, AppliedAt = DateTime.UtcNow });
        try { await duplicateDb.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is SqlException sql && sql.Number is 2601 or 2627) { duplicateRejected = true; }
    }
    Check(duplicateRejected, "SQL rejects duplicate application ledger insertion");

    var disabled = await Seed("DISABLED");
    await using (var disabledDb = Db())
    {
        var processor = new BillingPaymentApplicationProcessor(disabledDb, Options.Create(new BillingOptions()));
        Check((await processor.ApplyVerifiedPaymentAsync(disabled.Receipt.ReceiptId)).ErrorCode == "BILLING_APPLICATION_DISABLED",
            "Default gate blocks production-truth synthetic receipt without application");
    }
    Check(!await schema.BillingPaymentApplications.AnyAsync(x => x.BillingCheckoutIntentId == disabled.Intent.Id),
        "Disabled application gate leaves no ledger effects");
    completed = true;
    Console.WriteLine($"Completed: {checks.Count}/{checks.Count}. Synthetic captured evidence only; no external payment.");
}
finally
{
    var actual = new SqlConnectionStringBuilder(schema.Database.GetConnectionString());
    if (actual.DataSource != server || actual.InitialCatalog != database || database != "PaymentApplicationAudit_" + suffix
        || !Guid.TryParseExact(suffix, "N", out _))
        throw new InvalidOperationException("Refusing cleanup: unexpected server/database identity.");
    await schema.Database.EnsureDeletedAsync();
    Console.WriteLine("Deleted owned synthetic database: " + database);
    var evidence = Environment.GetEnvironmentVariable("NEOSTP_PAYMENT_APPLICATION_SQL_EVIDENCE");
    if (completed && !string.IsNullOrWhiteSpace(evidence))
    {
        var absolute = Path.GetFullPath(evidence);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, JsonSerializer.Serialize(new {
            generatedAtUtc = DateTime.UtcNow, syntheticServer = server, syntheticDatabase = database,
            migrationsApplied = true, appliedMigrations = migrations, syntheticDatabaseDeleted = true,
            passed = checks.Count, failed = 0, checks,
            scope = "Real isolated LocalDB; synthetic captured-production rows and options only; no provider host customer database or commercial external effects"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

async Task<bool> CompanyLockUnavailable(int empresaId)
{
    await using var sql = new SqlConnection(connection);
    await sql.OpenAsync();
    await using var transaction = (SqlTransaction)await sql.BeginTransactionAsync();
    await using var command = sql.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0; SELECT @r;";
    command.Parameters.AddWithValue("@resource", $"NeoSTP:BILLING:{empresaId}");
    var result = Convert.ToInt32(await command.ExecuteScalarAsync());
    await transaction.RollbackAsync();
    return result == -1;
}
async Task CancelUnderSameLock(int empresaId, DateTime canceledAt, TaskCompletionSource entered)
{
    await using var sql = new SqlConnection(connection);
    await sql.OpenAsync();
    await using var transaction = (SqlTransaction)await sql.BeginTransactionAsync(IsolationLevel.Serializable);
    await using var command = sql.CreateCommand();
    command.Transaction = transaction; command.CommandTimeout = 30;
    command.CommandText = """
        DECLARE @r int;
        EXEC @r = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
        IF @r < 0 THROW 50001, 'Synthetic cancellation lock unavailable.', 1;
        UPDATE s SET s.Status='CANCELED', s.CanceledAt=@at, s.CancelAtPeriodEnd=0
        FROM Billing_Subscriptions s JOIN Billing_Customers c ON s.BillingCustomerId=c.Id WHERE c.EmpresaId=@company;
        UPDATE Core_EmpresaPlan SET EstadoCodigo='INACTIVO', FechaFin=@at WHERE EmpresaId=@company;
        UPDATE Core_EmpresaModulos SET Activo=0, FechaInactivacion=@at WHERE EmpresaId=@company;
        """;
    command.Parameters.AddWithValue("@resource", $"NeoSTP:BILLING:{empresaId}");
    command.Parameters.AddWithValue("@company", empresaId);
    command.Parameters.Add(new SqlParameter("@at", SqlDbType.DateTime2) { Value = canceledAt });
    entered.TrySetResult();
    await command.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
}
sealed class PairLockGate : DbCommandInterceptor
{
    private int arrivals;
    private readonly TaskCompletionSource both = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Arrivals => Volatile.Read(ref arrivals);
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("sys.sp_getapplock", StringComparison.Ordinal))
        {
            if (Interlocked.Increment(ref arrivals) == 2) both.TrySetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        return result;
    }
}
sealed class FailCommitOnce : DbTransactionInterceptor
{
    private int failures;
    public int Failures => Volatile.Read(ref failures);
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref failures, 1, 0) == 0) throw new InvalidOperationException("Synthetic application commit failure");
        return ValueTask.FromResult(result);
    }
}
sealed class PauseCommitOnce : DbTransactionInterceptor
{
    private int paused;
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Entered => entered.Task;
    public void Release() => release.TrySetResult();
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref paused, 1, 0) == 0)
        { entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken); }
        return result;
    }
}
