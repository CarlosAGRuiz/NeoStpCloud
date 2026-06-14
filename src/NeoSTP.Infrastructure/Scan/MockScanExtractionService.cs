using NeoSTP.Application.Scan;

namespace NeoSTP.Infrastructure.Scan;

/// <summary>
/// Extracción "mock" por defecto: no ejecuta OCR/IA real. Devuelve confianza 0 para que el
/// documento quede en REQUIERE_REVISION y el usuario capture/corrija los campos manualmente.
/// Sustituible por un proveedor real (Azure Document Intelligence, Google Vision, LLM) vía
/// configuración <c>Scan:Provider</c>.
/// </summary>
public class MockScanExtractionService : IScanExtractionService
{
    public Task<ScanExtraccion> ExtraerAsync(byte[] contenido, string contentType, CancellationToken ct = default)
        => Task.FromResult(new ScanExtraccion
        {
            Confianza = 0m,
            OcrProveedor = "Mock",
            OcrModelo = "manual",
            OcrDuracionMs = 0,
            OcrErrorResumen = "MOCK_PROVIDER",
            OcrIntentoAt = DateTime.UtcNow,
        });
}
