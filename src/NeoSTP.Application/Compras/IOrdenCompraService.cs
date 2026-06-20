using NeoSTP.Application.Common;
using NeoSTP.Application.Compras.Dtos;

namespace NeoSTP.Application.Compras;

public interface IOrdenCompraService
{
    Task<Result<PagedResult<OrdenCompraDto>>> ListAsync(int empresaId, string? estado, int? proveedorId, PagedQuery query, CancellationToken ct = default);
    Task<Result<OrdenCompraDetalleDto>> GetAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<OrdenCompraDetalleDto>> CrearAsync(int empresaId, GuardarOrdenCompraRequest request, string? actor, CancellationToken ct = default);
    Task<Result<OrdenCompraDetalleDto>> ActualizarAsync(int empresaId, int id, GuardarOrdenCompraRequest request, string? actor, CancellationToken ct = default);
    Task<Result<OrdenCompraDetalleDto>> EmitirAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result<OrdenCompraDetalleDto>> CancelarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result<OrdenCompraDetalleDto>> ConvertirAFacturaAsync(int empresaId, int id, ConvertirOrdenCompraRequest request, string? actor, CancellationToken ct = default);
}
