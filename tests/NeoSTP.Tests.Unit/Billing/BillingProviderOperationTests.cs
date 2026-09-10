using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

namespace NeoSTP.Tests.Unit.Billing;

public sealed class BillingProviderOperationTests
{
    [Fact]
    public async Task CancellationIntentIsCommittedBeforeProcessorOrProviderRuns()
    {
        await using var fixture = new Fixture();
        BillingProviderOperation? observed = null;
        var processor = new RecordingProcessor(async (id, ct) =>
        {
            await using var observer = fixture.NewDb();
            observed = await observer.BillingProviderOperations.AsNoTracking().SingleAsync(o => o.Id == id, ct);
            return Result.Ok();
        });

        var result = await fixture.Service(processor).CancelSubscriptionAsync(new(Fixture.EmpresaId, false));

        result.IsSuccess.Should().BeTrue(result.Error);
        observed.Should().NotBeNull();
        observed!.Status.Should().Be(BillingProviderOperationStatuses.Pending);
        observed.Attempts.Should().Be(0);
        fixture.Provider.Calls.Should().BeEmpty("the committed intent must exist before any external effect");
        (await fixture.Subscription()).Status.Should().Be(SubscriptionStatus.Active);
        (await fixture.License()).EstadoCodigo.Should().Be("ACTIVO");
    }

    [Fact]
    public async Task RepeatingTheSamePendingCancellationReusesOneOperation()
    {
        await using var fixture = new Fixture();
        var processor = new RecordingProcessor((_, _) => Task.FromResult(
            Result.Fail("synthetic deferred processing", "BILLING_OPERATION_NOT_DUE")));
        var service = fixture.Service(processor);

        (await service.CancelSubscriptionAsync(new(Fixture.EmpresaId, false))).IsFailure.Should().BeTrue();
        (await service.CancelSubscriptionAsync(new(Fixture.EmpresaId, false))).IsFailure.Should().BeTrue();

        var operations = await fixture.Db.BillingProviderOperations.AsNoTracking().ToListAsync();
        operations.Should().ContainSingle();
        processor.OperationIds.Should().Equal(operations[0].Id, operations[0].Id);
        fixture.Provider.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ProviderConfirmationCompletesOperationAndAppliesSubscriptionAndLicense()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service().CancelSubscriptionAsync(new(Fixture.EmpresaId, false));

        result.IsSuccess.Should().BeTrue(result.Error);
        var operation = await fixture.Db.BillingProviderOperations.AsNoTracking().SingleAsync();
        operation.Status.Should().Be(BillingProviderOperationStatuses.Completed);
        operation.ProviderConfirmedAt.Should().NotBeNull();
        operation.CompletedAt.Should().NotBeNull();
        operation.LeaseId.Should().BeNull();
        fixture.Provider.Calls.Should().ContainSingle(c => !c.AtPeriodEnd && c.IdempotencyKey == operation.IdempotencyKey);
        (await fixture.Subscription()).Status.Should().Be(SubscriptionStatus.Canceled);
        (await fixture.License()).EstadoCodigo.Should().Be("CANCELADO");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnconfirmedProviderFailureRequiresReconciliationAndIsNeverResent(bool throwTimeout)
    {
        await using var fixture = new Fixture();
        fixture.Provider.CancelBehavior = throwTimeout
            ? _ => Task.FromException<Result>(new TimeoutException("synthetic ambiguous timeout"))
            : _ => Task.FromResult(Result.Fail("synthetic provider rejection", "PROVIDER_REJECTED"));
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Pending);
        var processor = fixture.Processor();

        var first = await processor.ProcessAsync(operation.Id);
        var second = await processor.ProcessAsync(operation.Id);

        first.IsFailure.Should().BeTrue();
        second.IsFailure.Should().BeTrue();
        fixture.Provider.Calls.Should().ContainSingle("an unconfirmed provider outcome must never be resent blindly");
        var stored = await fixture.Operation(operation.Id);
        stored.Status.Should().Be(BillingProviderOperationStatuses.RequiresReconciliation);
        stored.ProviderConfirmedAt.Should().BeNull();
        stored.NextAttemptAt.Should().BeNull();
        stored.LeaseId.Should().BeNull();
        stored.LeaseExpiresAt.Should().BeNull();
        (await fixture.Subscription()).Status.Should().Be(SubscriptionStatus.Active);
        (await fixture.License()).EstadoCodigo.Should().Be("ACTIVO");
        (await fixture.License()).FechaFin.Should().Be(fixture.OriginalEnd);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("resource")]
    [InlineData("license")]
    [InlineData("tenant")]
    [InlineData("plan")]
    public async Task ChangedSnapshotIsQuarantinedBeforeProviderAndDoesNotRevokeLicense(
        string changedSnapshot)
    {
        await using var fixture = new Fixture();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Pending);
        await fixture.BreakLocalSnapshot(changedSnapshot);
        var processor = fixture.Processor();

        var first = await processor.ProcessAsync(operation.Id);
        var second = await processor.ProcessAsync(operation.Id);

        first.IsFailure.Should().BeTrue();
        second.IsFailure.Should().BeTrue();
        fixture.Provider.Calls.Should().BeEmpty("correlation must be validated before any external effect");
        var stored = await fixture.Operation(operation.Id);
        stored.Status.Should().Be(BillingProviderOperationStatuses.RequiresReconciliation);
        stored.ProviderConfirmedAt.Should().BeNull();
        stored.LeaseId.Should().BeNull();
        stored.LeaseExpiresAt.Should().BeNull();
        (await fixture.Subscription()).Status.Should().Be(SubscriptionStatus.Active);
        (await fixture.License()).EstadoCodigo.Should().Be("ACTIVO");
        (await fixture.License()).FechaFin.Should().Be(fixture.OriginalEnd);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("resource")]
    [InlineData("license")]
    [InlineData("tenant")]
    [InlineData("plan")]
    public async Task ConfirmedProviderWithChangedSnapshotQuarantinesLocalRecoveryWithoutProviderReplay(
        string changedSnapshot)
    {
        await using var fixture = new Fixture();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Processing,
            providerConfirmedAt: DateTime.UtcNow.AddMinutes(-2), leaseExpiresAt: DateTime.UtcNow.AddMinutes(-1));
        await fixture.BreakLocalSnapshot(changedSnapshot);

        var result = await fixture.Processor().ProcessAsync(operation.Id);

        result.IsFailure.Should().BeTrue();
        fixture.Provider.Calls.Should().BeEmpty("a durable acknowledgement must never replay the provider call");
        var stored = await fixture.Operation(operation.Id);
        stored.Status.Should().Be(BillingProviderOperationStatuses.RequiresReconciliation);
        stored.ProviderConfirmedAt.Should().NotBeNull();
        (await fixture.Subscription()).Status.Should().Be(SubscriptionStatus.Active);
        (await fixture.License()).EstadoCodigo.Should().Be("ACTIVO");
    }

    [Fact]
    public async Task LicenseCreatedAfterIntentIsQuarantinedBeforeProvider()
    {
        await using var fixture = new Fixture();
        await fixture.RemoveLicense();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Pending, empresaPlanId: null);
        await fixture.CreateLicense();

        var result = await fixture.Processor().ProcessAsync(operation.Id);

        result.IsFailure.Should().BeTrue();
        fixture.Provider.Calls.Should().BeEmpty();
        (await fixture.Operation(operation.Id)).Status.Should()
            .Be(BillingProviderOperationStatuses.RequiresReconciliation);
        (await fixture.License()).EstadoCodigo.Should().Be("ACTIVO");
    }

    [Fact]
    public async Task OpenCancellationFreezesOtherBillingMutations()
    {
        await using var fixture = new Fixture();
        await fixture.Operation(BillingProviderOperationStatuses.Pending);

        var result = await fixture.Service().ChangePlanAsync(new(Fixture.EmpresaId, 123456));

        result.ErrorCode.Should().Be("BILLING_CANCELLATION_PENDING");
        fixture.Provider.Calls.Should().BeEmpty();
        (await fixture.Subscription()).PlanId.Should().Be(Fixture.SyntheticPlanId);
    }

    [Fact]
    public async Task OpenCancellationBlocksCheckoutAndPortalBeforeProvider()
    {
        await using var fixture = new Fixture();
        await fixture.Operation(BillingProviderOperationStatuses.Pending);
        var service = fixture.Service();

        var checkout = await service.CreateCheckoutSessionAsync(
            new(Fixture.EmpresaId, Fixture.SyntheticPlanId, "/billing"));
        var portal = await service.GetPortalUrlAsync(Fixture.EmpresaId);

        checkout.ErrorCode.Should().Be("BILLING_CANCELLATION_PENDING");
        portal.ErrorCode.Should().Be("BILLING_CANCELLATION_PENDING");
        fixture.Provider.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task OppositeCancellationModeCannotOpenWhileFirstOperationIsPending()
    {
        await using var fixture = new Fixture();
        var pending = await fixture.Operation(BillingProviderOperationStatuses.Pending);
        var tracked = await fixture.Db.BillingProviderOperations.SingleAsync(o => o.Id == pending.Id);
        tracked.CancelAtPeriodEnd = true;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var result = await fixture.Service().CancelSubscriptionAsync(new(Fixture.EmpresaId, false));

        result.ErrorCode.Should().Be("BILLING_CANCELLATION_PENDING");
        (await fixture.Db.BillingProviderOperations.AsNoTracking().CountAsync()).Should().Be(1);
        fixture.Provider.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ProviderConfirmedExpiredLeaseFinishesLocallyWithoutRepeatingProvider()
    {
        await using var fixture = new Fixture();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Processing,
            providerConfirmedAt: DateTime.UtcNow.AddMinutes(-2), leaseExpiresAt: DateTime.UtcNow.AddMinutes(-1));

        var result = await fixture.Processor().ProcessAsync(operation.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        fixture.Provider.Calls.Should().BeEmpty();
        (await fixture.Operation(operation.Id)).Status.Should().Be(BillingProviderOperationStatuses.Completed);
        (await fixture.Subscription()).Status.Should().Be(SubscriptionStatus.Canceled);
        (await fixture.License()).EstadoCodigo.Should().Be("CANCELADO");
    }

    [Fact]
    public async Task LiveLeaseCannotBeClaimedByAnotherProcessor()
    {
        await using var fixture = new Fixture();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Processing,
            leaseExpiresAt: DateTime.UtcNow.AddMinutes(2));

        var result = await fixture.Processor().ProcessAsync(operation.Id);

        result.ErrorCode.Should().Be("BILLING_OPERATION_IN_PROGRESS");
        fixture.Provider.Calls.Should().BeEmpty();
        (await fixture.Operation(operation.Id)).Status.Should().Be(BillingProviderOperationStatuses.Processing);
    }

    [Fact]
    public async Task ProcessingWithoutLeaseIsQuarantinedInsteadOfRemainingStuck()
    {
        await using var fixture = new Fixture();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Processing);

        (await fixture.Processor().ProcessPendingAsync()).Should().Be(1);

        fixture.Provider.Calls.Should().BeEmpty();
        var stored = await fixture.Operation(operation.Id);
        stored.Status.Should().Be(BillingProviderOperationStatuses.RequiresReconciliation);
        stored.LeaseId.Should().BeNull();
        stored.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessingWithoutLeaseAndWithProviderAckRecoversOnlyLocalPhase()
    {
        await using var fixture = new Fixture();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Processing,
            providerConfirmedAt: DateTime.UtcNow.AddMinutes(-1));

        (await fixture.Processor().ProcessPendingAsync()).Should().Be(1);

        fixture.Provider.Calls.Should().BeEmpty("the provider acknowledgement is already durable");
        var stored = await fixture.Operation(operation.Id);
        stored.Status.Should().Be(BillingProviderOperationStatuses.Completed);
        (await fixture.Subscription()).Status.Should().Be(SubscriptionStatus.Canceled);
        (await fixture.License()).EstadoCodigo.Should().Be("CANCELADO");
    }

    [Fact]
    public async Task ExpiredLeaseWithoutProviderAckIsQuarantinedAndNeverResent()
    {
        await using var fixture = new Fixture();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Processing,
            leaseExpiresAt: DateTime.UtcNow.AddMinutes(-1));

        (await fixture.Processor().ProcessPendingAsync()).Should().Be(1);

        fixture.Provider.Calls.Should().BeEmpty("an expired lease has an ambiguous external outcome");
        var stored = await fixture.Operation(operation.Id);
        stored.Status.Should().Be(BillingProviderOperationStatuses.RequiresReconciliation);
        stored.LeaseId.Should().BeNull();
        stored.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessingTheSameOperationTwiceIsIdempotent()
    {
        await using var fixture = new Fixture();
        var operation = await fixture.Operation(BillingProviderOperationStatuses.Pending);
        var processor = fixture.Processor();

        (await processor.ProcessAsync(operation.Id)).IsSuccess.Should().BeTrue();
        (await processor.ProcessAsync(operation.Id)).IsSuccess.Should().BeTrue();

        fixture.Provider.Calls.Should().ContainSingle();
        (await fixture.Operation(operation.Id)).Status.Should().Be(BillingProviderOperationStatuses.Completed);
        await fixture.Email.Received(1).EnviarAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduledThenImmediateCreatesSecondOperationButImmediateThenScheduledNeverRevives()
    {
        await using var fixture = new Fixture();
        var service = fixture.Service();

        (await service.CancelSubscriptionAsync(new(Fixture.EmpresaId, true))).IsSuccess.Should().BeTrue();
        var scheduledEnd = (await fixture.License()).FechaFin;
        (await service.CancelSubscriptionAsync(new(Fixture.EmpresaId, false))).IsSuccess.Should().BeTrue();
        var immediateEnd = (await fixture.License()).FechaFin;
        (await service.CancelSubscriptionAsync(new(Fixture.EmpresaId, true))).IsSuccess.Should().BeTrue();

        var operations = await fixture.Db.BillingProviderOperations.AsNoTracking().OrderBy(o => o.Id).ToListAsync();
        operations.Should().HaveCount(2);
        operations.Select(o => o.CancelAtPeriodEnd).Should().Equal(true, false);
        operations.Select(o => o.Status).Should().OnlyContain(s => s == BillingProviderOperationStatuses.Completed);
        fixture.Provider.Calls.Select(c => c.AtPeriodEnd).Should().Equal(true, false);
        scheduledEnd.Should().Be(fixture.OriginalEnd);
        immediateEnd.Should().BeBefore(scheduledEnd!.Value);
        (await fixture.License()).FechaFin.Should().Be(immediateEnd);
        (await fixture.License()).EstadoCodigo.Should().Be("CANCELADO");
        (await fixture.Subscription()).CancelAtPeriodEnd.Should().BeFalse();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public const int EmpresaId = 997001;
        public const int SyntheticPlanId = 997002;
        private const int UserId = 997003;
        private const int SubscriptionId = 997004;
        private const int LicenseId = 997005;

        private readonly InMemoryDatabaseRoot _root = new();
        private readonly string _databaseName = "billing-provider-operation-" + Guid.NewGuid().ToString("N");
        public NeoStpDbContext Db { get; }
        public FakeProvider Provider { get; } = new();
        public IPaymentProviderResolver Resolver { get; }
        public IEmailSender Email { get; } = Substitute.For<IEmailSender>();
        public IOptions<BillingOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(new BillingOptions
        {
            Provider = FakeProvider.Name,
            ProviderOperations = new BillingProviderOperationsOptions { BatchSize = 20, LeaseSeconds = 30 },
        });
        public DateTime OriginalEnd { get; } = DateTime.UtcNow.AddDays(30);

        public Fixture()
        {
            Db = NewDb();
            Resolver = Substitute.For<IPaymentProviderResolver>();
            Resolver.DefaultProvider.Returns(FakeProvider.Name);
            Resolver.Disponibles.Returns(new[] { FakeProvider.Name });
            Resolver.Resolve(Arg.Any<string?>()).Returns(Provider);
            Email.EnviarAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new EmailSendResult { Success = true }));

            var company = new Empresa { Id = EmpresaId, Nit = "SYNTHETIC-OP", RazonSocial = "Synthetic operation tenant", EstadoCodigo = "ACTIVA" };
            var plan = new Plan { Id = SyntheticPlanId, Codigo = "SYNTHETIC_OP", Nombre = "Synthetic plan", PrecioMensual = 10 };
            var customer = new BillingCustomer { Id = 997006, Empresa = company, EmpresaId = EmpresaId,
                Provider = FakeProvider.Name, Email = "operation@example.invalid" };
            var subscription = new BillingSubscription { Id = SubscriptionId, Customer = customer,
                BillingCustomerId = customer.Id, Plan = plan, PlanId = SyntheticPlanId, Status = SubscriptionStatus.Active,
                ExternalSubscriptionId = "sub_synthetic_operation", CurrentPeriodStart = DateTime.UtcNow.AddDays(-1),
                CurrentPeriodEnd = OriginalEnd };
            var license = new EmpresaPlan { Id = LicenseId, Empresa = company, EmpresaId = EmpresaId,
                Plan = plan, PlanId = SyntheticPlanId, FechaInicio = DateTime.UtcNow.AddDays(-1), FechaFin = OriginalEnd,
                EstadoCodigo = "ACTIVO" };
            var user = new Usuario { Id = UserId, Empresa = company, EmpresaId = EmpresaId,
                Username = "synthetic-op-admin", Email = "admin@example.invalid", NombreCompleto = "Synthetic admin",
                PasswordHash = "unused-synthetic", TipoUsuarioCodigo = "ADMIN", EstadoCodigo = "ACTIVO" };
            Db.AddRange(company, plan, customer, subscription, license, user);
            Db.SaveChanges();
        }

        public NeoStpDbContext NewDb() => new(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase(_databaseName, _root).Options);

        public BillingProviderOperationProcessor Processor() => new(Db, Resolver, Email, Options,
            NullLogger<BillingProviderOperationProcessor>.Instance);

        public BillingService Service(IBillingProviderOperationProcessor? processor = null) => new(
            Db, Resolver, Email, Options,
            BillingSecurityFixture.CurrentUser(UserId, "ADMIN", EmpresaId), processor ?? Processor());

        public Task<BillingSubscription> Subscription() => Db.BillingSubscriptions.AsNoTracking()
            .SingleAsync(s => s.Id == SubscriptionId);

        public Task<EmpresaPlan> License() => Db.EmpresaPlanes.AsNoTracking().SingleAsync(p => p.Id == LicenseId);

        public async Task RemoveLicense()
        {
            Db.EmpresaPlanes.Remove(await Db.EmpresaPlanes.SingleAsync(p => p.Id == LicenseId));
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }

        public async Task CreateLicense()
        {
            Db.EmpresaPlanes.Add(new EmpresaPlan
            {
                Id = LicenseId,
                EmpresaId = EmpresaId,
                PlanId = SyntheticPlanId,
                FechaInicio = DateTime.UtcNow,
                FechaFin = OriginalEnd,
                EstadoCodigo = "ACTIVO",
            });
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }

        public async Task BreakLocalSnapshot(string dimension)
        {
            switch (dimension)
            {
                case "provider":
                    (await Db.BillingCustomers.SingleAsync(c => c.EmpresaId == EmpresaId)).Provider = "ChangedProvider";
                    break;
                case "resource":
                    (await Db.BillingSubscriptions.SingleAsync(s => s.Id == SubscriptionId)).ExternalSubscriptionId =
                        "sub_changed_after_intent";
                    break;
                case "license":
                    (await Db.EmpresaPlanes.SingleAsync(p => p.Id == LicenseId)).PlanId++;
                    break;
                case "plan":
                    (await Db.BillingSubscriptions.SingleAsync(s => s.Id == SubscriptionId)).PlanId++;
                    break;
                case "tenant":
                    (await Db.BillingProviderOperations.SingleAsync()).EmpresaId++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dimension), dimension, null);
            }

            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }

        public async Task<BillingProviderOperation> Operation(
            string status,
            DateTime? providerConfirmedAt = null,
            DateTime? leaseExpiresAt = null,
            int? empresaPlanId = LicenseId)
        {
            var operation = new BillingProviderOperation
            {
                EmpresaId = EmpresaId,
                BillingSubscriptionId = SubscriptionId,
                EmpresaPlanId = empresaPlanId,
                PlanId = SyntheticPlanId,
                Provider = FakeProvider.Name,
                OperationType = BillingProviderOperationTypes.CancelSubscription,
                IdempotencyKey = "synthetic-operation-" + Guid.NewGuid().ToString("N"),
                ExternalResourceId = "sub_synthetic_operation",
                RequestedAt = DateTime.UtcNow.AddMinutes(-3),
                AccessEndsAt = null,
                Status = status,
                Attempts = status == BillingProviderOperationStatuses.Pending ? 0 : 1,
                NextAttemptAt = DateTime.UtcNow.AddMinutes(-2),
                LeaseId = status == BillingProviderOperationStatuses.Processing ? "expired-or-live-lease" : null,
                LeaseExpiresAt = leaseExpiresAt,
                ProviderConfirmedAt = providerConfirmedAt,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3),
                CreatedBy = UserId.ToString(),
            };
            Db.BillingProviderOperations.Add(operation);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return operation;
        }

        public Task<BillingProviderOperation> Operation(int id) => Db.BillingProviderOperations.AsNoTracking()
            .SingleAsync(o => o.Id == id);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class RecordingProcessor(
        Func<int, CancellationToken, Task<Result>> process) : IBillingProviderOperationProcessor
    {
        public List<int> OperationIds { get; } = [];
        public async Task<Result> ProcessAsync(int operationId, CancellationToken ct = default)
        {
            OperationIds.Add(operationId);
            return await process(operationId, ct);
        }
        public Task<int> ProcessPendingAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed record CancelCall(string ExternalId, bool AtPeriodEnd, string IdempotencyKey);

    private sealed class FakeProvider : IPaymentProvider
    {
        public const string Name = "SyntheticProvider";
        public List<CancelCall> Calls { get; } = [];
        public Func<CancelCall, Task<Result>> CancelBehavior { get; set; } =
            _ => Task.FromResult(Result.Ok());
        public string ProviderName => Name;
        public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
            => Task.FromResult(Result<string>.Ok("customer-synthetic"));
        public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(string customerId, string externalPlanId,
            string successUrl, string cancelUrl, CancellationToken ct = default)
            => Task.FromResult(Result<CheckoutSessionResult>.Fail("unused"));
        public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl,
            CancellationToken ct = default) => Task.FromResult(Result<BillingPortalResult>.Fail("unused"));
        public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId,
            CancellationToken ct = default) => Task.FromResult(Result<string>.Fail("unused"));
        public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd,
            CancellationToken ct = default)
            => CancelSubscriptionAsync(externalSubscriptionId, atPeriodEnd, "legacy-without-key", ct);
        public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd,
            string idempotencyKey, CancellationToken ct = default)
        {
            var call = new CancelCall(externalSubscriptionId, atPeriodEnd, idempotencyKey);
            Calls.Add(call);
            return CancelBehavior(call);
        }
    }
}
