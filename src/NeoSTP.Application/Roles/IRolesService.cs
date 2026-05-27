using NeoSTP.Application.Common;
using NeoSTP.Application.Roles.Dtos;

namespace NeoSTP.Application.Roles;

public interface IRolesService
{
    Task<Result<IReadOnlyList<RolDto>>> GetListAsync(int? empresaId, CancellationToken ct = default);
    Task<Result<RolDto>> GetByIdAsync(int? empresaId, int id, CancellationToken ct = default);
    Task<Result<RolDto>> CreateAsync(int? empresaId, CreateRolRequest request, string? actor, CancellationToken ct = default);
    Task<Result<RolDto>> UpdateAsync(int? empresaId, int id, UpdateRolRequest request, string? actor, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PermisoDto>>> GetPermisosAsync(CancellationToken ct = default);
}
