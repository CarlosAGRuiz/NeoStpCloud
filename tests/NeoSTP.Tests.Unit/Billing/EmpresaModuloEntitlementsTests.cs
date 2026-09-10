using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Billing;

public sealed class EmpresaModuloEntitlementsTests
{
    [Fact]
    public async Task Audited_company_grant_survives_paid_renewal_without_changing_plan_rights()
    {
        await using var f = new BillingEntitlementFixture();
        f.Db.EmpresaModulos.Add(Grant(BillingSecurityFixture.EmpresaA));
        await f.Db.SaveChangesAsync();
        await f.Paid();
        var service = new EmpresasService(f.Db, Substitute.For<IAuditoriaService>());
        var license = await service.ResolveAsync(BillingSecurityFixture.EmpresaA);
        var addon = license!.Modulos.Single(x => x.ModuloId == BillingEntitlementFixture.ExtraModule);
        addon.Activo.Should().BeTrue();
        addon.AutorizadoPorAcuerdo.Should().BeTrue();
        addon.IncluidoEnPlan.Should().BeFalse();
        (await f.Read()).Value!.ModuleIds.Should().Equal(BillingEntitlementFixture.Module);
        (await BillingCheckoutPolicy.ValidateAsync(f.Db, await f.Renewal(), DateTime.UtcNow)).IsSuccess.Should().BeTrue();
        await service.DesactivarModuloAsync(BillingSecurityFixture.EmpresaA, BillingEntitlementFixture.ExtraModule, "synthetic-admin");
        (await service.ResolveAsync(BillingSecurityFixture.EmpresaA))!.Modulos.Single(x => x.ModuloId == BillingEntitlementFixture.ExtraModule).Activo.Should().BeFalse();
    }

    [Fact]
    public async Task Foreign_grant_never_authorizes_unpurchased_module_for_paid_tenant()
    {
        await using var f = new BillingEntitlementFixture();
        await f.Paid();
        f.Db.EmpresaModulos.Add(Grant(BillingSecurityFixture.EmpresaB));
        f.Db.EmpresaModulos.Add(new EmpresaModulo { EmpresaId = BillingSecurityFixture.EmpresaA, ModuloId = BillingEntitlementFixture.ExtraModule });
        await f.Db.SaveChangesAsync();
        var service = new EmpresasService(f.Db, Substitute.For<IAuditoriaService>());
        (await service.ResolveAsync(BillingSecurityFixture.EmpresaA))!.Modulos.Single(x => x.ModuloId == BillingEntitlementFixture.ExtraModule).Activo.Should().BeFalse();
        (await BillingCheckoutPolicy.ValidateAsync(f.Db, await f.Renewal(), DateTime.UtcNow)).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("reason")]
    [InlineData("date")]
    [InlineData("future")]
    [InlineData("revoked")]
    public void Incomplete_or_revoked_authorization_does_not_grant_rights(string missing)
    {
        var grant = Grant(BillingSecurityFixture.EmpresaA);
        switch (missing)
        {
            case "actor": grant.ComplementoAutorizadoBy = " "; break;
            case "reason": grant.ComplementoMotivo = null; break;
            case "date": grant.ComplementoAutorizadoAt = null; break;
            case "future": grant.ComplementoAutorizadoAt = DateTime.UtcNow.AddDays(1); break;
            case "revoked": grant.ComplementoAutorizado = false; break;
        }
        EmpresaModuloEntitlements.HasGrant(grant).Should().BeFalse();
    }

    private static EmpresaModulo Grant(int empresaId) => new()
    {
        EmpresaId = empresaId, ModuloId = BillingEntitlementFixture.ExtraModule,
        ComplementoAutorizado = true, ComplementoAutorizadoAt = DateTime.UtcNow.AddMinutes(-1),
        ComplementoAutorizadoBy = "synthetic-admin", ComplementoMotivo = "Explicit tenant addon",
    };
}
