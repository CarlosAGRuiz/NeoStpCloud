using NeoSTP.Application.Common;
using NeoSTP.Application.Crm.Dtos;

namespace NeoSTP.Application.Crm;

public interface ICrmService
{
    Task<Result<PagedResult<ContactoCrmDto>>> ListContactosAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<ContactoCrmDto>> GetContactoAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<ContactoCrmDto>> CrearContactoAsync(int empresaId, UpsertContactoCrmRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ContactoCrmDto>> ActualizarContactoAsync(int empresaId, int id, UpsertContactoCrmRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarContactoAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    Task<Result<IReadOnlyList<EtapaPipelineCrmDto>>> ListEtapasAsync(int empresaId, CancellationToken ct = default);
    Task<Result<EtapaPipelineCrmDto>> CrearEtapaAsync(int empresaId, UpsertEtapaPipelineCrmRequest request, string? actor, CancellationToken ct = default);
    Task<Result<EtapaPipelineCrmDto>> ActualizarEtapaAsync(int empresaId, int id, UpsertEtapaPipelineCrmRequest request, string? actor, CancellationToken ct = default);

    Task<Result<PagedResult<OportunidadCrmDto>>> ListOportunidadesAsync(int empresaId, string? estado, int? etapaId, int? clienteId, PagedQuery query, CancellationToken ct = default);
    Task<Result<OportunidadCrmDetalleDto>> GetOportunidadAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<OportunidadCrmDetalleDto>> CrearOportunidadAsync(int empresaId, CrearOportunidadCrmRequest request, string? actor, CancellationToken ct = default);
    Task<Result<OportunidadCrmDetalleDto>> ActualizarOportunidadAsync(int empresaId, int id, ActualizarOportunidadCrmRequest request, string? actor, CancellationToken ct = default);
    Task<Result<OportunidadCrmDetalleDto>> CambiarEtapaAsync(int empresaId, int id, CambiarEtapaOportunidadRequest request, string? actor, CancellationToken ct = default);

    Task<Result<PagedResult<ActividadCrmDto>>> ListActividadesAsync(int empresaId, bool soloPendientes, int? oportunidadId, PagedQuery query, CancellationToken ct = default);
    Task<Result<ActividadCrmDto>> CrearActividadAsync(int empresaId, CrearActividadCrmRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ActividadCrmDto>> CompletarActividadAsync(int empresaId, int id, CompletarActividadCrmRequest request, string? actor, CancellationToken ct = default);
    Task<Result> CancelarActividadAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    Task<Result<CrmResumenDto>> ResumenAsync(int empresaId, CancellationToken ct = default);
}
