namespace NeoSTP.Application.Scan;

/// <summary>Resultado de la extracción OCR/IA de un documento capturado.</summary>
public sealed class ScanExtraccion
{
    public string? EmisorNombre { get; set; }
    public string? EmisorNit { get; set; }
    public string? EmisorNrc { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? TipoDocumento { get; set; }
    public string? NumeroControl { get; set; }
    public string? SelloRecibido { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Iva { get; set; }
    public decimal? Total { get; set; }
    /// <summary>Confianza global 0..1. 0 = sin datos (captura manual requerida).</summary>
    public decimal Confianza { get; set; }
}

/// <summary>
/// Extracción de campos desde la imagen/PDF de un documento. Implementaciones:
///   - MockScanExtractionService (default): no extrae; deja el documento para captura manual.
///   - (futuro) proveedor real OCR/IA (Azure Document Intelligence, Google Vision, LLM).
/// Toggle por configuración <c>Scan:Provider</c> (Mock | ...). Pluggable.
/// </summary>
public interface IScanExtractionService
{
    Task<ScanExtraccion> ExtraerAsync(byte[] contenido, string contentType, CancellationToken ct = default);
}
