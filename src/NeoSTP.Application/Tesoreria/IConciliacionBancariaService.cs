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

    /// <summary>
    /// Sugerencias para las líneas NO_CONCILIADO de la cuenta (no persiste nada):
    /// primero matches 1:1, luego combinaciones N:1 (V2.5-S1, `CombinacionIds`).
    /// </summary>
    Task<Result<IReadOnlyList<SugerenciaConciliacionDto>>> SugerenciasAsync(int empresaId, int cuentaId, int toleranciaDias = 3, CancellationToken ct = default);

    /// <summary>
    /// Aplica un movimiento interno a la línea. Si el monto interno es menor al de la línea,
    /// queda PARCIAL y se pueden seguir aplicando movimientos hasta completar (V2.5-S1).
    /// </summary>
    Task<Result> ConciliarAsync(int empresaId, int movimientoBancoId, int movimientoTesoreriaId, string? actor, CancellationToken ct = default);

    /// <summary>Aplica varios movimientos internos de una vez (combinación N:1).</summary>
    Task<Result> ConciliarCombinacionAsync(int empresaId, int movimientoBancoId, IReadOnlyList<int> movimientoTesoreriaIds, string? actor, CancellationToken ct = default);

    /// <summary>Quita un movimiento interno de la línea (CONCILIADO/PARCIAL → PARCIAL/NO_CONCILIADO).</summary>
    Task<Result> QuitarDetalleAsync(int empresaId, int movimientoBancoId, int movimientoTesoreriaId, string? actor, CancellationToken ct = default);

    /// <summary>Aplica de una vez todas las sugerencias de confianza ALTA. Devuelve cuántas concilió.</summary>
    Task<Result<int>> ConciliarSugeridosAsync(int empresaId, int cuentaId, int toleranciaDias = 3, string? actor = null, CancellationToken ct = default);

    Task<Result> DesconciliarAsync(int empresaId, int movimientoBancoId, string? actor, CancellationToken ct = default);

    Task<Result<ConciliacionResumenDto>> ResumenAsync(int empresaId, int cuentaId, CancellationToken ct = default);
}
