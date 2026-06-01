using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Billing;

public class BillingInvoice : AuditableEntity
{
    public int Id { get; set; }
    public int BillingSubscriptionId { get; set; }
    public BillingSubscription Subscription { get; set; } = null!;

    public string? ExternalInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MXN";

    /// <summary>DRAFT | OPEN | PAID | VOID | UNCOLLECTIBLE</summary>
    public string Status { get; set; } = "DRAFT";

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PdfUrl { get; set; }
}
