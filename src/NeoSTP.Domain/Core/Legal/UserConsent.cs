namespace NeoSTP.Domain.Core.Legal;

/// <summary>
/// Registro de consentimiento legal de un usuario (términos, privacidad, cookies, DPA).
/// Inmutable: nunca se edita, solo se insertan nuevas versiones.
/// </summary>
public class UserConsent
{
    public int Id { get; set; }

    /// <summary>Usuario que aceptó (null si aún no tiene cuenta — registro inicial).</summary>
    public int? UsuarioId { get; set; }

    /// <summary>Empresa en contexto al momento de aceptar (null para SuperAdmin o registro inicial).</summary>
    public int? EmpresaId { get; set; }

    /// <summary>Tipo de consentimiento: TERMS, PRIVACY, COOKIES, DPA.</summary>
    public string ConsentType { get; set; } = null!;

    /// <summary>Versión del documento legal aceptado (ej. "1.0", "2024-01").</summary>
    public string Version { get; set; } = null!;

    public DateTime AcceptedAt { get; set; }

    public string? AcceptedFromIp { get; set; }

    public string? AcceptedUserAgent { get; set; }
}
