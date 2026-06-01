using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Billing;

/// <summary>
/// Almacena cada payload de webhook recibido para garantizar idempotencia.
/// </summary>
public class BillingWebhookEvent : AuditableEntity
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;   // Stripe | MercadoPago
    public string EventType { get; set; } = string.Empty;
    public string ExternalEventId { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public bool Processed { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
