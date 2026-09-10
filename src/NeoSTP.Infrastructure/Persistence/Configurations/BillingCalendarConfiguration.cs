using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public sealed class BillingCalendarAgreementConfiguration : IEntityTypeConfiguration<BillingCalendarAgreement>
{
    public void Configure(EntityTypeBuilder<BillingCalendarAgreement> b)
    {
        b.ToTable("Billing_CalendarAgreements");
        b.HasKey(x => x.Id);
        b.Property(x => x.MonthlyAmount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(10);
        b.Property(x => x.TimeZoneId).HasMaxLength(80);
        b.Property(x => x.AgreementKey).HasMaxLength(120);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasIndex(x => x.AgreementKey).IsUnique();
        b.HasIndex(x => x.BillingSubscriptionId).IsUnique();
        b.HasIndex(x => x.EmpresaId).IsUnique().HasFilter("[Active] = 1");
        b.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.BillingSubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmpresaPlan>().WithMany().HasForeignKey(x => x.EmpresaPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BillingCalendarPeriodConfiguration : IEntityTypeConfiguration<BillingCalendarPeriod>
{
    public void Configure(EntityTypeBuilder<BillingCalendarPeriod> b)
    {
        b.ToTable("Billing_CalendarPeriods");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.AgreementId, x.PeriodStartLocal }).IsUnique();
        b.HasIndex(x => x.BillingInvoiceId).IsUnique();
        b.HasIndex(x => x.BillingPaymentId).IsUnique().HasFilter("[BillingPaymentId] IS NOT NULL");
        b.HasIndex(x => new { x.AgreementId, x.PaymentReference }).IsUnique().HasFilter("[PaymentReference] IS NOT NULL");
        b.Property(x => x.PaymentReference).HasMaxLength(200);
        b.HasOne(x => x.Agreement).WithMany().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.BillingInvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BillingPayment>().WithMany().HasForeignKey(x => x.BillingPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
