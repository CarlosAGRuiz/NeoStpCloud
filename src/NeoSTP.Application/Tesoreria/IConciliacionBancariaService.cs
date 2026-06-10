using NeoSTP.Application.Common;
using NeoSTP.Application.Tesoreria.Dtos;

namespace NeoSTP.Application.Tesoreria;

/// <summary>
/// V2-D4 — Conciliación bancaria básica: importa el estado de cuenta del banco (CSV/XLSX),
/// sugiere emparejamientos contra los movimientos internos de tesorería (monto exacto +
/// ventana de fecha + referencia) y permite conciliar/desconciliar manualmente.
/// Aislado por empresa; opera siempre sobre una cuenta de tesorería tipo banco/caja.
/// </summary>
public interface IConciliacionBancariaService
{
    /// <summary>
    /// Importa líneas del estado de cuenta. Columnas: fecha (requerida), monto con signo
    /// (o cargo/abono separados), descripcion/concepto, referencia. Deduplica contra lo ya importado.
    /// </summary>
    Task<Result<BulkImportResult>> ImportarAsync(int empresaId, int cuentaId, BulkImportRequest request, string? actor, CancellationToken ct = default);

    Task<Result<PagedResult<MovimientoBancarioDto>>> ListAsync(int empresaId, int cuentaId, string? estado, PagedQuery query, CancellationToken ct = default);

    /// <summary>Sugerencias de match para las líneas NO_CONCILIADO de la cuenta (no persiste nada).</summary>
    Task<Result<IReadOnlyList<SugerenciaConciliacionDto>>> SugerenciasAsync(int empresaId, int cuentaId, int toleranciaDias = 3, CancellationToken ct = default);

    Task<Result> ConciliarAsync(int empresaId, int movimientoBancoId, int movimientoTesoreriaId, string? actor, CancellationToken ct = default);

    /// <summary>Aplica de una vez todas las sugerencias de confianza ALTA. Devuelve cuántas concilió.</summary>
    Task<Result<int>> ConciliarSugeridosAsync(int empresaId, int cuentaId, int toleranciaDias = 3, string? actor = null, CancellationToken ct = default);

    Task<Result> DesconciliarAsync(int empresaId, int movimientoBancoId, string? actor, CancellationToken ct = default);

    Task<Result<ConciliacionResumenDto>> ResumenAsync(int empresaId, int cuentaId, CancellationToken ct = default);
}
