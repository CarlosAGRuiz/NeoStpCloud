namespace NeoSTP.Application.Billing;

public class BillingOptions
{
    /// <summary>Proveedor por defecto: Mock | Stripe | MercadoPago | Wompi | PayPal | Transferencia</summary>
    public string Provider { get; set; } = "Mock";
    public int TrialDays { get; set; } = 14;

    public StripeOptions Stripe { get; set; } = new();
    public MercadoPagoOptions MercadoPago { get; set; } = new();
    public WompiOptions Wompi { get; set; } = new();
    public PayPalOptions PayPal { get; set; } = new();
    public TransferenciaOptions Transferencia { get; set; } = new();
}

public class StripeOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "/billing/portal";
    public string CancelUrl { get; set; } = "/billing/checkout";
}

public class MercadoPagoOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "/billing/portal";
    public string FailureUrl { get; set; } = "/billing/checkout";
    public string PendingUrl { get; set; } = "/billing/portal";
}

/// <summary>Wompi El Salvador (wompi.sv). Checkout hospedado vía API REST.</summary>
public class WompiOptions
{
    public string BaseUrl { get; set; } = "https://api.wompi.sv";
    /// <summary>Servidor de identidad de Wompi (OAuth2 client credentials).</summary>
    public string IdUrl { get; set; } = "https://id.wompi.sv";
    public string AppId { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "/billing/portal";
    public string CancelUrl { get; set; } = "/billing/checkout";
}

/// <summary>PayPal Orders/Subscriptions v2 (REST).</summary>
public class PayPalOptions
{
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";
    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string WebhookId { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "/billing/portal";
    public string CancelUrl { get; set; } = "/billing/checkout";
}

/// <summary>Pago por transferencia bancaria (offline, confirmación manual del admin).</summary>
public class TransferenciaOptions
{
    public string Banco { get; set; } = string.Empty;
    public string TipoCuenta { get; set; } = string.Empty;
    public string NumeroCuenta { get; set; } = string.Empty;
    public string Titular { get; set; } = string.Empty;
    public string Instrucciones { get; set; } = "Realiza la transferencia y sube el comprobante. Un administrador la verificará.";
}
