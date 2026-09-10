using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Billing;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Unit.Billing;

public sealed class BillingEntitlementTests
{
    [Fact]
    public async Task Legacy_license_reads_current_plan_and_normalizes_zero_limits_to_unlimited()
    {
        await using var f = new BillingEntitlementFixture();
        var plan = await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic);
        plan!.LimiteUsuarios = 0; plan.LimiteSucursales = null;
        var license = new EmpresaPlan { EmpresaId = BillingSecurityFixture.EmpresaA, PlanId = BillingSecurityFixture.Basic };
        f.Db.EmpresaPlanes.Add(license); await f.Db.SaveChangesAsync();
        var result = await BillingEntitlementReader.ReadAsync(f.Db, license);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.FromPayment.Should().BeFalse();
        result.Value.PlanName.Should().Be("Basic");
        result.Value.LimiteUsuarios.Should().BeNull(); result.Value.LimiteSucursales.Should().BeNull();
        result.Value.ModuleIds.Should().Equal(BillingEntitlementFixture.Module);
    }

    [Fact]
    public async Task Paid_rights_keep_acquired_name_limits_and_modules_after_catalog_changes()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var plan = await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic);
        plan!.Nombre = "Changed catalog name"; plan.Codigo = "CHANGED"; plan.LimiteUsuarios = 500;
        plan.LimiteSucursales = 50; plan.LimitePuntosVenta = 30; plan.LimiteDteMensual = 10000;
        (await f.Db.PlanModulos.SingleAsync()).Activo = false;
        await f.Db.SaveChangesAsync();
        var result = await f.Read();
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.FromPayment.Should().BeTrue();
        result.Value.PlanCode.Should().Be("AUDIT_BASIC"); result.Value.PlanName.Should().Be("Basic");
        result.Value.LimiteUsuarios.Should().Be(5); result.Value.LimiteSucursales.Should().Be(2);
        result.Value.LimitePuntosVenta.Should().Be(3); result.Value.LimiteDteMensual.Should().Be(100);
        result.Value.ModuleIds.Should().Equal(BillingEntitlementFixture.Module);
    }

    [Fact]
    public async Task Paid_zero_limits_remain_unlimited_after_catalog_starts_imposing_limits()
    {
        await using var f = new BillingEntitlementFixture();
        var plan = await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic);
        plan!.LimiteUsuarios = plan.LimiteSucursales = plan.LimitePuntosVenta = plan.LimiteDteMensual = 0;
        await f.Db.SaveChangesAsync(); await f.Paid();
        plan = await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic); plan!.LimiteUsuarios = 1; await f.Db.SaveChangesAsync();
        var result = await f.Read();
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.LimiteUsuarios.Should().BeNull(); result.Value.LimiteSucursales.Should().BeNull();
        result.Value.LimitePuntosVenta.Should().BeNull(); result.Value.LimiteDteMensual.Should().BeNull();
    }

    [Theory]
    [InlineData("json")]
    [InlineData("version")]
    [InlineData("missing-quota")]
    [InlineData("negative-quota")]
    [InlineData("string-quota")]
    [InlineData("duplicate-root")]
    [InlineData("duplicate-quota")]
    [InlineData("duplicate-module")]
    [InlineData("module-code")]
    [InlineData("module-id")]
    [InlineData("module-set")]
    [InlineData("company")]
    public async Task Invalid_paid_snapshot_never_falls_back_to_current_catalog(string malformed)
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var application = await f.Db.BillingPaymentApplications.SingleAsync();
        var intent = await f.Db.BillingCheckoutIntents.SingleAsync();
        var parsed = JsonNode.Parse(application.CommercialSnapshotJson)!.AsObject();
        switch (malformed)
        {
            case "version": parsed["Version"] = 2; break;
            case "missing-quota": parsed["Plan"]!.AsObject().Remove("LimiteUsuarios"); break;
            case "negative-quota": parsed["Plan"]!["LimiteUsuarios"] = -1; break;
            case "string-quota": parsed["Plan"]!["LimiteUsuarios"] = "unlimited"; break;
            case "duplicate-module": parsed["Modules"]!.AsArray().Add(parsed["Modules"]![0]!.DeepClone()); break;
            case "module-code": parsed["Modules"]![0]!["Codigo"] = "WRONG_CODE"; break;
            case "module-id": parsed["Modules"]![0]!["ModuloId"] = 999999; break;
            case "module-set": application.ModuleIdsJson = "[]"; break;
            case "company": parsed["EmpresaId"] = BillingSecurityFixture.EmpresaB; break;
        }
        var raw = parsed.ToJsonString();
        if (malformed == "json") raw = "not-json";
        if (malformed == "duplicate-root") raw = "{\"Version\":1," + raw[1..];
        if (malformed == "duplicate-quota") raw = raw.Replace("\"LimiteUsuarios\":5", "\"LimiteUsuarios\":5,\"LimiteUsuarios\":5");
        application.CommercialSnapshotJson = intent.CommercialSnapshotJson = raw;
        await f.Db.SaveChangesAsync();
        (await f.Read()).ErrorCode.Should().Be("LICENSE_SNAPSHOT_INVALID");
    }

    [Theory]
    [InlineData("foreign-intent-company")]
    [InlineData("foreign-customer")]
    [InlineData("unsucceeded-payment")]
    [InlineData("amount")]
    [InlineData("period")]
    [InlineData("sandbox")]
    [InlineData("receipt-transaction")]
    [InlineData("snapshot-disagreement")]
    public async Task Invalid_application_chain_is_not_treated_as_legacy_license(string mismatch)
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        switch (mismatch)
        {
            case "foreign-intent-company": (await f.Db.BillingCheckoutIntents.SingleAsync()).EmpresaId = BillingSecurityFixture.EmpresaB; break;
            case "foreign-customer": (await f.Db.BillingCustomers.SingleAsync()).EmpresaId = BillingSecurityFixture.EmpresaB; break;
            case "unsucceeded-payment": (await f.Db.BillingPayments.SingleAsync()).Status = "REFUNDED"; break;
            case "amount": (await f.Db.BillingPayments.SingleAsync()).Amount = 11; break;
            case "period": (await f.Db.BillingPaymentApplications.SingleAsync()).PeriodEnd = DateTime.UtcNow.AddYears(1); break;
            case "sandbox": (await f.Db.BillingPaymentNotifications.SingleAsync()).IsProduction = false; break;
            case "receipt-transaction": (await f.Db.BillingPaymentNotifications.SingleAsync()).TransactionId = Guid.NewGuid(); break;
            case "snapshot-disagreement": (await f.Db.BillingPaymentApplications.SingleAsync()).CommercialSnapshotJson = "{}"; break;
        }
        await f.Db.SaveChangesAsync();
        (await f.Read()).ErrorCode.Should().Be("LICENSE_SNAPSHOT_INVALID");
    }

    [Fact]
    public async Task Replacement_license_without_application_cannot_escape_paid_company_adoption()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var replacement = new EmpresaPlan { EmpresaId = BillingSecurityFixture.EmpresaA, PlanId = BillingSecurityFixture.Basic,
            FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddYears(10), EstadoCodigo = "ACTIVO" };
        f.Db.EmpresaPlanes.Add(replacement); await f.Db.SaveChangesAsync();
        var result = await BillingEntitlementReader.ReadAsync(f.Db, replacement);
        result.ErrorCode.Should().Be("LICENSE_SNAPSHOT_INVALID");
    }

    [Fact]
    public async Task License_guard_enforces_paid_user_quota_after_catalog_increases_it()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!.LimiteUsuarios = 100;
        for (var index = 0; index < 3; index++)
            f.Db.Usuarios.Add(BillingSecurityFixture.User(998010 + index, "OPERADOR", BillingSecurityFixture.EmpresaA));
        await f.Db.SaveChangesAsync();
        var guard = new NeoSTP.Infrastructure.Services.LicenciaGuardService(f.Db);
        var result = await guard.ValidarLimiteAsync(BillingSecurityFixture.EmpresaA,
            NeoSTP.Application.Licenciamiento.RecursoLimitado.Usuarios);
        result.ErrorCode.Should().Be("LIMIT_EXCEEDED");
        result.Error.Should().Contain("5").And.Contain("Basic");
    }

    [Fact]
    public async Task License_guard_rejects_invalid_paid_snapshot_instead_of_using_unlimited_catalog()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!.LimiteUsuarios = null;
        (await f.Db.BillingPaymentApplications.SingleAsync()).CommercialSnapshotJson = "{}";
        await f.Db.SaveChangesAsync();
        var guard = new NeoSTP.Infrastructure.Services.LicenciaGuardService(f.Db);
        var result = await guard.ValidarLimiteAsync(BillingSecurityFixture.EmpresaA,
            NeoSTP.Application.Licenciamiento.RecursoLimitado.Usuarios);
        result.ErrorCode.Should().Be("LICENSE_SNAPSHOT_INVALID");
    }
    [Fact]
    public async Task Initiating_transfer_cannot_replace_paid_entitlements_or_create_another_payment()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var originalEnd = (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin;
        var originalLedger = (await f.Db.BillingPaymentApplications.SingleAsync()).CommercialSnapshotJson;

        var result = await f.Base.Service(BillingSecurityFixture.AdminA).IniciarTransferenciaAsync(
            new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro));

        result.ErrorCode.Should().Be("LICENSE_MANAGED_BY_BILLING");
        f.Db.BillingPayments.Should().ContainSingle(); f.Db.BillingSubscriptions.Should().ContainSingle();
        f.Db.BillingCustomers.Should().ContainSingle(); f.Db.BillingPaymentApplications.Should().ContainSingle();
        (await f.Db.EmpresaPlanes.SingleAsync()).PlanId.Should().Be(BillingSecurityFixture.Basic);
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().Be(originalEnd);
        (await f.Db.BillingPaymentApplications.SingleAsync()).CommercialSnapshotJson.Should().Be(originalLedger);
    }

    [Fact]
    public async Task Confirming_transfer_cannot_extend_or_overwrite_a_license_adopted_by_billing()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var sub = await f.Db.BillingSubscriptions.SingleAsync();
        var previousEnd = sub.CurrentPeriodEnd;
        var pending = new BillingPayment { BillingSubscriptionId = sub.Id, Amount = 10m, Currency = "USD",
            Metodo = "TRANSFERENCIA", Status = "PENDIENTE_VERIFICACION" };
        f.Db.BillingPayments.Add(pending); await f.Db.SaveChangesAsync();

        var result = await f.Base.Service(BillingSecurityFixture.Central).ConfirmarTransferenciaAsync(pending.Id, "synthetic-central");

        result.ErrorCode.Should().Be("LICENSE_MANAGED_BY_BILLING");
        (await f.Db.BillingPayments.SingleAsync(x => x.Id == pending.Id)).Status.Should().Be("PENDIENTE_VERIFICACION");
        (await f.Db.BillingPayments.SingleAsync(x => x.Id == pending.Id)).VerificadoAt.Should().BeNull();
        f.Db.BillingPayments.Count(x => x.Status == "SUCCEEDED").Should().Be(1);
        (await f.Db.BillingSubscriptions.SingleAsync()).CurrentPeriodEnd.Should().Be(previousEnd);
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().Be(previousEnd);
        f.Db.BillingPaymentApplications.Should().ContainSingle();
    }

    [Fact]
    public async Task Manual_plan_assignment_cannot_replace_a_paid_license_or_its_modules()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var audit = NSubstitute.Substitute.For<NeoSTP.Application.Auth.Abstractions.IAuditoriaService>();
        var service = new NeoSTP.Infrastructure.Services.EmpresasService(f.Db, audit);
        var license = await f.Db.EmpresaPlanes.SingleAsync();
        var originalId = license.Id; var originalEnd = license.FechaFin;

        var result = await service.AsignarPlanAsync(BillingSecurityFixture.EmpresaA,
            new() { PlanId = BillingSecurityFixture.Pro, FechaFin = DateTime.UtcNow.AddYears(10) }, "synthetic-central");

        result.ErrorCode.Should().Be("LICENSE_MANAGED_BY_BILLING");
        (await f.Db.EmpresaPlanes.SingleAsync()).Id.Should().Be(originalId);
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().Be(originalEnd);
        (await f.Db.EmpresaPlanes.SingleAsync()).EstadoCodigo.Should().Be("ACTIVO");
        (await f.Db.EmpresaModulos.SingleAsync()).ModuloId.Should().Be(BillingEntitlementFixture.Module);
        NSubstitute.SubstituteExtensions.ReceivedCalls(audit).Should().BeEmpty();
    }

    [Fact]
    public async Task Module_added_to_catalog_after_purchase_cannot_be_activated_outside_paid_snapshot()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        f.Db.PlanModulos.Add(new() { PlanId = BillingSecurityFixture.Basic, ModuloId = BillingEntitlementFixture.ExtraModule, Activo = true });
        await f.Db.SaveChangesAsync();
        var audit = NSubstitute.Substitute.For<NeoSTP.Application.Auth.Abstractions.IAuditoriaService>();
        var service = new NeoSTP.Infrastructure.Services.EmpresasService(f.Db, audit);

        var result = await service.ActivarModuloAsync(BillingSecurityFixture.EmpresaA, BillingEntitlementFixture.ExtraModule, "synthetic-central");

        result.ErrorCode.Should().Be("LICENSE_MANAGED_BY_BILLING");
        f.Db.EmpresaModulos.Should().ContainSingle();
        (await f.Db.EmpresaModulos.SingleAsync()).ModuloId.Should().Be(BillingEntitlementFixture.Module);
        f.Db.BillingPayments.Should().ContainSingle(); f.Db.BillingPaymentApplications.Should().ContainSingle();
        NSubstitute.SubstituteExtensions.ReceivedCalls(audit).Should().BeEmpty();
    }
    [Theory]
    [InlineData("quota")]
    [InlineData("module")]
    public async Task Real_checkout_service_rejects_changed_paid_terms_before_reserving_intent_or_calling_provider(string change)
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        if (change == "quota") (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!.LimiteUsuarios = 50;
        else f.Db.PlanModulos.Add(new() { PlanId = BillingSecurityFixture.Basic, ModuloId = BillingEntitlementFixture.ExtraModule });
        f.Db.BillingPlanProviderMappings.Add(new() { PlanId = BillingSecurityFixture.Basic, Provider = "Wompi",
            ExternalPlanId = "synthetic-renewal-price", UnitAmount = 10m, Currency = "USD", IsActive = true });
        await f.Db.SaveChangesAsync();
        var options = Microsoft.Extensions.Options.Options.Create(new BillingOptions
        {
            Provider = "Wompi", Checkout = new() { Enabled = true, Provider = "Wompi",
                ProviderAccountId = "synthetic-account", BeneficiaryId = "synthetic-app",
                SuccessUrl = "https://billing.example.invalid/success", CancelUrl = "https://billing.example.invalid/cancel" }
        });
        var provider = NSubstitute.Substitute.For<IPaymentProvider, IBillingCheckoutProvider>();
        NSubstitute.SubstituteExtensions.Returns(provider.ProviderName, "Wompi");
        NSubstitute.SubstituteExtensions.Returns(((IBillingCheckoutProvider)provider).ProviderName, "Wompi");
        var resolver = new PaymentProviderResolver([provider], options);
        var service = new BillingService(f.Db, resolver, f.Base.Email, options, f.Base.Identity(BillingSecurityFixture.AdminA));
        var originalIntentCount = await f.Db.BillingCheckoutIntents.CountAsync();

        var result = await service.CreateCheckoutSessionAsync(new(BillingSecurityFixture.EmpresaA,
            BillingSecurityFixture.Basic, options.Value.Checkout.SuccessUrl, "Wompi", "synthetic-renewal-key-001"));

        result.ErrorCode.Should().Be("BILLING_TRANSITION_UNSUPPORTED");
        (await f.Db.BillingCheckoutIntents.CountAsync()).Should().Be(originalIntentCount);
        NSubstitute.SubstituteExtensions.ReceivedCalls(provider)
            .Count(call => call.GetMethodInfo().Name == nameof(IBillingCheckoutProvider.CreateCheckoutAsync)).Should().Be(0);
        NSubstitute.SubstituteExtensions.ReceivedCalls(provider)
            .Should().OnlyContain(call => call.GetMethodInfo().Name == "get_ProviderName");
        f.Db.BillingPayments.Should().ContainSingle(); f.Db.BillingPaymentApplications.Should().ContainSingle();
    }
    [Fact]
    public async Task Globally_disabled_module_preserves_acquired_identity_but_renamed_module_invalidates_it()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var module = await f.Db.Modulos.FindAsync(BillingEntitlementFixture.Module);
        module!.Activo = false; await f.Db.SaveChangesAsync();
        var disabled = await f.Read();
        disabled.IsSuccess.Should().BeTrue(disabled.Error);
        disabled.Value!.ModuleIds.Should().Equal(BillingEntitlementFixture.Module);
        module.Codigo = "REASSIGNED_MEANING"; await f.Db.SaveChangesAsync();
        (await f.Read()).ErrorCode.Should().Be("LICENSE_SNAPSHOT_INVALID");
    }
}

internal sealed class BillingEntitlementFixture : IAsyncDisposable
{
    public const int Module = 996001, ExtraModule = 996002;
    public BillingSecurityFixture Base { get; } = new();
    public NeoStpDbContext Db => Base.Db;
    public DateTime PaidAt { get; } = DateTime.UtcNow.AddMinutes(-1);
    public BillingCheckoutIntent Intent { get; }
    public BillingEntitlementFixture()
    {
        var plan = Db.Planes.Single(p => p.Id == BillingSecurityFixture.Basic);
        plan.LimiteUsuarios = 5; plan.LimiteSucursales = 2; plan.LimitePuntosVenta = 3; plan.LimiteDteMensual = 100;
        Db.Modulos.AddRange(new Modulo { Id = Module, Codigo = "SYNTHETIC_RIGHT", Nombre = "Included" },
            new Modulo { Id = ExtraModule, Codigo = "SYNTHETIC_ADDON", Nombre = "Extra" });
        Db.PlanModulos.Add(new() { PlanId = BillingSecurityFixture.Basic, ModuloId = Module, Activo = true });
        Intent = new() { CorrelationId = Guid.NewGuid(), EmpresaId = BillingSecurityFixture.EmpresaA, PlanId = BillingSecurityFixture.Basic,
            PlanCode = "AUDIT_BASIC", PlanName = "Basic", Amount = 10m, Currency = "USD", Provider = "Wompi",
            ProviderAccountId = "synthetic-account", BeneficiaryId = "synthetic-app", IsProduction = true,
            ExternalCheckoutId = "12345", Status = BillingCheckoutStatuses.AwaitingPayment, CreatedAt = PaidAt.AddMinutes(-1) };
        Db.SaveChanges();
    }
    public async Task Paid()
    {
        Db.BillingCheckoutIntents.Add(Intent);
        var receipt = new BillingPaymentNotification { ReceiptId = Guid.NewGuid(), BillingCheckoutIntentId = Intent.Id,
            CheckoutCorrelationId = Intent.CorrelationId, TransactionId = Guid.NewGuid(), Provider = "Wompi",
            ProviderAccountId = Intent.ProviderAccountId, BeneficiaryId = Intent.BeneficiaryId, IsProduction = true,
            ExternalCheckoutId = Intent.ExternalCheckoutId!, Amount = 10m, Currency = "USD", TransactionAt = new DateTimeOffset(PaidAt),
            ProviderPaidAt = new DateTimeOffset(PaidAt), VerifiedAt = DateTime.UtcNow,
            Status = BillingPaymentNotificationStatuses.VerifiedCapturedProduction };
        Db.BillingPaymentNotifications.Add(receipt); await Db.SaveChangesAsync();
        Intent.CommercialSnapshotJson = await BillingCommercialSnapshot.CaptureAsync(Db, Intent); await Db.SaveChangesAsync();
        var options = Microsoft.Extensions.Options.Options.Create(new BillingOptions { PaymentApplication = new() { Enabled = true } });
        var result = await new BillingPaymentApplicationProcessor(Db, options).ApplyVerifiedPaymentAsync(receipt.ReceiptId);
        result.IsSuccess.Should().BeTrue(result.Error);
    }
    public async Task<NeoSTP.Application.Common.Result<BillingEntitlements>> Read()
        => await BillingEntitlementReader.ReadAsync(Db, await Db.EmpresaPlanes.SingleAsync());
    public async Task<BillingCheckoutIntent> Renewal()
    {
        var subscription = await Db.BillingSubscriptions.SingleAsync();
        var license = await Db.EmpresaPlanes.SingleAsync();
        return new() { EmpresaId = BillingSecurityFixture.EmpresaA, PlanId = BillingSecurityFixture.Basic, PlanCode = "AUDIT_BASIC",
            PlanName = "Basic", Amount = 10m, Currency = "USD", Provider = "Wompi", BillingInterval = "MONTH",
            BillingCustomerId = subscription.BillingCustomerId, BillingSubscriptionId = subscription.Id, EmpresaPlanId = license.Id };
    }
    public ValueTask DisposeAsync() => Base.DisposeAsync();
}
