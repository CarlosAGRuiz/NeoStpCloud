using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Domain.Core.Billing;

/// <summary>Immutable commercial snapshot reserved before any external checkout effect.</summary>
public sealed class BillingCheckoutIntent : AuditableEntity
{
    public Guid CorrelationId { get; set; }
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public int? BillingCustomerId { get; set; }
    public BillingCustomer? Customer { get; set; }
    public int? BillingSubscriptionId { get; set; }
    public BillingSubscription? Subscription { get; set; }
    public int? EmpresaPlanId { get; set; }
    public EmpresaPlan? EmpresaPlan { get; set; }
    public bool IsProduction { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderAccountId { get; set; } = string.Empty;
    public string BeneficiaryId { get; set; } = string.Empty;
    public string IdempotencyKeyHash { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string BillingInterval { get; set; } = "MONTH";
    public string? CommercialSnapshotJson { get; set; }
    public string ExternalPlanId { get; set; } = string.Empty;
    public string? ExternalCustomerId { get; set; }
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string Status { get; set; } = BillingCheckoutStatuses.Processing;
    public string LeaseId { get; set; } = string.Empty;
    public DateTime LeaseExpiresAt { get; set; }
    public string? ExternalCheckoutId { get; set; }
    public string? RedirectUrl { get; set; }
    public DateTime? ProviderAcknowledgedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastErrorCode { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public static class BillingCheckoutStatuses
{
    public const string Processing = "PROCESSING";
    public const string AwaitingPayment = "AWAITING_PAYMENT";
    public const string RequiresReconciliation = "REQUIRES_RECONCILIATION";
    // Only a verified payment application may complete an intent, never a redirect/ACK.
    public const string Completed = "COMPLETED";
    public const string PaymentVerifiedSandbox = "PAYMENT_VERIFIED_SANDBOX";
}
