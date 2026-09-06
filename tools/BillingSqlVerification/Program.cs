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
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

// Never reads application settings, company data, credentials or real payment providers.
// Only this hard-coded dedicated audit instance and this newly generated database are allowed.
const string server = @"(localdb)\NeoStpAuthAudit_20260904";
var suffix = Guid.NewGuid().ToString("N");
var database = "BillingAudit_" + suffix;
var connection = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 }.ConnectionString;
NeoStpDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<NeoStpDbContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3)).AddInterceptors(interceptors).Options);
var options = Options.Create(new BillingOptions { Provider = "Mock", TrialDays = 14 });
var providers = new PaymentProviderResolver([new MockPaymentProvider(), new TransferenciaPaymentProvider()], options);
var email = Substitute.For<IEmailSender>();
BillingService Service(
    NeoStpDbContext db,
    Usuario user,
    IBillingProviderOperationProcessor? operationProcessor = null)
{
    var current = Substitute.For<ICurrentUser>();
    current.IsAuthenticated.Returns(true); current.UserId.Returns(user.Id); current.EmpresaId.Returns(user.EmpresaId);
    current.TipoUsuarioCodigo.Returns(user.TipoUsuarioCodigo); current.IsInRole(user.TipoUsuarioCodigo).Returns(true);
    return new(db, providers, email, options, current, operationProcessor);
}
int checks = 0;
var passedChecks = new List<string>();
var verificationCompleted = false;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passedChecks.Add(name);
    checks++;
}
await using var schema = Db();
try
{
    // Apply the real migration chain; EnsureCreated would bypass every Up method and
    // could hide a production migration failure.
    await schema.Database.MigrateAsync();
    Console.WriteLine("Isolated SQL database: " + database);
    var basic = new Plan { Codigo = "BILLING_AUDIT_BASIC", Nombre = "Synthetic basic", PrecioMensual = 10, MonedaCodigo = "USD" };
    var pro = new Plan { Codigo = "BILLING_AUDIT_PRO", Nombre = "Synthetic pro", PrecioMensual = 50, MonedaCodigo = "USD" };
    schema.Planes.AddRange(basic, pro);
    var role = await schema.Roles.SingleAsync(r => r.Codigo == "SUPERADMIN" && r.EmpresaId == null && r.EsSistema);
    var central = new Usuario { Username = "audit-central", Email = "central@example.invalid", NombreCompleto = "Synthetic central",
        PasswordHash = "synthetic-not-a-password", TipoUsuarioCodigo = "SUPERADMIN", EstadoCodigo = "ACTIVO",
        Roles = [new UsuarioRol { RolId = role.Id }] };
    schema.Usuarios.Add(central); await schema.SaveChangesAsync();
    async Task<Usuario> Tenant(string code)
    {
        var company = new Empresa { Nit = "AUDIT-" + code, RazonSocial = "SYNTHETIC BILLING " + code, EstadoCodigo = "ACTIVA" };
        schema.Empresas.Add(company); await schema.SaveChangesAsync();
        var user = new Usuario { EmpresaId = company.Id, Username = "audit-" + code, Email = code + "@example.invalid",
            PasswordHash = "synthetic-not-a-password", NombreCompleto = "Synthetic administrator", TipoUsuarioCodigo = "ADMIN", EstadoCodigo = "ACTIVO" };
        schema.Usuarios.Add(user); await schema.SaveChangesAsync(); return user;
    }

    async Task<(Usuario User, int OperationId, int SubscriptionId, int LicenseId, string Key)>
        SeedProviderOperation(string code)
    {
        var user = await Tenant(code);
        await using var seed = Db();
        var customer = new BillingCustomer
        {
            EmpresaId = user.EmpresaId!.Value,
            Provider = BlockingPaymentProvider.Name,
            Email = code + "@example.invalid",
            ExternalCustomerId = "cus-" + code,
        };
        var subscription = new BillingSubscription
        {
            Customer = customer,
            PlanId = basic.Id,
            Status = SubscriptionStatus.Active,
            ExternalSubscriptionId = "sub-" + code,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-1),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(29),
        };
        var license = new EmpresaPlan
        {
            EmpresaId = user.EmpresaId.Value,
            PlanId = basic.Id,
            FechaInicio = DateTime.UtcNow.AddDays(-1),
            FechaFin = DateTime.UtcNow.AddDays(29),
            EstadoCodigo = "ACTIVO",
        };
        seed.AddRange(subscription, license);
        await seed.SaveChangesAsync();
        var key = "billing-audit-" + code + "-" + suffix;
        var operation = new BillingProviderOperation
        {
            EmpresaId = user.EmpresaId.Value,
            BillingSubscriptionId = subscription.Id,
            EmpresaPlanId = license.Id,
            PlanId = basic.Id,
            Provider = BlockingPaymentProvider.Name,
            OperationType = BillingProviderOperationTypes.CancelSubscription,
            IdempotencyKey = key,
            ExternalResourceId = subscription.ExternalSubscriptionId,
            RequestedAt = DateTime.UtcNow,
            Status = BillingProviderOperationStatuses.Pending,
            NextAttemptAt = DateTime.UtcNow,
        };
        seed.BillingProviderOperations.Add(operation);
        await seed.SaveChangesAsync();
        return (user, operation.Id, subscription.Id, license.Id, key);
    }

    var trialUser = await Tenant("TRIAL");
    var trialGate = new PairLockGate();
    var trials = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
    {
        await using var db = Db(trialGate);
        return await Service(db, trialUser).StartTrialAsync(new(trialUser.EmpresaId!.Value, basic.Id, "audit@example.invalid"));
    }));
    Check(trialGate.Arrivals == 2, "Two SQL trial transactions overlap at company application-lock acquisition (no sleeps)");
    Check(trials.Count(x => x.IsSuccess) == 1 && trials.Count(x => x.ErrorCode == "BILLING_TRIAL_ALREADY_USED") == 1,
        "Concurrent trial has one winner and one business rejection, despite configured SQL retries");
    Check(await schema.BillingSubscriptions.CountAsync(s => s.Customer.EmpresaId == trialUser.EmpresaId) == 1,
        "Exactly one trial subscription persisted");
    var trialLicense = await schema.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.EmpresaId == trialUser.EmpresaId);
    Check(trialLicense.FechaFin is not null, "SQL trial license has finite expiry");
    await using (var db = Db())
    {
        var service = Service(db, trialUser);
        Check((await service.ChangePlanAsync(new(trialUser.EmpresaId!.Value, pro.Id))).IsSuccess, "SQL trial can change plan within original period");
        Check((await db.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.EmpresaId == trialUser.EmpresaId)).FechaFin == trialLicense.FechaFin,
            "SQL plan change preserves exact trial expiry");
        Check((await service.CancelSubscriptionAsync(new(trialUser.EmpresaId.Value, false))).IsSuccess, "SQL trial cancellation succeeds");
        Check((await service.StartTrialAsync(new(trialUser.EmpresaId.Value, basic.Id, "audit@example.invalid"))).ErrorCode == "BILLING_TRIAL_ALREADY_USED",
            "Canceled SQL trial cannot restart its free period");
    }

    var transferUser = await Tenant("TRANSFER");
    var transferGate = new PairLockGate();
    var transfers = await Task.WhenAll(new[] { basic.Id, pro.Id }.Select(async planId =>
    {
        await using var db = Db(transferGate);
        return await Service(db, transferUser).IniciarTransferenciaAsync(new(transferUser.EmpresaId!.Value, planId));
    }));
    Check(transferGate.Arrivals == 2, "Two SQL transfer transactions overlap at application lock (no sleeps)");
    Check(transfers.Count(x => x.IsSuccess) == 1 && transfers.Count(x => x.ErrorCode == "BILLING_TRANSFER_PENDING") == 1,
        "Concurrent transfers for different plans produce one pending payment and one rejection");
    var payment = await schema.BillingPayments.AsNoTracking().Include(p => p.Subscription).SingleAsync(p => p.Subscription.Customer.EmpresaId == transferUser.EmpresaId);
    Check(payment.Amount == (payment.Subscription.PlanId == basic.Id ? basic.PrecioMensual : pro.PrecioMensual),
        "Pending SQL payment amount matches unchanged winning plan");
    Check(await schema.EmpresaPlanes.CountAsync(p => p.EmpresaId == transferUser.EmpresaId) == 0,
        "Pending SQL transfer does not activate any license");
    await using (var db = Db())
    {
        var service = Service(db, central);
        Check((await service.ConfirmarTransferenciaAsync(payment.Id, "untrusted-actor")).IsSuccess, "Central SQL verification activates only the winning plan");
        Check((await service.ConfirmarTransferenciaAsync(payment.Id, "untrusted-actor")).ErrorCode == "ESTADO_INVALIDO",
            "Repeated SQL confirmation rejected without extending paid period");
    }
    var paidLicense = await schema.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.EmpresaId == transferUser.EmpresaId);
    Check(paidLicense.PlanId == payment.Subscription.PlanId && paidLicense.FechaFin is not null,
        "SQL paid license is finite and matches the plan actually paid");
    Check((await schema.BillingPayments.AsNoTracking().SingleAsync(p => p.Id == payment.Id)).VerificadoPor == central.Username,
        "SQL verification actor comes from persisted central identity, not caller text");
    var originalPaidEnd = paidLicense.FechaFin;
    await using (var db = Db())
    {
        Check((await Service(db, transferUser).CancelSubscriptionAsync(new(transferUser.EmpresaId!.Value, true))).IsSuccess,
            "SQL end-of-period cancellation succeeds");
        var scheduled = await db.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.EmpresaId == transferUser.EmpresaId);
        Check(scheduled.EstadoCodigo == "ACTIVO" && scheduled.FechaFin == originalPaidEnd,
            "SQL scheduled cancellation preserves exact original paid end without extension");
        Check((await Service(db, transferUser).GetActiveSubscriptionAsync(transferUser.EmpresaId.Value)).Value?.CancelAtPeriodEnd == true,
            "SQL scheduled subscription remains visible while finite license is current");
        Check((await Service(db, transferUser).CancelSubscriptionAsync(new(transferUser.EmpresaId.Value, false))).IsSuccess,
            "SQL scheduled cancellation can be converted to immediate");
        var revoked = await db.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.EmpresaId == transferUser.EmpresaId);
        Check(revoked.EstadoCodigo == "CANCELADO" && revoked.FechaFin <= DateTime.UtcNow,
            "SQL immediate cancellation revokes current license without deleting it");
        Check((await Service(db, transferUser).CancelSubscriptionAsync(new(transferUser.EmpresaId.Value, true))).IsSuccess,
            "SQL repeated cancellation is idempotent");
        var stillRevoked = await db.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.EmpresaId == transferUser.EmpresaId);
        Check(stillRevoked.EstadoCodigo == "CANCELADO" && stillRevoked.FechaFin == revoked.FechaFin,
            "SQL repeated scheduled request cannot revive or extend immediate cancellation");
        Check((await new LicenciaGuardService(db).ValidarLimiteAsync(
            transferUser.EmpresaId.Value, RecursoLimitado.DteMensual)).ErrorCode == "LICENSE_INVALID",
            "Real SQL DTE license guard rejects immediately canceled company");
    }

    var operationSeed = await SeedProviderOperation("PROVIDER-OP");
    var operationUser = operationSeed.User;
    var operationProvider = new BlockingPaymentProvider();
    var operationOptions = Options.Create(new BillingOptions
    {
        Provider = BlockingPaymentProvider.Name,
        ProviderOperations = new BillingProviderOperationsOptions { BatchSize = 20, LeaseSeconds = 120 },
    });
    var operationResolver = new PaymentProviderResolver([operationProvider], operationOptions);
    var operationId = operationSeed.OperationId;
    var operationKey = operationSeed.Key;

    // The creating DbContext is disposed: this proves the committed operation survives
    // the request scope and can be resumed by an independent worker context.
    await using var operationDb1 = Db();
    await using var operationDb2 = Db();
    var processor1 = new BillingProviderOperationProcessor(operationDb1, operationResolver, email,
        operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
    var processor2 = new BillingProviderOperationProcessor(operationDb2, operationResolver, email,
        operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
    var firstProcessing = processor1.ProcessAsync(operationId);
    await operationProvider.Entered.WaitAsync(TimeSpan.FromSeconds(30));
    var competing = await processor2.ProcessAsync(operationId);
    Check(competing.ErrorCode == "BILLING_OPERATION_IN_PROGRESS",
        "SQL lease prevents a second processor from repeating the provider cancellation");
    operationProvider.Release();
    Check((await firstProcessing).IsSuccess, "Independent SQL worker completes committed provider operation");
    Check(operationProvider.Calls == 1, "External provider cancellation executes exactly once");
    var completedOperation = await schema.BillingProviderOperations.AsNoTracking().SingleAsync(o => o.Id == operationId);
    Check(completedOperation.Status == BillingProviderOperationStatuses.Completed
          && completedOperation.ProviderConfirmedAt is not null && completedOperation.CompletedAt is not null,
        "Provider ACK and local completion are durably correlated");
    Check((await schema.BillingSubscriptions.AsNoTracking().SingleAsync(s => s.Id == completedOperation.BillingSubscriptionId)).Status
          == SubscriptionStatus.Canceled,
        "SQL provider completion cancels the correlated subscription");
    Check((await schema.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.Id == completedOperation.EmpresaPlanId)).EstadoCodigo
          == "CANCELADO",
        "SQL provider completion revokes only the correlated license");

    await using (var duplicateDb = Db())
    {
        duplicateDb.BillingProviderOperations.Add(new BillingProviderOperation
        {
            EmpresaId = completedOperation.EmpresaId,
            BillingSubscriptionId = completedOperation.BillingSubscriptionId,
            EmpresaPlanId = completedOperation.EmpresaPlanId,
            PlanId = completedOperation.PlanId,
            Provider = BlockingPaymentProvider.Name,
            OperationType = BillingProviderOperationTypes.CancelSubscription,
            IdempotencyKey = operationKey,
            RequestedAt = DateTime.UtcNow,
            Status = BillingProviderOperationStatuses.Pending,
        });
        var duplicateRejected = false;
        try { await duplicateDb.SaveChangesAsync(); }
        catch (DbUpdateException) { duplicateRejected = true; }
        Check(duplicateRejected, "SQL unique idempotency key rejects duplicate external intent");
    }

    int ambiguousOperationId;
    await using (var ambiguousDb = Db())
    {
        var completed = await ambiguousDb.BillingProviderOperations.AsNoTracking().SingleAsync(o => o.Id == operationId);
        var ambiguous = new BillingProviderOperation
        {
            EmpresaId = completed.EmpresaId,
            BillingSubscriptionId = completed.BillingSubscriptionId,
            EmpresaPlanId = completed.EmpresaPlanId,
            PlanId = completed.PlanId,
            Provider = completed.Provider,
            OperationType = completed.OperationType,
            IdempotencyKey = operationKey + "-ambiguous",
            ExternalResourceId = completed.ExternalResourceId,
            RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            Status = BillingProviderOperationStatuses.Processing,
            Attempts = 1,
            LeaseId = "expired-synthetic-lease",
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        ambiguousDb.BillingProviderOperations.Add(ambiguous);
        await ambiguousDb.SaveChangesAsync();
        ambiguousOperationId = ambiguous.Id;
    }
    var callsBeforeReconciliation = operationProvider.Calls;
    await using (var recoveryDb = Db())
    {
        var recovery = new BillingProviderOperationProcessor(recoveryDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        Check(await recovery.ProcessPendingAsync() >= 1, "SQL recovery scanner finds expired operation lease");
    }
    var ambiguousStored = await schema.BillingProviderOperations.AsNoTracking().SingleAsync(o => o.Id == ambiguousOperationId);
    Check(ambiguousStored.Status == BillingProviderOperationStatuses.RequiresReconciliation,
        "Expired SQL lease without durable provider ACK is quarantined for reconciliation");
    Check(operationProvider.Calls == callsBeforeReconciliation,
        "Ambiguous SQL operation is never resent automatically to the provider");

    var preflightSeed = await SeedProviderOperation("PREFLIGHTA");
    var unrelatedTenant = await Tenant("PREFLIGHTB");
    await using (var corruptDb = Db())
    {
        await corruptDb.BillingProviderOperations.Where(o => o.Id == preflightSeed.OperationId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.EmpresaId, unrelatedTenant.EmpresaId!.Value));
    }
    var callsBeforePreflight = operationProvider.Calls;
    await using (var preflightDb = Db())
    {
        var processor = new BillingProviderOperationProcessor(preflightDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        Check((await processor.ProcessAsync(preflightSeed.OperationId)).IsFailure,
            "SQL tenant mismatch is rejected before provider processing");
    }
    Check(operationProvider.Calls == callsBeforePreflight,
        "SQL tenant mismatch performs zero external provider calls");
    Check((await schema.BillingProviderOperations.AsNoTracking()
            .SingleAsync(o => o.Id == preflightSeed.OperationId)).Status
          == BillingProviderOperationStatuses.RequiresReconciliation,
        "SQL tenant mismatch is durably quarantined");
    await using (var reconciledDb = Db())
    {
        var processor = new BillingProviderOperationProcessor(reconciledDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        Check((await processor.ProcessAsync(preflightSeed.OperationId)).ErrorCode
              == "BILLING_RECONCILIATION_REQUIRED",
            "SQL reconciliation state is terminal for automatic processing");
    }
    Check(operationProvider.Calls == callsBeforePreflight,
        "SQL reconciliation retry performs zero external provider calls");

    var monotonicSeed = await SeedProviderOperation("MONOTONIC");
    await using (var prepareDb = Db())
    {
        await prepareDb.BillingProviderOperations.Where(o => o.Id == monotonicSeed.OperationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, BillingProviderOperationStatuses.Processing)
                .SetProperty(o => o.Attempts, 1)
                .SetProperty(o => o.LeaseId, "expired-monotonic-lease")
                .SetProperty(o => o.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
    }
    var quarantinePause = new PauseCommandOnceInterceptor("Billing_ProviderOperations");
    await using (var staleDb = Db(quarantinePause))
    {
        var staleProcessor = new BillingProviderOperationProcessor(staleDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        var staleProcess = staleProcessor.ProcessAsync(monotonicSeed.OperationId);
        await quarantinePause.Entered.WaitAsync(TimeSpan.FromSeconds(30));
        await using (var winnerDb = Db())
        {
            var confirmedAt = DateTime.UtcNow;
            await winnerDb.BillingProviderOperations.Where(o => o.Id == monotonicSeed.OperationId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, BillingProviderOperationStatuses.Completed)
                    .SetProperty(o => o.ProviderConfirmedAt, confirmedAt)
                    .SetProperty(o => o.CompletedAt, confirmedAt)
                    .SetProperty(o => o.LeaseId, (string?)null)
                    .SetProperty(o => o.LeaseExpiresAt, (DateTime?)null));
        }
        quarantinePause.Release();
        Check((await staleProcess).IsSuccess,
            "Stale SQL quarantine respects a concurrent terminal transition");
    }
    var monotonicStored = await schema.BillingProviderOperations.AsNoTracking()
        .SingleAsync(o => o.Id == monotonicSeed.OperationId);
    Check(monotonicStored.Status == BillingProviderOperationStatuses.Completed,
        "Stale SQL quarantine cannot overwrite COMPLETED");
    Check(monotonicStored.ProviderConfirmedAt is not null && monotonicStored.CompletedAt is not null,
        "Concurrent SQL provider acknowledgement remains durable after stale quarantine");

    var ackRaceSeed = await SeedProviderOperation("ACK-RACE");
    await using (var prepareAckDb = Db())
    {
        var prepared = await prepareAckDb.BillingProviderOperations.Where(o => o.Id == ackRaceSeed.OperationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, BillingProviderOperationStatuses.Processing)
                .SetProperty(o => o.Attempts, 1)
                .SetProperty(o => o.LeaseId, "expired-ack-race-lease")
                .SetProperty(o => o.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        Check(prepared == 1, "SQL ACK-only race starts from one expired unconfirmed PROCESSING row");
    }
    var ackPause = new PauseCommandOnceInterceptor("Billing_ProviderOperations");
    var callsBeforeAckRace = operationProvider.Calls;
    await using (var staleAckDb = Db(ackPause))
    {
        var staleProcessor = new BillingProviderOperationProcessor(staleAckDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        var staleAttempt = staleProcessor.ProcessAsync(ackRaceSeed.OperationId);
        await ackPause.Entered.WaitAsync(TimeSpan.FromSeconds(30));
        await using (var ackWinnerDb = Db())
        {
            await ackWinnerDb.BillingProviderOperations.Where(o => o.Id == ackRaceSeed.OperationId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.ProviderConfirmedAt, DateTime.UtcNow));
            var winnerState = await ackWinnerDb.BillingProviderOperations.AsNoTracking()
                .SingleAsync(o => o.Id == ackRaceSeed.OperationId);
            Check(winnerState.Status == BillingProviderOperationStatuses.Processing
                  && winnerState.ProviderConfirmedAt is not null,
                "Concurrent SQL winner persists ACK while operation remains PROCESSING");
        }
        ackPause.Release();
        var staleAckResult = await staleAttempt;
        Check(staleAckResult.ErrorCode == "BILLING_OPERATION_IN_PROGRESS",
            "Stale SQL quarantine observes a concurrent ACK without claiming reconciliation");
    }
    var ackRaceStored = await schema.BillingProviderOperations.AsNoTracking()
        .SingleAsync(o => o.Id == ackRaceSeed.OperationId);
    Check(ackRaceStored.Status == BillingProviderOperationStatuses.Processing
          && ackRaceStored.ProviderConfirmedAt is not null,
        "Stale SQL quarantine cannot overwrite an ACK that remains PROCESSING");
    await ExpireLease(ackRaceSeed.OperationId);
    await using (var ackRecoveryDb = Db())
    {
        var recovery = new BillingProviderOperationProcessor(ackRecoveryDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        Check((await recovery.ProcessAsync(ackRaceSeed.OperationId)).IsSuccess,
            "SQL operation with preserved ACK completes only its local phase");
    }
    Check(operationProvider.Calls == callsBeforeAckRace,
        "SQL ACK race and recovery never repeat the external provider call");
    Check((await schema.BillingProviderOperations.AsNoTracking()
            .SingleAsync(o => o.Id == ackRaceSeed.OperationId)).Status
          == BillingProviderOperationStatuses.Completed,
        "SQL ACK race recovery reaches terminal COMPLETED");

    var ackFailureSeed = await SeedProviderOperation("ACK-FAIL");
    var callsBeforeAckFailure = operationProvider.Calls;
    var ackFailure = new FailCommandOnceInterceptor("SET [b].[ProviderConfirmedAt]");
    await using (var ackFailureDb = Db(ackFailure))
    {
        var processor = new BillingProviderOperationProcessor(ackFailureDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        var failed = false;
        try { await processor.ProcessAsync(ackFailureSeed.OperationId); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed && ackFailure.Failures == 1, "Injected SQL failure interrupts durable provider ACK write");
    }
    var ackFailureStored = await schema.BillingProviderOperations.AsNoTracking()
        .SingleAsync(o => o.Id == ackFailureSeed.OperationId);
    Check(ackFailureStored.Status == BillingProviderOperationStatuses.Processing
          && ackFailureStored.ProviderConfirmedAt is null,
        "Failed ACK write leaves claimed operation without inventing confirmation");
    Check((await schema.BillingSubscriptions.AsNoTracking().SingleAsync(s => s.Id == ackFailureSeed.SubscriptionId)).Status
          == SubscriptionStatus.Active
          && (await schema.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.Id == ackFailureSeed.LicenseId)).EstadoCodigo == "ACTIVO",
        "ACK persistence failure leaves local subscription and license intact");
    await ExpireLease(ackFailureSeed.OperationId);
    await RecoverPending();
    Check((await schema.BillingProviderOperations.AsNoTracking().SingleAsync(o => o.Id == ackFailureSeed.OperationId)).Status
          == BillingProviderOperationStatuses.RequiresReconciliation,
        "Expired operation after ACK write failure is quarantined");
    Check(operationProvider.Calls == callsBeforeAckFailure + 1,
        "Provider is not called again after ambiguous ACK persistence failure");

    var localFailureSeed = await SeedProviderOperation("LOCAL-FAIL");
    var callsBeforeLocalFailure = operationProvider.Calls;
    var localFailure = new FailCommandOnceInterceptor("Billing_Subscriptions");
    await using (var localFailureDb = Db(localFailure))
    {
        var processor = new BillingProviderOperationProcessor(localFailureDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        var failed = false;
        try { await processor.ProcessAsync(localFailureSeed.OperationId); }
        catch (DbUpdateException) { failed = true; }
        Check(failed && localFailure.Failures == 1, "Injected SQL failure rolls back local apply after durable ACK");
    }
    var localFailureStored = await schema.BillingProviderOperations.AsNoTracking()
        .SingleAsync(o => o.Id == localFailureSeed.OperationId);
    Check(localFailureStored.Status == BillingProviderOperationStatuses.Processing
          && localFailureStored.ProviderConfirmedAt is not null,
        "Local transaction failure preserves the durable provider ACK for recovery");
    Check((await schema.BillingSubscriptions.AsNoTracking().SingleAsync(s => s.Id == localFailureSeed.SubscriptionId)).Status
          == SubscriptionStatus.Active
          && (await schema.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.Id == localFailureSeed.LicenseId)).EstadoCodigo == "ACTIVO",
        "Failed local transaction rolls back subscription and license together");
    await ExpireLease(localFailureSeed.OperationId);
    await RecoverPending();
    var localRecovered = await schema.BillingProviderOperations.AsNoTracking()
        .SingleAsync(o => o.Id == localFailureSeed.OperationId);
    Check(localRecovered.Status == BillingProviderOperationStatuses.Completed,
        "Expired operation with durable ACK completes its local phase after restart");
    Check(operationProvider.Calls == callsBeforeLocalFailure + 1,
        "Local recovery after durable ACK never repeats the provider call");
    Check((await schema.BillingSubscriptions.AsNoTracking().SingleAsync(s => s.Id == localFailureSeed.SubscriptionId)).Status
          == SubscriptionStatus.Canceled
          && (await schema.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.Id == localFailureSeed.LicenseId)).EstadoCodigo == "CANCELADO",
        "Recovered local phase atomically cancels subscription and license");

    var commitUser = await Tenant("COMMIT");
    int commitSubscriptionId;
    int commitLicenseId;
    await using (var commitSeedDb = Db())
    {
        var customer = new BillingCustomer
        {
            EmpresaId = commitUser.EmpresaId!.Value,
            Provider = "Mock",
            Email = "commit@example.invalid",
            ExternalCustomerId = "cus-commit",
        };
        var subscription = new BillingSubscription
        {
            Customer = customer,
            PlanId = basic.Id,
            Status = SubscriptionStatus.Active,
            ExternalSubscriptionId = "sub-commit",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-1),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(29),
        };
        var license = new EmpresaPlan
        {
            EmpresaId = commitUser.EmpresaId.Value,
            PlanId = basic.Id,
            FechaInicio = DateTime.UtcNow.AddDays(-1),
            FechaFin = DateTime.UtcNow.AddDays(29),
            EstadoCodigo = "ACTIVO",
        };
        commitSeedDb.AddRange(subscription, license);
        await commitSeedDb.SaveChangesAsync();
        commitSubscriptionId = subscription.Id;
        commitLicenseId = license.Id;
    }
    BillingProviderOperation? committedIntent = null;
    var recordingProcessor = new RecordingOperationProcessor(async (id, ct) =>
    {
        await using var observerDb = Db();
        committedIntent = await observerDb.BillingProviderOperations.AsNoTracking()
            .SingleAsync(o => o.Id == id, ct);
        return Result.Ok();
    });
    await using (var commitRequestDb = Db())
    {
        Check((await Service(commitRequestDb, commitUser, recordingProcessor)
                .CancelSubscriptionAsync(new(commitUser.EmpresaId!.Value, false))).IsSuccess,
            "SQL BillingService hands off a committed cancellation intent");
    }
    Check(committedIntent is not null
          && committedIntent.Status == BillingProviderOperationStatuses.Pending
          && committedIntent.BillingSubscriptionId == commitSubscriptionId
          && committedIntent.EmpresaPlanId == commitLicenseId
          && committedIntent.PlanId == basic.Id,
        "Independent SQL context observes tenant, subscription, plan and license before processing");
    Check((await schema.BillingSubscriptions.AsNoTracking()
              .SingleAsync(s => s.Id == commitSubscriptionId)).Status == SubscriptionStatus.Active
          && (await schema.EmpresaPlanes.AsNoTracking()
              .SingleAsync(p => p.Id == commitLicenseId)).EstadoCodigo == "ACTIVO",
        "SQL handoff alone leaves subscription and license unchanged");

    async Task ExpireLease(int id)
    {
        await using var expireDb = Db();
        await expireDb.BillingProviderOperations.Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
    }

    async Task RecoverPending()
    {
        await using var recoveryDb = Db();
        var recovery = new BillingProviderOperationProcessor(recoveryDb, operationResolver, email,
            operationOptions, Microsoft.Extensions.Logging.Abstractions.NullLogger<BillingProviderOperationProcessor>.Instance);
        await recovery.ProcessPendingAsync();
    }
    Console.WriteLine($"Completed: {checks}/{checks} checks. Real SQL/service, synthetic identities/providers/email only.");
    verificationCompleted = true;
}
finally
{
    // Validate exact server plus this run's GUID before any destructive operation.
    var actual = new SqlConnectionStringBuilder(schema.Database.GetConnectionString());
    if (actual.DataSource != server || actual.InitialCatalog != database || database != "BillingAudit_" + suffix
        || !Guid.TryParseExact(suffix, "N", out _))
        throw new InvalidOperationException("Refusing cleanup: unexpected server/database identity.");
    await schema.Database.EnsureDeletedAsync();
    Console.WriteLine("Deleted owned synthetic database: " + database);
    var evidencePath = Environment.GetEnvironmentVariable("NEOSTP_BILLING_SQL_EVIDENCE");
    if (verificationCompleted && !string.IsNullOrWhiteSpace(evidencePath))
    {
        var absoluteEvidencePath = Path.GetFullPath(evidencePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteEvidencePath)!);
        await File.WriteAllTextAsync(absoluteEvidencePath, JsonSerializer.Serialize(new
        {
            generatedAtUtc = DateTime.UtcNow,
            syntheticServer = server,
            syntheticDatabase = database,
            migrationsApplied = true,
            syntheticDatabaseDeleted = true,
            passed = checks,
            failed = 0,
            checks = passedChecks,
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Evidence written: " + absoluteEvidencePath);
    }
    // The parent owns the audit LocalDB process; this tool deliberately does not start or stop it.
}

sealed class PairLockGate : DbCommandInterceptor
{
    private int _arrivals;
    private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Arrivals => Volatile.Read(ref _arrivals);
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("sys.sp_getapplock", StringComparison.Ordinal))
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
            await _both.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        return result;
    }
}

sealed class BlockingPaymentProvider : IPaymentProvider
{
    public const string Name = "AuditBlockingProvider";
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;
    public string ProviderName => Name;
    public int Calls => Volatile.Read(ref _calls);
    public Task Entered => _entered.Task;
    public void Release() => _release.TrySetResult();

    public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Fail("Not used by SQL audit."));
    public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(string customerId, string externalPlanId,
        string successUrl, string cancelUrl, CancellationToken ct = default)
        => Task.FromResult(Result<CheckoutSessionResult>.Fail("Not used by SQL audit."));
    public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl,
        CancellationToken ct = default)
        => Task.FromResult(Result<BillingPortalResult>.Fail("Not used by SQL audit."));
    public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId,
        CancellationToken ct = default) => Task.FromResult(Result<string>.Fail("Not used by SQL audit."));
    public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd,
        CancellationToken ct = default)
        => CancelSubscriptionAsync(externalSubscriptionId, atPeriodEnd, "legacy", ct);
    public async Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd,
        string idempotencyKey, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _calls);
        _entered.TrySetResult();
        await _release.Task.WaitAsync(ct);
        return Result.Ok();
    }
}

sealed class RecordingOperationProcessor(
    Func<int, CancellationToken, Task<Result>> process) : IBillingProviderOperationProcessor
{
    public Task<Result> ProcessAsync(int operationId, CancellationToken ct = default)
        => process(operationId, ct);
    public Task<int> ProcessPendingAsync(CancellationToken ct = default) => Task.FromResult(0);
}

sealed class FailCommandOnceInterceptor(string marker) : DbCommandInterceptor
{
    private int _failures;
    public int Failures => Volatile.Read(ref _failures);

    private void FailIfMatched(DbCommand command)
    {
        if (command.CommandText.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains(marker, StringComparison.OrdinalIgnoreCase)
            && Interlocked.CompareExchange(ref _failures, 1, 0) == 0)
            throw new InvalidOperationException("Synthetic SQL command failure: " + marker);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FailIfMatched(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        FailIfMatched(command);
        return ValueTask.FromResult(result);
    }
}

sealed class PauseCommandOnceInterceptor(string marker) : DbCommandInterceptor
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _paused;
    public Task Entered => _entered.Task;
    public void Release() => _release.TrySetResult();

    private async Task PauseIfMatched(DbCommand command, CancellationToken cancellationToken)
    {
        if (command.CommandText.TrimStart().StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains(marker, StringComparison.OrdinalIgnoreCase)
            && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await PauseIfMatched(command, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await PauseIfMatched(command, cancellationToken);
        return result;
    }
}
