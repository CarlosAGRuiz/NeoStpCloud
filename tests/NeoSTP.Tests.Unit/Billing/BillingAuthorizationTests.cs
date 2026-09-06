using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Billing;

namespace NeoSTP.Tests.Unit.Billing;

public sealed class BillingAuthorizationTests
{
    [Theory]
    [InlineData(BillingSecurityFixture.AdminA)]
    [InlineData(BillingSecurityFixture.AdminB)]
    [InlineData(BillingSecurityFixture.OperatorA)]
    public async Task TenantUser_CannotReviewOrApproveAnySubscriptionTransfer(int actorId)
    {
        await using var f = new BillingSecurityFixture();
        var a = await f.Pending(BillingSecurityFixture.EmpresaA);
        var b = await f.Pending(BillingSecurityFixture.EmpresaB);
        var service = f.Service(actorId);
        (await service.GetTransferenciasPendientesAsync(null)).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.GetTransferenciasPendientesAsync(BillingSecurityFixture.EmpresaA)).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        foreach (var payment in new[] { a, b })
        {
            (await service.ConfirmarTransferenciaAsync(payment.Id, "superadmin")).ErrorCode.Should().Be("BILLING_FORBIDDEN");
            (await service.RechazarTransferenciaAsync(payment.Id, "synthetic", "superadmin")).ErrorCode.Should().Be("BILLING_FORBIDDEN");
            payment.Status.Should().Be("PENDIENTE_VERIFICACION");
            payment.VerificadoPor.Should().BeNull();
        }
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForgedPlatformRole_IsRejectedEvenWhenClaimsOmitEmpresa(bool includeEmpresa)
    {
        await using var f = new BillingSecurityFixture();
        var payment = await f.Pending(BillingSecurityFixture.EmpresaB);
        var forged = BillingSecurityFixture.CurrentUser(BillingSecurityFixture.AdminA, "SUPERADMIN",
            includeEmpresa ? BillingSecurityFixture.EmpresaA : null);
        var service = new BillingService(f.Db, f.Payments, f.Email, f.Options, forged);
        (await service.ConfirmarTransferenciaAsync(payment.Id, "platform-owner")).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        payment.Status.Should().Be("PENDIENTE_VERIFICACION");
    }

    [Fact]
    public async Task CentralAdministrator_VerifiesBothTenants_AndUsesPersistedActor()
    {
        await using var f = new BillingSecurityFixture();
        var a = await f.Pending(BillingSecurityFixture.EmpresaA);
        var b = await f.Pending(BillingSecurityFixture.EmpresaB);
        var service = f.Service(BillingSecurityFixture.Central);
        (await service.GetTransferenciasPendientesAsync(null)).Value.Should().HaveCount(2);
        (await service.ConfirmarTransferenciaAsync(a.Id, "forged-caller-name")).IsSuccess.Should().BeTrue();
        (await service.RechazarTransferenciaAsync(b.Id, "synthetic", "forged-caller-name")).IsSuccess.Should().BeTrue();
        a.Status.Should().Be("SUCCEEDED");
        b.Status.Should().Be("FAILED");
        a.VerificadoPor.Should().Be("audit-user-" + BillingSecurityFixture.Central);
        b.VerificadoPor.Should().Be(a.VerificadoPor);
        var license = await f.Db.EmpresaPlanes.SingleAsync();
        license.EmpresaId.Should().Be(BillingSecurityFixture.EmpresaA);
        license.FechaFin.Should().NotBeNull();
        (await service.ConfirmarTransferenciaAsync(a.Id, "again")).ErrorCode.Should().Be("ESTADO_INVALIDO");
    }

    [Fact]
    public async Task RevokedCentralRole_DeniesVerificationDespiteOldClaims()
    {
        await using var f = new BillingSecurityFixture();
        var payment = await f.Pending(BillingSecurityFixture.EmpresaA);
        var identity = f.Identity(BillingSecurityFixture.Central);
        (await f.Db.Roles.SingleAsync(r => r.Codigo == "SUPERADMIN")).Activo = false;
        await f.Db.SaveChangesAsync();
        var service = new BillingService(f.Db, f.Payments, f.Email, f.Options, identity);
        (await service.ConfirmarTransferenciaAsync(payment.Id, "central")).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        payment.Status.Should().Be("PENDIENTE_VERIFICACION");
    }

    [Fact]
    public async Task TenantScope_IsEnforcedForReadsAndSubscriptionMutations()
    {
        await using var f = new BillingSecurityFixture();
        var service = f.Service(BillingSecurityFixture.AdminA);
        (await service.GetPaymentsAsync(BillingSecurityFixture.EmpresaB)).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.GetInvoicesAsync(BillingSecurityFixture.EmpresaB)).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.GetActiveSubscriptionAsync(BillingSecurityFixture.EmpresaB)).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.StartTrialAsync(new(BillingSecurityFixture.EmpresaB, BillingSecurityFixture.Basic, "audit@example.invalid"))).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.IniciarTransferenciaAsync(new(BillingSecurityFixture.EmpresaB, BillingSecurityFixture.Basic))).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.GetPaymentsAsync(BillingSecurityFixture.EmpresaA)).IsSuccess.Should().BeTrue();
        (await f.Db.BillingSubscriptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Operator_CannotManageOwnCompanySubscription()
    {
        await using var f = new BillingSecurityFixture();
        var service = f.Service(BillingSecurityFixture.OperatorA);
        (await service.StartTrialAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic, "audit@example.invalid"))).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.ChangePlanAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro))).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, false))).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.CreateCheckoutSessionAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic, "/billing"))).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await service.GetPortalUrlAsync(BillingSecurityFixture.EmpresaA)).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        (await f.Db.BillingSubscriptions.CountAsync()).Should().Be(0);
    }
}
