using NeoSTP.Application.Common;
using NeoSTP.Application.Scan.Dtos;

namespace NeoSTP.Application.Scan;

/// <summary>
/// Consulta de DTE recibidos de proveedores (registro/respaldo) generados al confirmar
/// escaneos de NeoScanAI como "DTE recibido". Solo lectura. Aislado por EmpresaId.
/// </summary>
public interface IDteRecibidoService
{
    Task<Result<PagedResult<DteRecibidoDto>>> ListAsync(int empresaId, DteRecibidoQuery query, CancellationToken ct = default);
    Task<Result<DteRecibidoDto>> GetAsync(int empresaId, int id, CancellationToken ct = default);
}
