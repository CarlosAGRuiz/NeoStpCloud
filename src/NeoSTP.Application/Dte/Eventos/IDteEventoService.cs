using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Eventos.Dtos;

namespace NeoSTP.Application.Dte.Eventos;

/// <summary>
/// Sprint 15 — consulta y creación de eventos DTE persistidos. La transmisión real
/// (firma RS512 + envío a Hacienda) sigue viviendo en IDteDocumentosService —
/// este servicio orquesta la creación delegando en esos métodos (que persisten el
/// evento) y expone la consulta sobre las tablas Dte_Eventos*.
/// </summary>
public interface IDteEventoService
{
    Task<Result<IReadOnlyList<DteEventoListDto>>> GetListAsync(int empresaId, string? tipoEvento = null, string? estado = null, CancellationToken ct = default);
    Task<Result<DteEventoDetalleDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<DteEventoJsonDto>> GetJsonAsync(int empresaId, int id, CancellationToken ct = default);

    Task<Result<CrearEventoResultadoDto>> CrearInvalidacionAsync(int empresaId, CrearEventoInvalidacionRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CrearEventoResultadoDto>> CrearContingenciaAsync(int empresaId, CrearEventoContingenciaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CrearEventoResultadoDto>> CrearRetornoAsync(int empresaId, CrearEventoRetornoRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CrearEventoResultadoDto>> CrearOperacionesEspecialesAsync(int empresaId, CrearEventoOperacionesEspecialesRequest request, string? actor, CancellationToken ct = default);
}
