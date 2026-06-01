using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

/// <summary>
/// Procesa el payload JSON recibido de un webhook y ejecuta las transiciones de estado.
/// </summary>
public interface IBillingWebhookHandler
{
    Task<Result> HandleAsync(string provider, string eventType, string externalEventId, string rawPayload, CancellationToken ct = default);
}
