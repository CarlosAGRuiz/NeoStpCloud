using NeoSTP.Application.Common;
using NeoSTP.Application.Compras.Dtos;

namespace NeoSTP.Application.Compras;

/// <summary>
/// NEOCOMPRAS — proveedores y cuentas por pagar (espejo de Cobros/CxC). Registra facturas de
/// compra y sus pagos, deriva saldos y estados de vencimiento, opcionalmente genera el gasto
/// en NeoProfit y el egreso en Tesorería. Aislado por empresa.
/// </summary>
public interface ICompraService
{
    // Proveedores
    Task<Result<PagedResult<ProveedorDto>>> ListProveedoresAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<ProveedorDetalleDto>> GetProveedorAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<ProveedorDto>> CrearProveedorAsync(int empresaId, CreateProveedorRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ProveedorDto>> ActualizarProveedorAsync(int empresaId, int id, UpdateProveedorRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarProveedorAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result> ReactivarProveedorAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    // Facturas de compra (CxP)
    Task<Result<PagedResult<FacturaCompraDto>>> ListFacturasAsync(int empresaId, int? proveedorId, bool soloPendientes, PagedQuery query, CancellationToken ct = default);
    Task<Result<FacturaCompraDetalleDto>> GetFacturaAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<FacturaCompraDetalleDto>> CrearFacturaAsync(int empresaId, CrearFacturaCompraRequest request, string? actor, CancellationToken ct = default);
    Task<Result> AnularFacturaAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    // Pagos
    Task<Result<PagoProveedorDto>> RegistrarPagoAsync(int empresaId, RegistrarPagoProveedorRequest request, string? actor, CancellationToken ct = default);
    Task<Result> AnularPagoAsync(int empresaId, int pagoId, string? actor, CancellationToken ct = default);

    Task<Result<ComprasResumenDto>> ResumenAsync(int empresaId, CancellationToken ct = default);
}
