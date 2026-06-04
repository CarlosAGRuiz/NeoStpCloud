using NeoSTP.Application.Common;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Scan.Dtos;

namespace NeoSTP.Application.Scan;

public sealed record ScanArchivo(byte[] Contenido, string ContentType, string Nombre);

/// <summary>
/// NeoScanAI: bandeja de documentos capturados, extracción OCR/IA, revisión/corrección
/// y conversión a gasto, compra o DTE recibido (alimenta NeoProfit). Aislado por EmpresaId.
/// </summary>
public interface IScanService
{
    Task<Result<PagedResult<ScanDocumentoDto>>> ListAsync(int empresaId, ScanQuery query, CancellationToken ct = default);
    Task<Result<ScanDocumentoDto>> GetAsync(int empresaId, int id, CancellationToken ct = default);
    Task<ScanArchivo?> GetArchivoAsync(int empresaId, int id, CancellationToken ct = default);

    /// <summary>Sube un documento capturado, ejecuta la extracción y lo deja en la bandeja.</summary>
    Task<Result<ScanDocumentoDto>> SubirAsync(int empresaId, SubirScanRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Corrige manualmente los campos extraídos.</summary>
    Task<Result<ScanDocumentoDto>> CorregirAsync(int empresaId, int id, CorregirScanRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Recibe un resultado de extracción desde un proveedor externo de NeoScanAI.</summary>
    Task<Result<ScanDocumentoDto>> SetResultadoAsync(int empresaId, int id, ScanResultadoRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Confirma el escaneo como gasto (crea ProfitGasto, alimenta NeoProfit).</summary>
    Task<Result<ScanDocumentoDto>> ConfirmarComoGastoAsync(int empresaId, int id, CreateProfitGastoRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Confirma el escaneo como compra (crea ProfitCompra, alimenta NeoProfit).</summary>
    Task<Result<ScanDocumentoDto>> ConfirmarComoCompraAsync(int empresaId, int id, CreateProfitCompraRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Confirma el escaneo como DTE recibido de un proveedor.</summary>
    Task<Result<ScanDocumentoDto>> RegistrarDteRecibidoAsync(int empresaId, int id, RegistrarDteRecibidoRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Rechaza el escaneo (no se convierte en nada).</summary>
    Task<Result> RechazarAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default);
}
