using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Billing;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class BillingCustomerConfiguration : IEntityTypeConfiguration<BillingCustomer>
{
    public void Configure(EntityTypeBuilder<BillingCustomer> b)
    {
        b.ToTable("Billing_Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.ExternalCustomerId).HasMaxLength(200);
        b.Property(x => x.PhoneNumber).HasMaxLength(30);

        b.HasIndex(x => x.EmpresaId);
        b.HasIndex(x => new { x.Provider, x.ExternalCustomerId });

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BillingSubscriptionConfiguration : IEntityTypeConfiguration<BillingSubscription>
{
    public void Configure(EntityTypeBuilder<BillingSubscription> b)
    {
        b.ToTable("Billing_Subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(30).IsRequired();
        b.Property(x => x.ExternalSubscriptionId).HasMaxLength(200);

        b.HasIndex(x => new { x.BillingCustomerId, x.Status });
        b.HasIndex(x => x.ExternalSubscriptionId);

        b.HasOne(x => x.Customer).WithMany(c => c.Subscriptions).HasForeignKey(x => x.BillingCustomerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BillingPaymentConfiguration : IEntityTypeConfiguration<BillingPayment>
{
    public void Configure(EntityTypeBuilder<BillingPayment> b)
    {
        b.ToTable("Billing_Payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(10);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.ExternalPaymentId).HasMaxLength(200);
        b.Property(x => x.FailureReason).HasMaxLength(500);
        b.Property(x => x.ReceiptUrl).HasMaxLength(500);

        b.HasIndex(x => x.BillingSubscriptionId);
        b.HasOne(x => x.Subscription).WithMany(s => s.Payments).HasForeignKey(x => x.BillingSubscriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BillingInvoiceConfiguration : IEntityTypeConfiguration<BillingInvoice>
{
    public void Configure(EntityTypeBuilder<BillingInvoice> b)
    {
        b.ToTable("Billing_Invoices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(10);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.ExternalInvoiceId).HasMaxLength(200);
        b.Property(x => x.PdfUrl).HasMaxLength(500);

        b.HasIndex(x => x.BillingSubscriptionId);
        b.HasOne(x => x.Subscription).WithMany(s => s.Invoices).HasForeignKey(x => x.BillingSubscriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BillingWebhookEventConfiguration : IEntityTypeConfiguration<BillingWebhookEvent>
{
    public void Configure(EntityTypeBuilder<BillingWebhookEvent> b)
    {
        b.ToTable("Billing_WebhookEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.Property(x => x.ExternalEventId).HasMaxLength(200).IsRequired();
        b.Property(x => x.RawPayload).HasColumnType("nvarchar(max)");
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);

        b.HasIndex(x => new { x.Provider, x.ExternalEventId }).IsUnique();
        b.HasIndex(x => x.Processed);
    }
}

public class BillingPlanProviderMappingConfiguration : IEntityTypeConfiguration<BillingPlanProviderMapping>
{
    public void Configure(EntityTypeBuilder<BillingPlanProviderMapping> b)
    {
        b.ToTable("Billing_PlanProviderMappings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.ExternalPlanId).HasMaxLength(200).IsRequired();
        b.Property(x => x.ExternalProductId).HasMaxLength(200);
        b.Property(x => x.Currency).HasMaxLength(10);
        b.Property(x => x.UnitAmount).HasPrecision(18, 2);

        b.HasIndex(x => new { x.PlanId, x.Provider }).IsUnique();
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
