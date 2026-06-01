namespace NeoSTP.Application.Legal;

/// <summary>
/// Contrato del servicio de documentos legales.
/// </summary>
public interface ILegalDocumentService
{
    /// <summary>
    /// Devuelve el contenido HTML del documento legal con placeholders reemplazados.
    /// </summary>
    /// <param name="slug">terms | privacy | cookies | dpa</param>
    /// <returns>HTML del documento, o null si el slug no existe.</returns>
    string? ObtenerContenido(string slug);

    /// <summary>
    /// Registra la aceptación de un conjunto de tipos de consentimiento por parte de un usuario.
    /// </summary>
    Task RegistrarConsentimientosAsync(
        int? usuarioId,
        int? empresaId,
        IEnumerable<string> tipos,
        string? ip,
        string? userAgent,
        CancellationToken ct = default);

    /// <summary>
    /// Versión actual de los documentos legales (de LegalOptions).
    /// </summary>
    string VersionActual { get; }
}
