using NeoSTP.Application.Common;
using NeoSTP.Application.Pos.Dtos;

namespace NeoSTP.Application.Pos;

/// <summary>NEOPOS — sesiones / corte de caja: apertura con fondo, cierre con conteo e historial.</summary>
public interface IPosCajaService
{
    /// <summary>Sesión de caja abierta de la empresa con totales en vivo, o <c>null</c> si no hay ninguna.</summary>
    Task<Result<SesionCajaDto?>> GetEstadoAsync(int empresaId, CancellationToken ct = default);
    Task<Result<SesionCajaDto>> AbrirAsync(int empresaId, AbrirCajaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<SesionCajaDto>> CerrarAsync(int empresaId, int sesionId, CerrarCajaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<SesionCajaDto>> GetAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<PagedResult<SesionCajaDto>>> ListAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
}
