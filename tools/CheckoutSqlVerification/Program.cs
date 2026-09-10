using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;
using NSubstitute;

// Never reads app settings or credentials. No network provider, client database or payment.
// The parent controls the lifecycle of this dedicated LocalDB instance.
const string server = @"(localdb)\NeoStpAuthAudit_20260904";
var suffix = Guid.NewGuid().ToString("N");
var database = "CheckoutAudit_" + suffix;
var connection = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 }.ConnectionString;
NeoStpDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<NeoStpDbContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3)).AddInterceptors(interceptors).Options);
const string successUrl = "https://billing.example.invalid/success";
var configuration = new BillingOptions { Provider = "Wompi", Checkout = new() {
    Enabled = true, Provider = "Wompi", ProviderAccountId = "synthetic-account-A",
    BeneficiaryId = "synthetic-beneficiary", SuccessUrl = successUrl,
    CancelUrl = "https://billing.example.invalid/cancel", LeaseSeconds = 120 } };
var options = Options.Create(configuration);
var provider = new SyntheticCheckoutProvider();
var resolver = new PaymentProviderResolver([provider, new MockPaymentProvider(), new TransferenciaPaymentProvider()], options);
var email = Substitute.For<IEmailSender>();
BillingService Service(NeoStpDbContext db, Usuario user)
{
    var identity = Substitute.For<ICurrentUser>();
    identity.IsAuthenticated.Returns(true); identity.UserId.Returns(user.Id); identity.EmpresaId.Returns(user.EmpresaId);
    identity.TipoUsuarioCodigo.Returns("ADMIN"); identity.IsInRole("ADMIN").Returns(true);
    return new(db, resolver, email, options, identity);
}
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
    Check(migrations.Any(x => x.Contains("GL1H", StringComparison.Ordinal)), "Real migration chain includes GL1H");
    var plan = new Plan { Codigo = "CHECKOUT_AUDIT", Nombre = "Synthetic checkout plan", PrecioMensual = 10, MonedaCodigo = "USD" };
    schema.Planes.Add(plan); await schema.SaveChangesAsync();
    schema.BillingPlanProviderMappings.Add(new() { PlanId = plan.Id, Provider = "Wompi", ExternalPlanId = "synthetic-price",
        UnitAmount = 10, Currency = "USD", IsActive = true });
    await schema.SaveChangesAsync();
    async Task<Usuario> Tenant(string code)
    {
        var empresa = new Empresa { Nit = "CHECKOUT-" + code, RazonSocial = "SYNTHETIC " + code, EstadoCodigo = "ACTIVA" };
        schema.Empresas.Add(empresa); await schema.SaveChangesAsync();
        var user = new Usuario { EmpresaId = empresa.Id, Username = "checkout-" + code, Email = code + "@example.invalid",
            NombreCompleto = "Synthetic administrator", PasswordHash = "synthetic-not-a-password", TipoUsuarioCodigo = "ADMIN", EstadoCodigo = "ACTIVO" };
        schema.Usuarios.Add(user); await schema.SaveChangesAsync(); return user;
    }
    CreateCheckoutRequest Request(Usuario user, string key) => new(user.EmpresaId!.Value, plan.Id, successUrl, "Wompi", key);
    async Task<BillingCheckoutIntent> Stored(Usuario user)
    {
        await using var observer = Db();
        return await observer.BillingCheckoutIntents.AsNoTracking().SingleAsync(x => x.EmpresaId == user.EmpresaId);
    }
    var raceUser = await Tenant("RACE");
    var pair = new PairLockGate();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    provider.OnCheckout = async request =>
    {
        var stored = await Stored(raceUser);
        Check(stored.CorrelationId == request.CorrelationId && stored.Status == BillingCheckoutStatuses.Processing,
            "Independent SQL context observes committed intent before external effect");
        Check(stored.Amount == 10 && stored.Currency == "USD" && stored.PlanId == plan.Id
            && stored.ProviderAccountId == request.ProviderAccountId && stored.BeneficiaryId == request.BeneficiaryId,
            "Committed commercial snapshot matches provider payload");
        Check(!string.IsNullOrEmpty(stored.LeaseId) && stored.LeaseExpiresAt > DateTime.UtcNow,
            "Committed checkout has a live lease before provider effect");
        entered.TrySetResult();
        await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
        return SyntheticCheckoutProvider.Accept(request);
    };
    await using (var firstDb = Db(pair))
    await using (var secondDb = Db(pair))
    {
        var request = Request(raceUser, "same-key-sql-0001");
        var first = Service(firstDb, raceUser).CreateCheckoutSessionAsync(request);
        var second = Service(secondDb, raceUser).CreateCheckoutSessionAsync(request);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var loser = await await Task.WhenAny(first, second).WaitAsync(TimeSpan.FromSeconds(30));
            Check(loser.ErrorCode == "BILLING_CHECKOUT_PENDING", "Concurrent same-key loser observes pending intent");
            Check(pair.Arrivals == 2, "Two SQL reservations overlap at company application lock without sleeps");
            await using var differentDb = Db();
            var different = await Service(differentDb, raceUser).CreateCheckoutSessionAsync(Request(raceUser, "different-key-0001"));
            Check(different.ErrorCode == "BILLING_CHECKOUT_PENDING", "Different key is blocked while provider is in flight");
            var cancel = await Service(differentDb, raceUser).CancelSubscriptionAsync(new(raceUser.EmpresaId!.Value));
            Check(cancel.ErrorCode == "BILLING_CHECKOUT_PENDING", "Committed checkout excludes concurrent cancellation");
        }
        finally { release.TrySetResult(); }
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));
        Check(results.Count(x => x.IsSuccess) == 1 && provider.Calls == 1, "Concurrent same key performs exactly one external checkout");
    }
    var acknowledged = await Stored(raceUser);
    Check(await schema.BillingCheckoutIntents.CountAsync(x => x.EmpresaId == raceUser.EmpresaId) == 1, "One intent row survives same-key race");
    Check(acknowledged.ProviderAcknowledgedAt is not null && acknowledged.Status == BillingCheckoutStatuses.AwaitingPayment
        && acknowledged.ExternalCheckoutId is not null, "Checkout ACK is durable before URL is returned");
    await using (var replayDb = Db())
    {
        var replay = await Service(replayDb, raceUser).CreateCheckoutSessionAsync(Request(raceUser, "same-key-sql-0001"));
        Check(replay.IsSuccess && replay.Value!.SessionId == acknowledged.ExternalCheckoutId && provider.Calls == 1,
            "Independent context replays durable ACK without another provider call");
    }
    provider.OnCheckout = request => Task.FromResult(SyntheticCheckoutProvider.Accept(request));

    var timeoutUser = await Tenant("TIMEOUT");
    provider.OnCheckout = _ => throw new TimeoutException("Synthetic ambiguous provider timeout");
    var beforeTimeout = provider.Calls;
    await using (var timeoutDb = Db())
        Check((await Service(timeoutDb, timeoutUser).CreateCheckoutSessionAsync(Request(timeoutUser, "timeout-key-0001"))).ErrorCode
            == "BILLING_RECONCILIATION_REQUIRED", "Provider timeout requires reconciliation");
    await using (var timeoutReplayDb = Db())
        Check((await Service(timeoutReplayDb, timeoutUser).CreateCheckoutSessionAsync(Request(timeoutUser, "timeout-key-0001"))).ErrorCode
            == "BILLING_RECONCILIATION_REQUIRED", "Restart replay retains timeout quarantine");
    Check(provider.Calls == beforeTimeout + 1 && (await Stored(timeoutUser)).ProviderAcknowledgedAt is null,
        "Timeout is never resent and invents no ACK");

    var ackFailUser = await Tenant("ACKFAIL");
    provider.OnCheckout = request => Task.FromResult(SyntheticCheckoutProvider.Accept(request));
    var failure = new FailAckOnce();
    var beforeAckFailure = provider.Calls;
    await using (var failedDb = Db(failure))
        Check((await Service(failedDb, ackFailUser).CreateCheckoutSessionAsync(Request(ackFailUser, "ackfail-key-0001"))).ErrorCode
            == "BILLING_RECONCILIATION_REQUIRED", "Injected SQL ACK failure returns no checkout URL");
    Check(failure.Failures == 1, "SQL ACK persistence failure was actually injected");
    var ackFailed = await Stored(ackFailUser);
    Check(ackFailed.ProviderAcknowledgedAt is null && ackFailed.Status == BillingCheckoutStatuses.RequiresReconciliation,
        "Failed ACK persistence leaves durable unconfirmed quarantine");
    await using (var failedReplayDb = Db())
        Check((await Service(failedReplayDb, ackFailUser).CreateCheckoutSessionAsync(Request(ackFailUser, "ackfail-key-0001"))).ErrorCode
            == "BILLING_RECONCILIATION_REQUIRED", "ACK failure replay remains quarantined");
    Check(provider.Calls == beforeAckFailure + 1, "ACK failure recovery never repeats provider request");

    provider.OnCheckout = request => Task.FromResult(SyntheticCheckoutProvider.Accept(request, "shared-external-id"));
    var identityA = await Tenant("IDENTITYA");
    var identityB = await Tenant("IDENTITYB");
    await using (var a = Db())
        Check((await Service(a, identityA).CreateCheckoutSessionAsync(Request(identityA, "identity-key-0001"))).IsSuccess,
            "First account-scoped external checkout identity is accepted");
    await using (var b = Db())
        Check((await Service(b, identityB).CreateCheckoutSessionAsync(Request(identityB, "identity-key-0001"))).ErrorCode
            == "BILLING_RECONCILIATION_REQUIRED", "SQL unique constraint rejects external identity reused in same account");
    Check((await Stored(identityB)).ProviderAcknowledgedAt is null, "Duplicate external session cannot acquire durable ACK");
    configuration.Checkout.ProviderAccountId = "synthetic-account-B";
    var identityC = await Tenant("IDENTITYC");
    await using (var c = Db())
        Check((await Service(c, identityC).CreateCheckoutSessionAsync(Request(identityC, "identity-key-0001"))).IsSuccess,
            "Same external identity in a different provider account is independent");

    var cancellationUser = await Tenant("CANCEL");
    schema.BillingProviderOperations.Add(new() { EmpresaId = cancellationUser.EmpresaId!.Value, PlanId = plan.Id,
        Provider = "Wompi", IdempotencyKey = "synthetic-cancellation", Status = BillingProviderOperationStatuses.Pending });
    await schema.SaveChangesAsync();
    var beforeCancellation = provider.Calls;
    await using (var cancelDb = Db())
        Check((await Service(cancelDb, cancellationUser).CreateCheckoutSessionAsync(Request(cancellationUser, "cancel-key-0001"))).ErrorCode
            == "BILLING_CANCELLATION_PENDING", "Pending cancellation excludes new checkout");
    Check(provider.Calls == beforeCancellation && !await schema.BillingCheckoutIntents.AnyAsync(x => x.EmpresaId == cancellationUser.EmpresaId),
        "Cancellation exclusion creates neither checkout row nor provider effect");
    Check(!await schema.BillingPayments.AnyAsync() && !await schema.EmpresaPlanes.AnyAsync(),
        "Checkout creation and ACK never create payments or grant licenses");
    Check(provider.LegacyCalls == 0, "No legacy amount-less checkout or customer call occurs");
    completed = true;
    Console.WriteLine($"Completed: {checks.Count}/{checks.Count}. Synthetic provider, real isolated SQL only.");
}
finally
{
    var actual = new SqlConnectionStringBuilder(schema.Database.GetConnectionString());
    if (actual.DataSource != server || actual.InitialCatalog != database || database != "CheckoutAudit_" + suffix
        || !Guid.TryParseExact(suffix, "N", out _))
        throw new InvalidOperationException("Refusing cleanup: unexpected server/database identity.");
    await schema.Database.EnsureDeletedAsync();
    Console.WriteLine("Deleted owned synthetic database: " + database);
    var evidence = Environment.GetEnvironmentVariable("NEOSTP_CHECKOUT_SQL_EVIDENCE");
    if (completed && !string.IsNullOrWhiteSpace(evidence))
    {
        var absolute = Path.GetFullPath(evidence);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, JsonSerializer.Serialize(new {
            generatedAtUtc = DateTime.UtcNow, syntheticServer = server, syntheticDatabase = database,
            migrationsApplied = true, appliedMigrations = migrations, syntheticDatabaseDeleted = true,
            passed = checks.Count, failed = 0, checks,
            scope = "Real LocalDB; synthetic identities/provider only; no network, payments, customer data or production migration"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
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
sealed class FailAckOnce : DbCommandInterceptor
{
    private int failures;
    public int Failures => Volatile.Read(ref failures);
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.TrimStart().StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("[ExternalCheckoutId]", StringComparison.Ordinal)
            && Interlocked.CompareExchange(ref failures, 1, 0) == 0)
            throw new InvalidOperationException("Synthetic SQL ACK failure");
        return ValueTask.FromResult(result);
    }
}
sealed class SyntheticCheckoutProvider : IPaymentProvider, IBillingCheckoutProvider
{
    private int calls;
    private int legacyCalls;
    public string ProviderName => "Wompi";
    public int Calls => Volatile.Read(ref calls);
    public int LegacyCalls => Volatile.Read(ref legacyCalls);
    public Func<ProviderCheckoutRequest, Task<Result<ProviderCheckoutSession>>>? OnCheckout { get; set; }
    public static Result<ProviderCheckoutSession> Accept(ProviderCheckoutRequest request, string? session = null)
        => Result<ProviderCheckoutSession>.Ok(new("Wompi", request.ProviderAccountId, session ?? "synthetic-" + request.CorrelationId.ToString("N"),
            "https://pay.example.invalid/checkout", DateTime.UtcNow.AddMinutes(15)));
    public Task<Result<ProviderCheckoutSession>> CreateCheckoutAsync(ProviderCheckoutRequest request, CancellationToken ct = default)
    {
        Interlocked.Increment(ref calls);
        return OnCheckout?.Invoke(request) ?? Task.FromResult(Accept(request));
    }
    public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
    { Interlocked.Increment(ref legacyCalls); throw new InvalidOperationException("Unexpected legacy customer effect"); }
    public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
    { Interlocked.Increment(ref legacyCalls); throw new InvalidOperationException("Unexpected legacy checkout effect"); }
    public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default)
        => throw new InvalidOperationException("Unexpected portal effect");
    public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default)
        => throw new InvalidOperationException("Unexpected plan effect");
    public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
        => throw new InvalidOperationException("Unexpected cancellation effect");
}
