using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Billing;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public sealed class BillingPaymentApplicationConfiguration : IEntityTypeConfiguration<BillingPaymentApplication>
{
    public void Configure(EntityTypeBuilder<BillingPaymentApplication> b)
    {
        b.ToTable("Billing_PaymentApplications", t =>
            t.HasCheckConstraint("CK_Billing_PaymentApplications_Period", "[PeriodEnd] > [PeriodStart]"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.BillingCheckoutIntentId).IsUnique();
        b.HasIndex(x => x.BillingPaymentNotificationId).IsUnique();
        b.HasIndex(x => x.BillingPaymentId).IsUnique();
        b.HasOne(x => x.CheckoutIntent).WithMany().HasForeignKey(x => x.BillingCheckoutIntentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.BillingPaymentNotificationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.BillingPaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.BillingSubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EmpresaPlan).WithMany().HasForeignKey(x => x.EmpresaPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}