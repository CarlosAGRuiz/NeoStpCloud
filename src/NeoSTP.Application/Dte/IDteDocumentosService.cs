using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Dtos;

namespace NeoSTP.Application.Dte;

public interface IDteDocumentosService
{
    Task<Result<PagedResult<DteDocumentoListItemDto>>> GetListAsync(int empresaId, DteListQuery query, CancellationToken ct = default);
    Task<Result<DteDocumentoDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<DteDocumentoDto>> CreateBorradorAsync(int empresaId, CreateDteDocumentoRequest request, string? actor, CancellationToken ct = default);
    Task<Result<DteDocumentoDto>> GenerarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result<DteDocumentoDto>> ValidarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result<DteDocumentoDto>> FirmarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result<DteDocumentoDto>> EnviarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result> InvalidarAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default);
    Task<Result<string>> TransmitirEventoContingenciaAsync(int empresaId, IReadOnlyList<int> documentoIds, int tipoContingencia, string? motivo, string nombreResponsable, string tipoDocResponsable, string numeroDocResponsable, string? actor, CancellationToken ct = default);
    Task<Result<string>> TransmitirInvalidacionEventoAsync(int empresaId, int documentoId, int tipoAnulacion, string? motivoAnulacion, string? codigoGeneracionReemplazo, string nombreResponsable, string tipoDocResponsable, string numDocResponsable, string? actor, CancellationToken ct = default);
    Task<Result<string>> TransmitirEventoOperacionesEspecialesAsync(int empresaId, string? codigoGeneracionRef, string descripcion, decimal monto, string? actor, CancellationToken ct = default);
    Task<Result<DteArchivosDto>> ObtenerArchivosAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<DteReenvioResultDto>> ReenviarPorCorreoAsync(int empresaId, int id, string? destinatario, string? actor, CancellationToken ct = default);
}
