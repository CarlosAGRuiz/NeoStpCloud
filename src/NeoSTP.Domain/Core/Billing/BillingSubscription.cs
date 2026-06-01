using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Domain.Core.Billing;

public class BillingSubscription : AuditableEntity
{
    public int Id { get; set; }
    public int BillingCustomerId { get; set; }
    public BillingCustomer Customer { get; set; } = null!;

    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    /// <summary>ID de la suscripción en el proveedor externo (sub_xxx, etc.).</summary>
    public string? ExternalSubscriptionId { get; set; }

    /// <summary>TRIALING | ACTIVE | PAST_DUE | CANCELED | INCOMPLETE | EXPIRED | SUSPENDED</summary>
    public string Status { get; set; } = SubscriptionStatus.Trialing;

    public DateTime TrialStart { get; set; } = DateTime.UtcNow;
    public DateTime TrialEnd { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? CanceledAt { get; set; }

    /// <summary>Cuando es true el acceso sigue hasta CurrentPeriodEnd aunque el estado sea CANCELED.</summary>
    public bool CancelAtPeriodEnd { get; set; }

    public ICollection<BillingPayment> Payments { get; set; } = new List<BillingPayment>();
    public ICollection<BillingInvoice> Invoices { get; set; } = new List<BillingInvoice>();
}

public static class SubscriptionStatus
{
    public const string Trialing   = "TRIALING";
    public const string Active     = "ACTIVE";
    public const string PastDue    = "PAST_DUE";
    public const string Canceled   = "CANCELED";
    public const string Incomplete = "INCOMPLETE";
    public const string Expired    = "EXPIRED";
    public const string Suspended  = "SUSPENDED";
}
