using NeoSTP.Application.Common;
using NeoSTP.Application.Rrhh.Dtos;

namespace NeoSTP.Application.Rrhh;

/// <summary>
/// Gestión de empleados (NEORRHH). CRUD con soft-delete; al crear/editar mantiene el
/// contrato laboral vigente (salario/periodicidad). Aislado por EmpresaId.
/// </summary>
public interface IEmpleadosService
{
    Task<Result<PagedResult<EmpleadoDto>>> GetListAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<EmpleadoDetalleDto>> GetAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<EmpleadoDetalleDto>> CreateAsync(int empresaId, CreateEmpleadoRequest request, string? actor, CancellationToken ct = default);
    Task<Result<EmpleadoDetalleDto>> UpdateAsync(int empresaId, int id, UpdateEmpleadoRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result> RestaurarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
}
