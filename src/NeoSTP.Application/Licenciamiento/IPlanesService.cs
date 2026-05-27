using NeoSTP.Application.Common;
using NeoSTP.Application.Licenciamiento.Dtos;

namespace NeoSTP.Application.Licenciamiento;

public interface IPlanesService
{
    Task<Result<IReadOnlyList<PlanDto>>> GetListAsync(CancellationToken ct = default);
    Task<Result<PlanDto>> GetByIdAsync(int id, CancellationToken ct = default);
}

public interface IModulosService
{
    Task<Result<IReadOnlyList<ModuloDto>>> GetListAsync(CancellationToken ct = default);
}
