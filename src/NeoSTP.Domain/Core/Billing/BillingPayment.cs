using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Billing;

public class BillingPayment : AuditableEntity
{
    public int BillingSubscriptionId { get; set; }
    public BillingSubscription Subscription { get; set; } = null!;

    public string? ExternalPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MXN";

    /// <summary>PENDING | PENDIENTE_VERIFICACION | SUCCEEDED | FAILED | REFUNDED</summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>Método/proveedor de pago: STRIPE | MERCADOPAGO | WOMPI | PAYPAL | TRANSFERENCIA | MOCK.</summary>
    public string? Metodo { get; set; }

    public DateTime PaidAt { get; set; }
    public string? FailureReason { get; set; }
    public string? ReceiptUrl { get; set; }

    // Transferencia bancaria (verificación manual)
    /// <summary>URL/ruta del comprobante de transferencia subido por el cliente.</summary>
    public string? ComprobanteUrl { get; set; }
    /// <summary>Usuario que verificó la transferencia.</summary>
    public string? VerificadoPor { get; set; }
    public DateTime? VerificadoAt { get; set; }
}
