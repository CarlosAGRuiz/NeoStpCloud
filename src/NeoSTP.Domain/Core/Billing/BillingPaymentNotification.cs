using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Billing;

/// <summary>Authenticated payment notice; no raw payload, card data or customer PII retained.</summary>
public sealed class BillingPaymentNotification : AuditableEntity
{
    public Guid ReceiptId { get; set; }
    public string Provider { get; set; } = "Wompi";
    public string ProviderAccountId { get; set; } = string.Empty;
    public string BeneficiaryId { get; set; } = string.Empty;
    public bool IsProduction { get; set; }
    public Guid TransactionId { get; set; }
    public Guid CheckoutCorrelationId { get; set; }
    public int? BillingCheckoutIntentId { get; set; }
    public BillingCheckoutIntent? CheckoutIntent { get; set; }
    public string ExternalCheckoutId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset TransactionAt { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string SemanticHash { get; set; } = string.Empty;
    public string Status { get; set; } = BillingPaymentNotificationStatuses.Processing;
    public string LeaseId { get; set; } = string.Empty;
    public DateTime LeaseExpiresAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTimeOffset? ProviderPaidAt { get; set; }
    public string? LastErrorCode { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public static class BillingPaymentNotificationStatuses
{
    public const string Processing = "PROCESSING";
    public const string RequiresReconciliation = "REQUIRES_RECONCILIATION";
    public const string VerifiedSandbox = "VERIFIED_SANDBOX";
    public const string VerifiedCapturedProduction = "VERIFIED_CAPTURED_PRODUCTION";
}