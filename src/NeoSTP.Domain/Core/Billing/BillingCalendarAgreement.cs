using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Billing;

/// <summary>Calendar invoicing is independent of administrative access.</summary>
public class BillingCalendarAgreement : AuditableEntity
{
    public int EmpresaId { get; set; }
    public int BillingSubscriptionId { get; set; }
    public BillingSubscription Subscription { get; set; } = null!;
    public int EmpresaPlanId { get; set; }
    public int PlanId { get; set; }
    public decimal MonthlyAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string TimeZoneId { get; set; } = "America/El_Salvador";
    public DateOnly FirstPeriodStartLocal { get; set; }
    public bool Active { get; set; } = true;
    public bool AutomaticSuspension { get; set; }
    public string AgreementKey { get; set; } = null!;
    public string Reason { get; set; } = null!;
}

public class BillingCalendarPeriod : AuditableEntity
{
    public int AgreementId { get; set; }
    public BillingCalendarAgreement Agreement { get; set; } = null!;
    public DateOnly PeriodStartLocal { get; set; }
    public DateOnly PeriodEndExclusiveLocal { get; set; }
    public DateOnly DueLocalDate { get; set; }
    public int BillingInvoiceId { get; set; }
    public BillingInvoice Invoice { get; set; } = null!;
    public int? BillingPaymentId { get; set; }
    public string? PaymentReference { get; set; }
}
