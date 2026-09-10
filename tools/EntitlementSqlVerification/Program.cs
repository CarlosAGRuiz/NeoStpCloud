using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

// Synthetic captured evidence and temporary options only. Never reads application config or real credentials.
const string server = @"(localdb)\NeoStpAuthAudit_20260904";
var suffix = Guid.NewGuid().ToString("N");
var database = "EntitlementAudit_" + suffix;
var connection = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 }.ConnectionString;
NeoStpDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<NeoStpDbContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3)).AddInterceptors(interceptors).Options);
var options = Options.Create(new BillingOptions { PaymentApplication = new() { Enabled = true } });
var audit = Substitute.For<IAuditoriaService>();
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
    Check(migrations.Contains("20260905150044_GL1H_AtomicPaymentApplication"), "Real migration chain includes atomic application ledger without a new entitlement migration");
    var plan = new Plan { Codigo = "ENTITLEMENT_SQL", Nombre = "Original purchased plan", PrecioMensual = 10, MonedaCodigo = "USD",
        LimiteUsuarios = 3, LimiteSucursales = 2, LimitePuntosVenta = 4, LimiteDteMensual = 100 };
    var module = new Modulo { Codigo = "ENTITLEMENT_SQL_ORIGINAL", Nombre = "Purchased module", Activo = true };
    var laterModule = new Modulo { Codigo = "ENTITLEMENT_SQL_LATER", Nombre = "Later catalog module", Activo = true };
    schema.Planes.Add(plan); schema.Modulos.AddRange(module, laterModule); await schema.SaveChangesAsync();
    schema.PlanModulos.Add(new() { PlanId = plan.Id, ModuloId = module.Id }); await schema.SaveChangesAsync();
    async Task<Empresa> Company(string code)
    {
        var company = new Empresa { Nit = "RIGHTS-" + code, RazonSocial = "SYNTHETIC " + code, EstadoCodigo = "ACTIVA" };
        schema.Empresas.Add(company); await schema.SaveChangesAsync(); return company;
    }
    BillingCheckoutIntent NewIntent(int companyId, string code) => new() {
        CorrelationId = Guid.NewGuid(), EmpresaId = companyId, PlanId = plan.Id,
        Provider = "Wompi", ProviderAccountId = "synthetic-rights-account", BeneficiaryId = "synthetic-beneficiary", IsProduction = true,
        IdempotencyKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))), RequestFingerprint = new string('A', 64),
        PlanCode = plan.Codigo, PlanName = plan.Nombre, Amount = 10, Currency = "USD", BillingInterval = "MONTH", ExternalPlanId = "synthetic-price",
        ExternalCheckoutId = "synthetic-link-" + code, RedirectUrl = "https://pay.example.invalid/link", ProviderAcknowledgedAt = DateTime.UtcNow,
        SuccessUrl = "https://billing.example.invalid/success", CancelUrl = "https://billing.example.invalid/cancel",
        Status = BillingCheckoutStatuses.AwaitingPayment, LeaseId = Guid.NewGuid().ToString("N"), LeaseExpiresAt = DateTime.UtcNow.AddMinutes(2),
        CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
    async Task<BillingPaymentNotification> PersistIntentAndCapture(BillingCheckoutIntent intent)
    {
        schema.BillingCheckoutIntents.Add(intent); await schema.SaveChangesAsync();
        intent.CommercialSnapshotJson = await BillingCommercialSnapshot.CaptureAsync(schema, intent);
        await schema.SaveChangesAsync();
        var paidAt = DateTimeOffset.UtcNow;
        var receipt = new BillingPaymentNotification {
            ReceiptId = Guid.NewGuid(), Provider = "Wompi", ProviderAccountId = intent.ProviderAccountId, BeneficiaryId = intent.BeneficiaryId,
            IsProduction = true, TransactionId = Guid.NewGuid(), CheckoutCorrelationId = intent.CorrelationId, BillingCheckoutIntentId = intent.Id,
            ExternalCheckoutId = intent.ExternalCheckoutId!, Amount = 10, Currency = "USD", TransactionAt = paidAt,
            PayloadHash = new string('B', 64), SemanticHash = new string('C', 64), Status = BillingPaymentNotificationStatuses.VerifiedCapturedProduction,
            VerifiedAt = DateTime.UtcNow, ProviderPaidAt = paidAt, LeaseId = Guid.NewGuid().ToString("N"), LeaseExpiresAt = DateTime.UtcNow.AddMinutes(2) };
        schema.BillingPaymentNotifications.Add(receipt); await schema.SaveChangesAsync(); return receipt;
    }
    var paidCompany = await Company("PAID");
    var initialIntent = NewIntent(paidCompany.Id, "initial");
    var initialReceipt = await PersistIntentAndCapture(initialIntent);
    await using (var applicationDb = Db())
        Check((await new BillingPaymentApplicationProcessor(applicationDb, options).ApplyVerifiedPaymentAsync(initialReceipt.ReceiptId)).IsSuccess,
            "Synthetic captured evidence creates paid license through actual application processor");
    async Task<EmpresaPlan> PaidLicense()
    {
        await using var observer = Db();
        return await observer.EmpresaPlanes.AsNoTracking().SingleAsync(x => x.EmpresaId == paidCompany.Id && x.EstadoCodigo == "ACTIVO");
    }
    await using (var readerDb = Db())
    {
        var terms = await BillingEntitlementReader.ReadAsync(readerDb, await PaidLicense());
        Check(terms.IsSuccess && terms.Value!.FromPayment && terms.Value.LimiteUsuarios == 3 && terms.Value.LimiteSucursales == 2
            && terms.Value.LimitePuntosVenta == 4 && terms.Value.LimiteDteMensual == 100 && terms.Value.ModuleIds.SequenceEqual(new[] { module.Id }),
            "Paid reader resolves all acquired quotas and module identity from ledger");
    }
    var licenseBeforeAdmin = await PaidLicense();
    await using (var adminDb = Db())
    {
        var admin = new EmpresasService(adminDb, audit);
        var assigned = await admin.AsignarPlanAsync(paidCompany.Id, new() { PlanId = plan.Id }, "synthetic-sql-audit");
        Check(assigned.ErrorCode == "LICENSE_MANAGED_BY_BILLING", "Administrative plan assignment rejects paid ledger under real SQL transaction");
        var extra = await admin.ActivarModuloAsync(paidCompany.Id, laterModule.Id, "synthetic-sql-audit");
        Check(extra.ErrorCode == "LICENSE_MANAGED_BY_BILLING", "Administrative activation rejects module absent from purchased terms");
    }
    var licenseAfterAdmin = await PaidLicense();
    Check(licenseAfterAdmin.Id == licenseBeforeAdmin.Id && licenseAfterAdmin.FechaFin == licenseBeforeAdmin.FechaFin
        && await schema.EmpresaPlanes.CountAsync(x => x.EmpresaId == paidCompany.Id) == 1
        && !await schema.EmpresaModulos.AnyAsync(x => x.EmpresaId == paidCompany.Id && x.ModuloId == laterModule.Id),
        "Rejected administrative mutations preserve exact license and create no extra module row");
    var lockObserver = new CompanyLockObserver($"NeoSTP:BILLING:{paidCompany.Id}");
    await using (var lockConnection = new SqlConnection(connection))
    {
        await lockConnection.OpenAsync();
        await using var heldTransaction = (SqlTransaction)await lockConnection.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var heldCommand = lockConnection.CreateCommand();
        heldCommand.Transaction = heldTransaction;
        heldCommand.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0; SELECT @r;";
        heldCommand.Parameters.AddWithValue("@resource", $"NeoSTP:BILLING:{paidCompany.Id}");
        Check(Convert.ToInt32(await heldCommand.ExecuteScalarAsync()) >= 0, "Synthetic lock holder acquires exact Billing company resource");
        await using var mutatorDb = Db(lockObserver);
        var mutation = new EmpresasService(mutatorDb, audit).DesactivarModuloAsync(paidCompany.Id, module.Id, "synthetic-sql-audit");
        try
        {
            await lockObserver.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            Check(lockObserver.ExpectedResource && !mutation.IsCompleted && lockObserver.CommandsBeyondLock == 0,
                "Administrative mutation reaches shared company lock before running any license action query");
            Check(await schema.EmpresaModulos.AsNoTracking().AnyAsync(x => x.EmpresaId == paidCompany.Id && x.ModuloId == module.Id && x.Activo),
                "Held company lock prevents administrative module state from changing");
        }
        finally { await heldTransaction.CommitAsync(); }
        var disabledResult = await mutation.WaitAsync(TimeSpan.FromSeconds(20));
        Check(disabledResult.IsSuccess && !disabledResult.Value!.Modulos.Single(x => x.ModuloId == module.Id).Activo,
            "Purchased module deactivation succeeds after SQL company lock release");
    }
    await using (var reactivationDb = Db())
    {
        var enabledResult = await new EmpresasService(reactivationDb, audit).ActivarModuloAsync(paidCompany.Id, module.Id, "synthetic-sql-audit");
        Check(enabledResult.IsSuccess && enabledResult.Value!.Modulos.Single(x => x.ModuloId == module.Id).Activo,
            "Purchased module reactivation commits under real administrative Billing transaction");
    }
    Check(await schema.EmpresaModulos.AsNoTracking().AnyAsync(x => x.EmpresaId == paidCompany.Id && x.ModuloId == module.Id
        && x.Activo && x.FechaInactivacion == null), "Administrative reactivation persists acquired module before renewal snapshot capture");

    var originalLicense = await PaidLicense();
    var initialApplication = await schema.BillingPaymentApplications.AsNoTracking().SingleAsync(x => x.BillingCheckoutIntentId == initialIntent.Id);
    var subscription = await schema.BillingSubscriptions.AsNoTracking().SingleAsync(x => x.Id == initialApplication.BillingSubscriptionId);
    var renewal = NewIntent(paidCompany.Id, "renewal");
    renewal.BillingCustomerId = subscription.BillingCustomerId; renewal.BillingSubscriptionId = subscription.Id; renewal.EmpresaPlanId = originalLicense.Id;
    await using (var policyDb = Db())
        Check((await BillingCheckoutPolicy.ValidateAsync(policyDb, renewal, DateTime.UtcNow)).IsSuccess,
            "Prepaid renewal with unchanged purchased terms passes transition policy");
    var renewalReceipt = await PersistIntentAndCapture(renewal);
    await using (var applicationDb = Db())
        Check((await new BillingPaymentApplicationProcessor(applicationDb, options).ApplyVerifiedPaymentAsync(renewalReceipt.ReceiptId)).IsSuccess,
            "Prepaid renewal applies the second finite period");
    var renewedLicense = await PaidLicense();
    var renewalApplication = await schema.BillingPaymentApplications.AsNoTracking().SingleAsync(x => x.BillingCheckoutIntentId == renewal.Id);
    Check(renewedLicense.FechaInicio == originalLicense.FechaInicio && renewedLicense.FechaInicio <= DateTime.UtcNow
        && renewalApplication.PeriodStart == initialApplication.PeriodEnd && renewedLicense.FechaFin == initialApplication.PeriodEnd.AddMonths(1),
        "Prepaid renewal retains current access start while extending exactly one future month");
    await using (var readerDb = Db())
        Check((await BillingEntitlementReader.ReadAsync(readerDb, renewedLicense)).IsSuccess,
            "Latest paid ledger remains readable before prepaid renewal period begins");
    await using (var resolverDb = Db())
    {
        var licenseDto = await new EmpresasService(resolverDb, audit).ResolveAsync(paidCompany.Id);
        Check(licenseDto?.Vigente == true && licenseDto.PlanNombre == "Original purchased plan" && licenseDto.LimiteDteMensual == 100,
            "Resolver does not falsely expire prepaid renewal before next period starts");
    }

    await schema.Planes.Where(x => x.Id == plan.Id).ExecuteUpdateAsync(s => s
        .SetProperty(x => x.Codigo, "ENTITLEMENT_SQL_CHANGED").SetProperty(x => x.Nombre, "Edited live catalog")
        .SetProperty(x => x.LimiteUsuarios, 999).SetProperty(x => x.LimiteSucursales, 999)
        .SetProperty(x => x.LimitePuntosVenta, 999).SetProperty(x => x.LimiteDteMensual, 999));
    await schema.PlanModulos.Where(x => x.PlanId == plan.Id && x.ModuloId == module.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, false));
    schema.PlanModulos.Add(new() { PlanId = plan.Id, ModuloId = laterModule.Id, Activo = true }); await schema.SaveChangesAsync();
    await using (var readerDb = Db())
    {
        var terms = await BillingEntitlementReader.ReadAsync(readerDb, await PaidLicense());
        Check(terms.IsSuccess && terms.Value!.PlanCode == "ENTITLEMENT_SQL" && terms.Value.PlanName == "Original purchased plan"
            && terms.Value.LimiteUsuarios == 3 && terms.Value.LimiteSucursales == 2 && terms.Value.LimitePuntosVenta == 4
            && terms.Value.LimiteDteMensual == 100 && terms.Value.ModuleIds.SequenceEqual(new[] { module.Id }),
            "Live catalog name code quotas and memberships cannot rewrite purchased rights");
        var nextRenewal = NewIntent(paidCompany.Id, "next-renewal");
        nextRenewal.BillingCustomerId = subscription.BillingCustomerId; nextRenewal.BillingSubscriptionId = subscription.Id; nextRenewal.EmpresaPlanId = renewedLicense.Id;
        Check((await BillingCheckoutPolicy.ValidateAsync(readerDb, nextRenewal, DateTime.UtcNow)).ErrorCode == "BILLING_TRANSITION_UNSUPPORTED",
            "Renewal policy rejects changed live quotas or module membership before external checkout");
    }
    await using (var resolverDb = Db())
    {
        var licenseDto = await new EmpresasService(resolverDb, audit).ResolveAsync(paidCompany.Id);
        Check(licenseDto?.Vigente == true && licenseDto.LimiteUsuarios == 3 && licenseDto.PlanNombre == "Original purchased plan"
            && licenseDto.Modulos.Single(x => x.ModuloId == module.Id).Activo && licenseDto.Modulos.Single(x => x.ModuloId == module.Id).IncluidoEnPlan,
            "Resolver exposes purchased name quotas and module despite later plan membership edits");
        var dashboard = await new DashboardService(resolverDb).GetDashboardEmpresaAsync(paidCompany.Id);
        Check(dashboard.LimiteDteMensual == 100 && dashboard.PlanNombre == "Original purchased plan",
            "Dashboard reads acquired DTE quota instead of changed live catalog");
    }

    var legacyCompany = await Company("LEGACY");
    var legacyLicense = new EmpresaPlan { EmpresaId = legacyCompany.Id, PlanId = plan.Id, FechaInicio = DateTime.UtcNow.AddDays(-1), FechaFin = DateTime.UtcNow.AddDays(30) };
    schema.EmpresaPlanes.Add(legacyLicense); await schema.SaveChangesAsync();
    await using (var legacyDb = Db())
    {
        var terms = await BillingEntitlementReader.ReadAsync(legacyDb, legacyLicense);
        Check(terms.IsSuccess && !terms.Value!.FromPayment && terms.Value.LimiteDteMensual == 999
            && terms.Value.PlanName == "Edited live catalog" && terms.Value.ModuleIds.SequenceEqual(new[] { laterModule.Id }),
            "Company without adoption ledger retains explicit legacy live-plan fallback");
    }
    await schema.Modulos.Where(x => x.Id == module.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, false));
    await using (var switchedDb = Db())
    {
        var terms = await BillingEntitlementReader.ReadAsync(switchedDb, await PaidLicense());
        Check(terms.IsSuccess && terms.Value!.ModuleIds.Contains(module.Id), "Global module kill switch preserves purchased snapshot validity");
        var licenseDto = await new EmpresasService(switchedDb, audit).ResolveAsync(paidCompany.Id);
        Check(licenseDto?.Vigente == true && !licenseDto.Modulos.Single(x => x.ModuloId == module.Id).Activo,
            "Resolver applies global module kill switch without inventing license expiry");
    }
    await schema.Modulos.Where(x => x.Id == module.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, true).SetProperty(x => x.Codigo, "REPURPOSED_MODULE"));
    await using (var repurposedDb = Db())
        Check((await BillingEntitlementReader.ReadAsync(repurposedDb, await PaidLicense())).ErrorCode == "LICENSE_SNAPSHOT_INVALID",
            "Reusing purchased module identity with a different code fails closed");
    await schema.Modulos.Where(x => x.Id == module.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Codigo, "ENTITLEMENT_SQL_ORIGINAL"));

    var originalSnapshot = renewalApplication.CommercialSnapshotJson;
    await schema.BillingPaymentApplications.Where(x => x.Id == renewalApplication.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CommercialSnapshotJson, "{}"));
    await schema.BillingCheckoutIntents.Where(x => x.Id == renewal.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CommercialSnapshotJson, "{}"));
    await using (var corruptedDb = Db())
    {
        Check((await BillingEntitlementReader.ReadAsync(corruptedDb, await PaidLicense())).ErrorCode == "LICENSE_SNAPSHOT_INVALID",
            "Matching but malformed ledger and checkout snapshots cannot fall back to unlimited live rights");
        var licenseDto = await new EmpresasService(corruptedDb, audit).ResolveAsync(paidCompany.Id);
        Check(licenseDto?.Vigente == false && licenseDto.LimiteUsuarios == 0 && licenseDto.LimiteDteMensual == 0
            && licenseDto.Modulos.All(x => !x.Activo), "Malformed paid snapshot disables resolver rights and returns zero quotas");
        Check((await new LicenciaGuardService(corruptedDb).ValidarLimiteAsync(paidCompany.Id, RecursoLimitado.DteMensual)).ErrorCode == "LICENSE_SNAPSHOT_INVALID",
            "Quota guard propagates strict paid snapshot failure");
        var dashboard = await new DashboardService(corruptedDb).GetDashboardEmpresaAsync(paidCompany.Id);
        Check(dashboard.PlanNombre is null && dashboard.LimiteDteMensual == 0,
            "Malformed snapshot dashboard cannot advertise an unlimited active plan");
    }
    await schema.BillingPaymentApplications.Where(x => x.Id == renewalApplication.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CommercialSnapshotJson, originalSnapshot));
    await schema.BillingCheckoutIntents.Where(x => x.Id == renewal.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CommercialSnapshotJson, originalSnapshot));
    await schema.EmpresaPlanes.Where(x => x.Id == renewedLicense.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.EstadoCodigo, "CANCELADO"));
    var replacement = new EmpresaPlan { EmpresaId = paidCompany.Id, PlanId = plan.Id, FechaInicio = DateTime.UtcNow.AddMinutes(-1), FechaFin = DateTime.UtcNow.AddMonths(1) };
    schema.EmpresaPlanes.Add(replacement); await schema.SaveChangesAsync();
    await using (var replacementDb = Db())
    {
        Check((await BillingEntitlementReader.ReadAsync(replacementDb, replacement)).ErrorCode == "LICENSE_SNAPSHOT_INVALID",
            "Replacing an adopted paid license cannot regain legacy live-plan fallback");
        var licenseDto = await new EmpresasService(replacementDb, audit).ResolveAsync(paidCompany.Id);
        Check(licenseDto?.Vigente == false && licenseDto.LimiteDteMensual == 0, "Resolver rejects replacement license without its own correlated ledger");
    }
    Check(await schema.BillingPaymentApplications.CountAsync() == 2 && await schema.BillingPayments.CountAsync() == 2,
        "Entitlement reads and catalog edits create no extra payments or application periods");
    completed = true;
    Console.WriteLine($"Completed: {checks.Count}/{checks.Count}. Synthetic payment truth and isolated real SQL only.");
}
finally
{
    var actual = new SqlConnectionStringBuilder(schema.Database.GetConnectionString());
    if (actual.DataSource != server || actual.InitialCatalog != database || database != "EntitlementAudit_" + suffix
        || !Guid.TryParseExact(suffix, "N", out _))
        throw new InvalidOperationException("Refusing cleanup: unexpected server/database identity.");
    await schema.Database.EnsureDeletedAsync();
    Console.WriteLine("Deleted owned synthetic database: " + database);
    var evidence = Environment.GetEnvironmentVariable("NEOSTP_ENTITLEMENT_SQL_EVIDENCE");
    if (completed && !string.IsNullOrWhiteSpace(evidence))
    {
        var absolute = Path.GetFullPath(evidence);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, JsonSerializer.Serialize(new {
            generatedAtUtc = DateTime.UtcNow, syntheticServer = server, syntheticDatabase = database,
            migrationsApplied = true, appliedMigrations = migrations, syntheticDatabaseDeleted = true,
            passed = checks.Count, failed = 0, checks,
            scope = "LocalDB with synthetic captured-production fixtures only; no provider network host application settings or active customer database"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

sealed class CompanyLockObserver(string resource) : DbCommandInterceptor
{
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int beyondLock;
    public Task Entered => entered.Task;
    public bool ExpectedResource { get; private set; }
    public int CommandsBeyondLock => Volatile.Read(ref beyondLock);
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("sys.sp_getapplock", StringComparison.Ordinal))
        {
            ExpectedResource = command.Parameters.Cast<DbParameter>().Any(x => x.Value is string value && value == resource);
            entered.TrySetResult();
        }
        else Interlocked.Increment(ref beyondLock);
        return ValueTask.FromResult(result);
    }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref beyondLock);
        return ValueTask.FromResult(result);
    }
}