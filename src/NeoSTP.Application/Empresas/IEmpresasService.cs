using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas.Dtos;

namespace NeoSTP.Application.Empresas;

public interface IEmpresasService
{
    /// <summary>Para SuperAdmin: lista todas. Para usuario de empresa: solo la suya.</summary>
    Task<Result<PagedResult<EmpresaDto>>> GetListAsync(int? scopeEmpresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<EmpresaDto>> GetByIdAsync(int? scopeEmpresaId, int id, CancellationToken ct = default);
    Task<Result<EmpresaDto>> CreateAsync(CreateEmpresaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<EmpresaDto>> UpdateAsync(int? scopeEmpresaId, int id, UpdateEmpresaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<LicenciaDto>> GetLicenciaAsync(int empresaId, CancellationToken ct = default);
    Task<Result<LicenciaDto>> AsignarPlanAsync(int empresaId, AsignarPlanRequest request, string? actor, CancellationToken ct = default);
    Task<Result<LicenciaDto>> ActivarModuloAsync(int empresaId, int moduloId, string? actor, CancellationToken ct = default);
    Task<Result<LicenciaDto>> DesactivarModuloAsync(int empresaId, int moduloId, string? actor, CancellationToken ct = default);
}

public interface ILicenciaResolver
{
    /// <summary>Obtiene el plan activo y los módulos activos de la empresa. Cachea en request.</summary>
    Task<LicenciaDto?> ResolveAsync(int empresaId, CancellationToken ct = default);
}
