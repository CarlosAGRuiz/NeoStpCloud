using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Notificaciones;

namespace NeoSTP.Application.Notificaciones;

public interface IDteCorreoOutboxDispatcher
{
    Task<EmailSendResult> EnviarAsync(
        NotificationOutboxMessage message,
        DteCorreoOutboxPayload payload,
        CancellationToken ct = default);

    Task ActualizarAlertaAsync(
        NotificationOutboxMessage message,
        DteCorreoOutboxPayload payload,
        CancellationToken ct = default);
}
