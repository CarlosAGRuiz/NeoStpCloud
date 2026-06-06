namespace NeoSTP.Infrastructure.Notificaciones;

/// <summary>
/// Configuración de Firebase Cloud Messaging (HTTP v1). Las credenciales del service account
/// son secretas: deben venir de variables de entorno / appsettings.Local (gitignored).
/// Se autentica con un JWT firmado (RS256) con la clave privada del service account.
/// </summary>
public sealed class FcmOptions
{
    /// <summary>ID del proyecto Firebase (project_id del service account).</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Email del service account (client_email). Secreto.</summary>
    public string ClientEmail { get; set; } = string.Empty;

    /// <summary>Clave privada PEM del service account (private_key). Secreto.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>Endpoint OAuth2 para canjear el JWT por un access token.</summary>
    public string TokenUri { get; set; } = "https://oauth2.googleapis.com/token";

    /// <summary>Base URL de FCM. Configurable para apuntar a un stub en tests.</summary>
    public string BaseUrl { get; set; } = "https://fcm.googleapis.com";
}
