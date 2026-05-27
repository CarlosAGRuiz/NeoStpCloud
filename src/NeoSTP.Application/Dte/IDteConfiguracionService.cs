using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Dtos;

namespace NeoSTP.Application.Dte;

public interface IDteConfiguracionService
{
    Task<Result<DteConfiguracionDto>> GetAsync(int empresaId, CancellationToken ct = default);
    Task<Result<DteConfiguracionDto>> SaveAsync(int empresaId, SaveDteConfiguracionRequest request, string? actor, CancellationToken ct = default);
    Task<Result<DteConfiguracionDto>> UploadCertificadoAsync(int empresaId, UploadCertificadoRequest request, string? actor, CancellationToken ct = default);
    Task<Result> EliminarCertificadoAsync(int empresaId, string? actor, CancellationToken ct = default);
    Task<Result<ProbarConexionResultadoDto>> ProbarConexionAsync(int empresaId, string? actor, CancellationToken ct = default);
}
