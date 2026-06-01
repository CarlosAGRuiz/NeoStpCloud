using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Domain.Core.Billing;

/// <summary>
/// Relaciona un Plan interno con los identificadores de precio/producto en cada proveedor de pago.
/// </summary>
public class BillingPlanProviderMapping : AuditableEntity
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public string Provider { get; set; } = string.Empty;       // Stripe | MercadoPago
    public string ExternalPlanId { get; set; } = string.Empty; // price_xxx / plan_xxx
    public string? ExternalProductId { get; set; }
    public string Currency { get; set; } = "MXN";
    public decimal UnitAmount { get; set; }
    public bool IsActive { get; set; } = true;
}
