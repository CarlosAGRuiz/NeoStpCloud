namespace NeoSTP.Application.Notificaciones;

public sealed record NotificationOutboxRequest(
    int EmpresaId,
    string Tipo,
    string Canal,
    string? Destinatario,
    string Payload,
    string ClaveIdempotencia,
    int MaxIntentos = 6);

/// <summary>
/// Registra intenciones de notificación. El DbContext scoped compartido permite
/// incluir la escritura en la transacción de negocio que invoca este contrato.
/// </summary>
public interface INotificationOutbox
{
    Task<int> EnqueueAsync(NotificationOutboxRequest request, CancellationToken ct = default);
}

public interface INotificationOutboxProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken ct = default);
    Task<int> PurgeSentAsync(int retentionDays, CancellationToken ct = default);
}

public sealed record AlertaPushOutboxPayload(int EmpresaId, int AlertaId);
