using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Billing;

public class BillingCustomer : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>ID del cliente en Stripe (cus_xxx) o MercadoPago (meli_xxx).</summary>
    public string? ExternalCustomerId { get; set; }
    public string Provider { get; set; } = "Mock";   // Mock | Stripe | MercadoPago
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public ICollection<BillingSubscription> Subscriptions { get; set; } = new List<BillingSubscription>();
}
