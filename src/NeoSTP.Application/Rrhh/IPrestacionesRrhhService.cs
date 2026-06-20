using NeoSTP.Application.Common;
using NeoSTP.Application.Rrhh.Dtos;

namespace NeoSTP.Application.Rrhh;

public interface IPrestacionesRrhhService
{
    Task<Result<PoliticaPrestacionesDto>> GetPoliticaAsync(int empresaId, CancellationToken ct = default);
    Task<Result<PoliticaPrestacionesDto>> UpdatePoliticaAsync(int empresaId, UpdatePoliticaPrestacionesRequest request, string? actor, CancellationToken ct = default);
    Task<Result<VacacionResumenEmpleadoDto>> GetVacacionResumenAsync(int empresaId, int empleadoId, DateOnly? fechaCorte = null, CancellationToken ct = default);
    Task<Result<PagedResult<SolicitudVacacionDto>>> ListVacacionesAsync(int empresaId, int? empleadoId, string? estado, PagedQuery query, CancellationToken ct = default);
    Task<Result<SolicitudVacacionDto>> SolicitarVacacionAsync(int empresaId, CrearSolicitudVacacionRequest request, string? actor, CancellationToken ct = default);
    Task<Result<SolicitudVacacionDto>> AprobarVacacionAsync(int empresaId, int id, ResolverSolicitudVacacionRequest request, string? actor, CancellationToken ct = default);
    Task<Result<SolicitudVacacionDto>> RechazarVacacionAsync(int empresaId, int id, ResolverSolicitudVacacionRequest request, string? actor, CancellationToken ct = default);
    Task<Result> CancelarVacacionAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result<List<AguinaldoCalculoDto>>> CalcularAguinaldosAsync(int empresaId, int anio, string? actor, CancellationToken ct = default);
    Task<Result<List<AguinaldoCalculoDto>>> ListAguinaldosAsync(int empresaId, int anio, CancellationToken ct = default);
    Task<Result<int>> AprobarAguinaldosAsync(int empresaId, int anio, string? actor, CancellationToken ct = default);
}
