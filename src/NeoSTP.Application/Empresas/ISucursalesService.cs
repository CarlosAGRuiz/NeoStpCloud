using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas.Dtos;

namespace NeoSTP.Application.Empresas;

public interface ISucursalesService
{
    Task<Result<PagedResult<SucursalDto>>> GetListAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<SucursalDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<SucursalDto>> CreateAsync(int empresaId, CreateSucursalRequest request, string? actor, CancellationToken ct = default);
    Task<Result<SucursalDto>> UpdateAsync(int empresaId, int id, UpdateSucursalRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
}

public interface IPuntosVentaService
{
    Task<Result<PagedResult<PuntoVentaDto>>> GetListAsync(int empresaId, int? sucursalId, PagedQuery query, CancellationToken ct = default);
    Task<Result<PuntoVentaDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<PuntoVentaDto>> CreateAsync(int empresaId, CreatePuntoVentaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<PuntoVentaDto>> UpdateAsync(int empresaId, int id, UpdatePuntoVentaRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
}
