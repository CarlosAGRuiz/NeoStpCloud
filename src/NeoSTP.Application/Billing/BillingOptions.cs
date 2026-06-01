namespace NeoSTP.Application.Billing;

public class BillingOptions
{
    /// <summary>Mock | Stripe | MercadoPago</summary>
    public string Provider { get; set; } = "Mock";
    public int TrialDays { get; set; } = 14;

    public StripeOptions Stripe { get; set; } = new();
    public MercadoPagoOptions MercadoPago { get; set; } = new();
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
