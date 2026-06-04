using Microsoft.Extensions.Logging;
using NeoSTP.Application.Notificaciones;

namespace NeoSTP.Infrastructure.Notificaciones;

/// <summary>
/// Push "mock" por defecto: registra el envío en logs en lugar de contactar a FCM.
/// Sustituible por un FcmPushSender real vía configuración <c>Push:Provider</c>.
/// </summary>
public class MockPushSender : IPushSender
{
    private readonly ILogger<MockPushSender> _logger;

    public MockPushSender(ILogger<MockPushSender> logger) => _logger = logger;

    public Task<PushResult> EnviarAsync(PushMessage message, CancellationToken ct = default)
    {
        var n = message.Tokens.Count;
        _logger.LogInformation("MockPushSender: push '{Titulo}' a {N} dispositivo(s). Cuerpo: {Cuerpo}",
            message.Titulo, n, message.Cuerpo);
        return Task.FromResult(new PushResult { Success = true, Enviados = n, Detalle = $"mock: {n} dispositivos" });
    }
}
