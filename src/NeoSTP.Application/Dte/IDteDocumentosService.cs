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
}
