using Microsoft.Extensions.Logging;
using NeoSTP.Application.Notificaciones;

namespace NeoSTP.Infrastructure.Notificaciones;

/// <summary>Proveedor mock de WhatsApp: no llama APIs externas; solo registra el intento.</summary>
public class MockWhatsAppSender : IWhatsAppSender
{
    private readonly ILogger<MockWhatsAppSender> _logger;

    public MockWhatsAppSender(ILogger<MockWhatsAppSender> logger)
    {
        _logger = logger;
    }

    public Task<WhatsAppSendResult> EnviarAsync(WhatsAppMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("MockWhatsAppSender: mensaje a {To}. {Body}", message.To, message.Body);
        return Task.FromResult(new WhatsAppSendResult
        {
            Success = true,
            MessageId = $"mock-wa-{Guid.NewGuid():N}",
        });
    }
}
