using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Billing;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public sealed class BillingPaymentNotificationConfiguration : IEntityTypeConfiguration<BillingPaymentNotification>
{
    public void Configure(EntityTypeBuilder<BillingPaymentNotification> b)
    {
        b.ToTable("Billing_PaymentNotifications", t =>
        {
            t.HasCheckConstraint("CK_Billing_PaymentNotifications_Capture", "[Status] <> 'VERIFIED_CAPTURED_PRODUCTION' OR ([IsProduction] = 1 AND [VerifiedAt] IS NOT NULL AND [ProviderPaidAt] IS NOT NULL AND [BillingCheckoutIntentId] IS NOT NULL)");
            t.HasCheckConstraint("CK_Billing_PaymentNotifications_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Billing_PaymentNotifications_Verified", "[Status] <> 'VERIFIED_SANDBOX' OR ([IsProduction] = 0 AND [VerifiedAt] IS NOT NULL AND [ProviderPaidAt] IS NOT NULL AND [BillingCheckoutIntentId] IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.ProviderAccountId).HasMaxLength(200).IsRequired();
        b.Property(x => x.BeneficiaryId).HasMaxLength(200).IsRequired();
        b.Property(x => x.ExternalCheckoutId).HasMaxLength(200).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.SemanticHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();
        b.Property(x => x.LeaseId).HasMaxLength(64).IsRequired();
        b.Property(x => x.LastErrorCode).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.ReceiptId).IsUnique();
        b.HasIndex(x => new { x.Provider, x.ProviderAccountId, x.IsProduction, x.TransactionId }).IsUnique();
        b.HasIndex(x => new { x.CheckoutCorrelationId, x.Status });
        b.HasIndex(x => new { x.Status, x.LeaseExpiresAt });
        b.HasOne(x => x.CheckoutIntent).WithMany().HasForeignKey(x => x.BillingCheckoutIntentId).OnDelete(DeleteBehavior.Restrict);
    }
}