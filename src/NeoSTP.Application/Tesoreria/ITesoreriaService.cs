using NeoSTP.Application.Common;
using NeoSTP.Application.Tesoreria.Dtos;

namespace NeoSTP.Application.Tesoreria;

/// <summary>
/// NEOTESORERIA — cuentas (banco/caja) y movimientos. Permite registrar ingresos/egresos
/// (pagos de planilla, gastos, cobros) y mantiene el saldo corriente por cuenta. Aislado por empresa.
/// </summary>
public interface ITesoreriaService
{
    Task<Result<PagedResult<CuentaTesoreriaDto>>> ListCuentasAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<CuentaTesoreriaDetalleDto>> GetCuentaAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<CuentaTesoreriaDto>> CrearCuentaAsync(int empresaId, CreateCuentaTesoreriaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CuentaTesoreriaDto>> ActualizarCuentaAsync(int empresaId, int id, UpdateCuentaTesoreriaRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarCuentaAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result> ReactivarCuentaAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    Task<Result<PagedResult<MovimientoTesoreriaDto>>> ListMovimientosAsync(int empresaId, int? cuentaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<MovimientoTesoreriaDto>> RegistrarMovimientoAsync(int empresaId, RegistrarMovimientoRequest request, string? actor, CancellationToken ct = default);
    Task<Result> AnularMovimientoAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    Task<Result<TesoreriaResumenDto>> ResumenAsync(int empresaId, CancellationToken ct = default);
}
