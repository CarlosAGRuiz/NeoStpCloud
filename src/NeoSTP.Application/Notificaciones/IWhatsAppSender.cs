namespace NeoSTP.Application.Notificaciones;

public sealed class WhatsAppMessage
{
    public string To { get; set; } = null!;
    public string Body { get; set; } = null!;
    public Dictionary<string, string> Data { get; set; } = new();
}

public sealed class WhatsAppSendResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? Error { get; set; }
}

/// <summary>Proveedor pluggable para WhatsApp Business API. Mock por defecto en desarrollo.</summary>
public interface IWhatsAppSender
{
    Task<WhatsAppSendResult> EnviarAsync(WhatsAppMessage message, CancellationToken ct = default);
}
