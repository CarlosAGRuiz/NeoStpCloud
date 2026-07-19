using NeoSTP.Application.Common;
using NeoSTP.Application.Inventario.Dtos;

namespace NeoSTP.Application.Inventario;

/// <summary>
/// INVENTARIO — existencias y kardex con costeo por promedio ponderado. Mantiene el saldo por
/// producto, registra entradas/salidas/ajustes y actualiza el costo del producto (mejora NeoProfit).
/// Aislado por empresa.
/// </summary>
public interface IInventarioService
{
    /// <summary>Existencias: consolidadas por producto (sucursalId null) o de una sucursal (E2).</summary>
    Task<Result<PagedResult<ExistenciaDto>>> ListExistenciasAsync(int empresaId, bool soloStockBajo, PagedQuery query, int? sucursalId = null, CancellationToken ct = default);
    Task<Result<ExistenciaDto>> GetExistenciaAsync(int empresaId, int productoId, int? sucursalId = null, CancellationToken ct = default);
    Task<Result<PagedResult<MovimientoInventarioDto>>> GetKardexAsync(int empresaId, int productoId, PagedQuery query, int? sucursalId = null, CancellationToken ct = default);

    Task<Result<ExistenciaDto>> RegistrarEntradaAsync(int empresaId, RegistrarMovimientoInventarioRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ExistenciaDto>> RegistrarSalidaAsync(int empresaId, RegistrarMovimientoInventarioRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ExistenciaDto>> AjustarAsync(int empresaId, AjusteStockRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ExistenciaDto>> SetStockMinimoAsync(int empresaId, SetStockMinimoRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Traslado atómico entre sucursales: salida en origen + entrada en destino con kardex TRASLADO (E2).</summary>
    Task<Result> TrasladarAsync(int empresaId, TrasladoInventarioRequest request, string? actor, CancellationToken ct = default);

    Task<Result<InventarioResumenDto>> ResumenAsync(int empresaId, CancellationToken ct = default);

    /// <summary>
    /// Lotes con saldo de la empresa (opcionalmente de un producto). Con
    /// <paramref name="soloPorVencer"/> filtra vencidos o por vencer dentro de
    /// <paramref name="diasUmbral"/> días.
    /// </summary>
    Task<Result<IReadOnlyList<LoteDto>>> ListLotesAsync(int empresaId, int? productoId = null,
        bool soloPorVencer = false, int diasUmbral = 30, CancellationToken ct = default);
}
