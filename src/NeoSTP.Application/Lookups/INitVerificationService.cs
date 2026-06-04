namespace NeoSTP.Application.Lookups;

/// <summary>Resultado de la verificación de un NIT/DUI.</summary>
public sealed class NitVerificacionDto
{
    /// <summary>El documento tiene un formato válido (NIT 14 dígitos o DUI 9 dígitos).</summary>
    public bool FormatoValido { get; set; }
    /// <summary>Tipo detectado: NIT | DUI | DESCONOCIDO.</summary>
    public string TipoDocumento { get; set; } = "DESCONOCIDO";
    /// <summary>Documento normalizado (con guiones si aplica).</summary>
    public string DocumentoNormalizado { get; set; } = string.Empty;

    /// <summary>Se encontró en los datos de la empresa (cliente o emisor) para autocompletar.</summary>
    public bool EncontradoLocal { get; set; }
    public string? Nombre { get; set; }
    public string? Nrc { get; set; }
    public string? TipoContribuyente { get; set; }

    /// <summary>FORMATO | LOCAL | MH.</summary>
    public string Fuente { get; set; } = "FORMATO";
    public string Mensaje { get; set; } = string.Empty;
}

/// <summary>
/// Verificación de NIT/DUI. Implementación por defecto: valida formato salvadoreño y busca en los
/// datos locales (clientes/emisor) para autocompletar. El servicio en línea de MH no es público;
/// queda como hook pluggable (<c>Fuente=MH</c>) para cuando esté disponible.
/// </summary>
public interface INitVerificationService
{
    Task<NitVerificacionDto> VerificarAsync(int empresaId, string documento, CancellationToken ct = default);
}
