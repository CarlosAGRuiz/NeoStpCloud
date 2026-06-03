using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Orquesta el pipeline de emisión de DTE reusando <see cref="IDteDocumentosService"/>.
/// Pensado para consumo por API Key (NeoConnect): el cliente envía un solo POST y
/// recibe el documento ya procesado (o el error del paso que falló).
/// </summary>
public class ConnectDteService : IConnectDteService
{
    private readonly IDteDocumentosService _dte;

    public ConnectDteService(IDteDocumentosService dte) => _dte = dte;

    public async Task<Result<DteDocumentoDto>> EmitirAsync(
        int empresaId, CreateDteDocumentoRequest request, string? actor, CancellationToken ct = default)
    {
        var borrador = await _dte.CreateBorradorAsync(empresaId, request, actor, ct);
        if (borrador.IsFailure) return borrador;

        var id = borrador.Value!.Id;

        var generar = await _dte.GenerarAsync(empresaId, id, actor, ct);
        if (generar.IsFailure) return generar;

        var validar = await _dte.ValidarAsync(empresaId, id, actor, ct);
        if (validar.IsFailure) return validar;

        var firmar = await _dte.FirmarAsync(empresaId, id, actor, ct);
        if (firmar.IsFailure) return firmar;

        return await _dte.EnviarAsync(empresaId, id, actor, ct);
    }
}
