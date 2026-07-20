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

    /// <summary>Aprueba una orden POR_APROBAR (E4): pasa a EMITIDA. Requiere Compras.Aprobar.</summary>
    Task<Result<OrdenCompraDetalleDto>> AprobarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    /// <summary>Rechaza una orden POR_APROBAR (E4): regresa a BORRADOR anotando el motivo.</summary>
    Task<Result<OrdenCompraDetalleDto>> RechazarAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default);

    /// <summary>Configura el umbral de aprobación de la empresa (null = sin flujo de aprobación).</summary>
    Task<Result> SetUmbralAprobacionAsync(int empresaId, decimal? umbral, string? actor, CancellationToken ct = default);

    Task<Result<OrdenCompraDetalleDto>> CancelarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result<OrdenCompraRecepcionDto>> RecibirAsync(int empresaId, int id, RegistrarRecepcionOrdenCompraRequest request, string? actor, CancellationToken ct = default);
    Task<Result<OrdenCompraDetalleDto>> ConvertirAFacturaAsync(int empresaId, int id, ConvertirOrdenCompraRequest request, string? actor, CancellationToken ct = default);
}
