using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Billing;

public class BillingPayment : AuditableEntity
{
    public int Id { get; set; }
    public int BillingSubscriptionId { get; set; }
    public BillingSubscription Subscription { get; set; } = null!;

    public string? ExternalPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MXN";

    /// <summary>PENDING | SUCCEEDED | FAILED | REFUNDED</summary>
    public string Status { get; set; } = "PENDING";

    public DateTime PaidAt { get; set; }
    public string? FailureReason { get; set; }
    public string? ReceiptUrl { get; set; }
}
