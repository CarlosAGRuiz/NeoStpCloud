using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Billing;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public sealed class BillingCheckoutIntentConfiguration : IEntityTypeConfiguration<BillingCheckoutIntent>
{
    public void Configure(EntityTypeBuilder<BillingCheckoutIntent> b)
    {
        b.ToTable("Billing_CheckoutIntents", t =>
        {
            t.HasCheckConstraint("CK_Billing_CheckoutIntents_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Billing_CheckoutIntents_Ack", "[Status] <> 'AWAITING_PAYMENT' OR ([ProviderAcknowledgedAt] IS NOT NULL AND [ExternalCheckoutId] IS NOT NULL AND [RedirectUrl] IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.ProviderAccountId).HasMaxLength(200).IsRequired();
        b.Property(x => x.BeneficiaryId).HasMaxLength(200).IsRequired();
        b.Property(x => x.IdempotencyKeyHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.PlanCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.PlanName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.BillingInterval).HasMaxLength(10).IsRequired();
        b.Property(x => x.ExternalPlanId).HasMaxLength(200).IsRequired();
        b.Property(x => x.ExternalCustomerId).HasMaxLength(200);
        b.Property(x => x.ExternalCheckoutId).HasMaxLength(200);
        b.Property(x => x.SuccessUrl).HasMaxLength(2000).IsRequired();
        b.Property(x => x.CancelUrl).HasMaxLength(2000).IsRequired();
        b.Property(x => x.RedirectUrl).HasMaxLength(2000);
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.LeaseId).HasMaxLength(64).IsRequired();
        b.Property(x => x.LastErrorCode).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.CorrelationId).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.IdempotencyKeyHash }).IsUnique();
        b.HasIndex(x => new { x.Provider, x.ProviderAccountId, x.ExternalCheckoutId })
            .IsUnique().HasFilter("[ExternalCheckoutId] IS NOT NULL");
        b.HasIndex(x => new { x.EmpresaId, x.Status });
        b.HasIndex(x => new { x.Status, x.LeaseExpiresAt });
        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.BillingCustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.BillingSubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EmpresaPlan).WithMany().HasForeignKey(x => x.EmpresaPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
