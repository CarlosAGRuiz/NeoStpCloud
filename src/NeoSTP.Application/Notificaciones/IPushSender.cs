namespace NeoSTP.Application.Notificaciones;

public sealed class PushMessage
{
    public IReadOnlyList<string> Tokens { get; set; } = Array.Empty<string>();
    public string Titulo { get; set; } = null!;
    public string Cuerpo { get; set; } = null!;
    /// <summary>Datos extra (ej. tipo de alerta, entidadId) para el deep-link en la app.</summary>
    public Dictionary<string, string> Data { get; set; } = new();
}

public sealed class PushResult
{
    public bool Success { get; set; }
    public int Enviados { get; set; }
    public string? Detalle { get; set; }

    /// <summary>
    /// Tokens rechazados por el proveedor como no registrados/ inválidos (ej. FCM UNREGISTERED).
    /// El llamador debe desactivarlos para no reintentar. Vacío en el proveedor Mock.
    /// </summary>
    public IReadOnlyList<string> InvalidTokens { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Envío de notificaciones push. Implementaciones:
///   - MockPushSender (default): registra el envío en logs, no contacta a FCM.
///   - (futuro) FcmPushSender: Firebase Cloud Messaging real.
/// Toggle por configuración <c>Push:Provider</c> (Mock | Fcm). Pluggable.
/// </summary>
public interface IPushSender
{
    Task<PushResult> EnviarAsync(PushMessage message, CancellationToken ct = default);
}
