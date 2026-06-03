using NeoSTP.Application.Common;
using NeoSTP.Application.Profit.Dtos;

namespace NeoSTP.Application.Profit;

/// <summary>
/// Servicio financiero de NeoProfit. Calcula ventas, IVA, costos, ganancia, rankings
/// y tendencia a partir de DTE PROCESADO, costos de productos y gastos/compras registrados.
/// Todo aislado por <c>EmpresaId</c>.
/// </summary>
public interface IProfitService
{
    Task<ProfitDashboardDto> GetDashboardAsync(int empresaId, ProfitPeriodoQuery periodo, CancellationToken ct = default);
    Task<IReadOnlyList<ProfitProductoDto>> GetProductosAsync(int empresaId, ProfitPeriodoQuery periodo, int top = 20, CancellationToken ct = default);
    Task<IReadOnlyList<ProfitClienteDto>> GetClientesAsync(int empresaId, ProfitPeriodoQuery periodo, int top = 20, CancellationToken ct = default);
    Task<IReadOnlyList<ProfitSucursalDto>> GetSucursalesAsync(int empresaId, ProfitPeriodoQuery periodo, CancellationToken ct = default);
    Task<IReadOnlyList<ProfitTendenciaPuntoDto>> GetTendenciaAsync(int empresaId, int dias = 30, CancellationToken ct = default);

    // Gastos
    Task<Result<PagedResult<ProfitGastoDto>>> ListGastosAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<ProfitGastoDto>> GetGastoAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<ProfitGastoDto>> CreateGastoAsync(int empresaId, CreateProfitGastoRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ProfitGastoDto>> UpdateGastoAsync(int empresaId, int id, UpdateProfitGastoRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarGastoAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    // Compras
    Task<Result<PagedResult<ProfitCompraDto>>> ListComprasAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<ProfitCompraDto>> GetCompraAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<ProfitCompraDto>> CreateCompraAsync(int empresaId, CreateProfitCompraRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ProfitCompraDto>> UpdateCompraAsync(int empresaId, int id, UpdateProfitCompraRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarCompraAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
}
