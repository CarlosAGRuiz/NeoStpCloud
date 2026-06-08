using NeoSTP.Application.Common;
using NeoSTP.Application.Rrhh.Dtos;

namespace NeoSTP.Application.Rrhh;

/// <summary>
/// Corridas de planilla (NEORRHH). Calcula el período para los empleados activos con el
/// NominaCalculator, permite recalcular, cerrar (genera gasto PLANILLA en NeoProfit) y anular.
/// Aislado por EmpresaId.
/// </summary>
public interface IPlanillaService
{
    Task<Result<PagedResult<PlanillaPeriodoDto>>> ListAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<PlanillaPeriodoDetalleDto>> GetAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<PlanillaPeriodoDetalleDto>> CrearAsync(int empresaId, CrearPlanillaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<PlanillaPeriodoDetalleDto>> RecalcularAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result> CerrarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result> AnularAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    /// <summary>Datos del recibo de un empleado dentro de un período (para PDF).</summary>
    Task<Result<ReciboNominaModel>> GetReciboAsync(int empresaId, int periodoId, int empleadoId, CancellationToken ct = default);

    /// <summary>Filas del período con datos de seguridad social, para exportes (planilla/ISSS/AFP).</summary>
    Task<Result<List<PlanillaExportRow>>> GetExportRowsAsync(int empresaId, int periodoId, CancellationToken ct = default);
}
