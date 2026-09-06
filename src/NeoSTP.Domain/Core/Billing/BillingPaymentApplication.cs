using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Domain.Core.Billing;

/// <summary>One atomic commercial application per checkout, independently of delivery/transaction count.</summary>
public sealed class BillingPaymentApplication : AuditableEntity
{
    public int BillingCheckoutIntentId { get; set; }
    public BillingCheckoutIntent CheckoutIntent { get; set; } = null!;
    public int BillingPaymentNotificationId { get; set; }
    public BillingPaymentNotification Notification { get; set; } = null!;
    public int BillingPaymentId { get; set; }
    public BillingPayment Payment { get; set; } = null!;
    public int BillingSubscriptionId { get; set; }
    public BillingSubscription Subscription { get; set; } = null!;
    public int EmpresaPlanId { get; set; }
    public EmpresaPlan EmpresaPlan { get; set; } = null!;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime AppliedAt { get; set; }
    public string ModuleIdsJson { get; set; } = "[]";
    public string CommercialSnapshotJson { get; set; } = string.Empty;
}
