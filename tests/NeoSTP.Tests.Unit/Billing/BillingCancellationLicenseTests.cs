using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Billing;

public sealed class BillingCancellationLicenseTests
{
    private static async Task<(BillingSecurityFixture Fixture, DateTime OriginalEnd)> Trial()
    {
        var f = new BillingSecurityFixture();
        (await f.Service(BillingSecurityFixture.AdminA).StartTrialAsync(new(
            BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic, "audit@example.invalid"))).IsSuccess.Should().BeTrue();
        return (f, (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin!.Value);
    }

    [Fact]
    public async Task ImmediateCancellation_RevokesFiniteLicense_ButPreservesModuleRecords()
    {
        var (f, originalEnd) = await Trial();
        await using (f)
        {
            var addon = new Modulo { Id = 995101, Codigo = "AUDIT_ADDON", Nombre = "Synthetic addon" };
            f.Db.Modulos.Add(addon);
            f.Db.EmpresaModulos.Add(new EmpresaModulo { EmpresaId = BillingSecurityFixture.EmpresaA, Modulo = addon, Activo = true });
            await f.Db.SaveChangesAsync();
            var before = DateTime.UtcNow;
            (await f.Service(BillingSecurityFixture.AdminA).CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, false))).IsSuccess.Should().BeTrue();
            var after = DateTime.UtcNow;
            var license = await f.Db.EmpresaPlanes.AsNoTracking().SingleAsync();
            license.EstadoCodigo.Should().Be("CANCELADO");
            license.FechaFin.Should().BeOnOrAfter(before).And.BeOnOrBefore(after).And.BeBefore(originalEnd);
            (await f.Db.EmpresaModulos.AsNoTracking().SingleAsync()).Activo.Should().BeTrue();
            (await f.Db.BillingSubscriptions.AsNoTracking().SingleAsync()).CancelAtPeriodEnd.Should().BeFalse();
            (await f.Service(BillingSecurityFixture.AdminA).GetActiveSubscriptionAsync(BillingSecurityFixture.EmpresaA)).Value.Should().BeNull();
        }
    }

    [Fact]
    public async Task ScheduledCancellation_PreservesExactOriginalEnd_AndRemainsVisibleUntilThen()
    {
        var (f, originalEnd) = await Trial();
        await using (f)
        {
            var result = await f.Service(BillingSecurityFixture.AdminA).CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, true));
            result.IsSuccess.Should().BeTrue();
            var license = await f.Db.EmpresaPlanes.AsNoTracking().SingleAsync();
            license.EstadoCodigo.Should().Be("ACTIVO");
            license.FechaFin.Should().Be(originalEnd);
            var sub = await f.Db.BillingSubscriptions.AsNoTracking().SingleAsync();
            sub.Status.Should().Be(SubscriptionStatus.Canceled);
            sub.CancelAtPeriodEnd.Should().BeTrue();
            (await f.Service(BillingSecurityFixture.AdminA).GetActiveSubscriptionAsync(BillingSecurityFixture.EmpresaA)).Value!.CancelAtPeriodEnd.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RepeatingScheduledCancellation_DoesNotExtendEnd_OrSendDuplicateEmail()
    {
        var (f, originalEnd) = await Trial();
        await using (f)
        {
            f.Email.ClearReceivedCalls();
            var service = f.Service(BillingSecurityFixture.AdminA);
            (await service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, true))).IsSuccess.Should().BeTrue();
            var canceledAt = (await f.Db.BillingSubscriptions.AsNoTracking().SingleAsync()).CanceledAt;
            (await service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, true))).IsSuccess.Should().BeTrue();
            var sub = await f.Db.BillingSubscriptions.AsNoTracking().SingleAsync();
            sub.CanceledAt.Should().Be(canceledAt);
            (await f.Db.EmpresaPlanes.AsNoTracking().SingleAsync()).FechaFin.Should().Be(originalEnd);
            await f.Email.Received(1).EnviarAsync(Arg.Any<NeoSTP.Application.Dte.Abstractions.EmailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task ScheduledCancellation_CanBeChangedToImmediate_ButImmediateCannotBeRevived()
    {
        var (f, originalEnd) = await Trial();
        await using (f)
        {
            var service = f.Service(BillingSecurityFixture.AdminA);
            (await service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, true))).IsSuccess.Should().BeTrue();
            (await service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, false))).IsSuccess.Should().BeTrue();
            var immediateEnd = (await f.Db.EmpresaPlanes.AsNoTracking().SingleAsync()).FechaFin!.Value;
            immediateEnd.Should().BeBefore(originalEnd);
            (await service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, true))).IsSuccess.Should().BeTrue();
            var license = await f.Db.EmpresaPlanes.AsNoTracking().SingleAsync();
            license.EstadoCodigo.Should().Be("CANCELADO");
            license.FechaFin.Should().Be(immediateEnd);
            (await f.Db.BillingSubscriptions.AsNoTracking().SingleAsync()).CancelAtPeriodEnd.Should().BeFalse();
        }
    }

    [Fact]
    public async Task RequestedEndOfPeriod_WithoutAValidPeriod_CancelsAccessImmediatelyInsteadOfCreatingTime()
    {
        await using var f = new BillingSecurityFixture();
        var payment = await f.Pending(BillingSecurityFixture.EmpresaA);
        payment.Status = "FAILED";
        payment.Subscription.Status = SubscriptionStatus.Active;
        payment.Subscription.CurrentPeriodEnd = null;
        f.Db.EmpresaPlanes.Add(new EmpresaPlan { EmpresaId = BillingSecurityFixture.EmpresaA,
            PlanId = BillingSecurityFixture.Basic, FechaInicio = DateTime.UtcNow.AddDays(-1), FechaFin = null, EstadoCodigo = "ACTIVO" });
        await f.Db.SaveChangesAsync();
        (await f.Service(BillingSecurityFixture.AdminA).CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, true))).IsSuccess.Should().BeTrue();
        var license = await f.Db.EmpresaPlanes.AsNoTracking().SingleAsync();
        license.EstadoCodigo.Should().Be("CANCELADO");
        license.FechaFin.Should().NotBeNull().And.BeOnOrBefore(DateTime.UtcNow);
        (await f.Db.BillingSubscriptions.AsNoTracking().SingleAsync()).CancelAtPeriodEnd.Should().BeFalse();
    }

    [Theory]
    [InlineData("multiple")]
    [InlineData("different")]
    public async Task AmbiguousLicenseAssociation_FailsBeforeProviderOrLicenseMutation(string scenario)
    {
        var (f, originalEnd) = await Trial();
        await using (f)
        {
            if (scenario == "multiple") f.Db.EmpresaPlanes.Add(new EmpresaPlan { EmpresaId = BillingSecurityFixture.EmpresaA,
                PlanId = BillingSecurityFixture.Basic, FechaInicio = DateTime.UtcNow, FechaFin = originalEnd, EstadoCodigo = "ACTIVO" });
            else (await f.Db.EmpresaPlanes.SingleAsync()).PlanId = BillingSecurityFixture.Pro;
            await f.Db.SaveChangesAsync();
            var result = await f.Service(BillingSecurityFixture.AdminA).CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, false));
            result.ErrorCode.Should().Be("BILLING_LICENSE_AMBIGUOUS");
            (await f.Db.BillingSubscriptions.SingleAsync()).Status.Should().Be(SubscriptionStatus.Trialing);
            (await f.Db.EmpresaPlanes.CountAsync(p => p.EstadoCodigo == "ACTIVO")).Should().Be(scenario == "multiple" ? 2 : 1);
        }
    }

    [Fact]
    public async Task ProviderFailure_DoesNotCancelLocalSubscriptionOrLicense()
    {
        var (f, originalEnd) = await Trial();
        await using (f)
        {
            var sub = await f.Db.BillingSubscriptions.Include(s => s.Customer).SingleAsync();
            sub.ExternalSubscriptionId = "synthetic-external";
            await f.Db.SaveChangesAsync();
            var provider = Substitute.For<IPaymentProvider>();
            provider.ProviderName.Returns(sub.Customer.Provider);
            provider.CancelSubscriptionAsync(sub.ExternalSubscriptionId, false, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result.Fail("synthetic provider rejection", "SYNTHETIC_PROVIDER_REJECTED"));
            var resolver = Substitute.For<IPaymentProviderResolver>();
            resolver.Resolve(Arg.Any<string?>()).Returns(provider);
            var service = new NeoSTP.Infrastructure.Billing.BillingService(f.Db, resolver, f.Email, f.Options,
                f.Identity(BillingSecurityFixture.AdminA));
            (await service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, false))).ErrorCode.Should().Be("SYNTHETIC_PROVIDER_REJECTED");
            (await f.Db.BillingSubscriptions.AsNoTracking().SingleAsync()).Status.Should().Be(SubscriptionStatus.Trialing);
            var license = await f.Db.EmpresaPlanes.AsNoTracking().SingleAsync();
            license.EstadoCodigo.Should().Be("ACTIVO");
            license.FechaFin.Should().Be(originalEnd);
            await provider.Received(1).CancelSubscriptionAsync(
                "synthetic-external", false, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task RevokedLicense_IsObservedByRealResolverAndDteLimitGuard()
    {
        var (f, _) = await Trial();
        await using (f)
        {
            var module = new Modulo { Id = 995102, Codigo = "NEODTE", Nombre = "Synthetic DTE" };
            f.Db.Modulos.Add(module);
            f.Db.EmpresaModulos.Add(new EmpresaModulo { EmpresaId = BillingSecurityFixture.EmpresaA, Modulo = module, Activo = true });
            await f.Db.SaveChangesAsync();
            (await f.Service(BillingSecurityFixture.AdminA).CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, false))).IsSuccess.Should().BeTrue();
            var resolver = new EmpresasService(f.Db, Substitute.For<IAuditoriaService>());
            var license = await resolver.ResolveAsync(BillingSecurityFixture.EmpresaA);
            license!.Vigente.Should().BeFalse();
            license.PlanId.Should().BeNull();
            license.Modulos.Should().ContainSingle(m => m.Codigo == "NEODTE" && !m.Activo);
            (await f.Db.EmpresaModulos.AsNoTracking().SingleAsync(m => m.EmpresaId == BillingSecurityFixture.EmpresaA && m.ModuloId == module.Id))
                .Activo.Should().BeTrue("license revocation removes effective access without destroying the module assignment");
            var guard = new LicenciaGuardService(f.Db);
            (await guard.ValidarLimiteAsync(BillingSecurityFixture.EmpresaA, RecursoLimitado.DteMensual)).ErrorCode.Should().Be("LICENSE_INVALID");
        }
    }
}
