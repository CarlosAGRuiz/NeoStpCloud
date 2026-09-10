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
        // Un replay consulta el recurso persistido. No vuelve a firmar ni enviar, incluso si el
        // primer proceso terminó después de crear el borrador o perdió la respuesta de Hacienda.
        if (borrador.Value!.IdempotencyReplayed) return borrador;

        var id = borrador.Value!.Id;

        var generar = await _dte.GenerarAsync(empresaId, id, actor, ct);
        if (generar.IsFailure) return await ConDocumentoAsync(generar, borrador.Value, empresaId, ct);

        var validar = await _dte.ValidarAsync(empresaId, id, actor, ct);
        if (validar.IsFailure) return await ConDocumentoAsync(validar, generar.Value!, empresaId, ct);

        var firmar = await _dte.FirmarAsync(empresaId, id, actor, ct);
        if (firmar.IsFailure) return await ConDocumentoAsync(firmar, validar.Value!, empresaId, ct);

        var envio = await _dte.EnviarAsync(empresaId, id, actor, ct);
        return envio.IsFailure ? await ConDocumentoAsync(envio, firmar.Value!, empresaId, ct) : envio;
    }

    private async Task<Result<DteDocumentoDto>> ConDocumentoAsync(Result<DteDocumentoDto> failure,
        DteDocumentoDto previous, int empresaId, CancellationToken ct)
    {
        var current = await _dte.GetByIdAsync(empresaId, previous.Id, ct);
        var documento = current?.Value ?? previous;
        documento.Diagnostico = NeoSTP.Application.Dte.Diagnostico.DteDiagnosticoGuia.Crear(
            documento.EstadoCodigo, documento.SelloRecibido, documento.EnviadoAt, documento.RespuestaHacienda,
            failure.ErrorCode, failure.Error, failure.ValidationErrors);
        return Result<DteDocumentoDto>.FailWithValue(documento,
            failure.Error ?? "La emisión quedó incompleta. Consulte el DTE existente.", failure.ErrorCode, failure.ValidationErrors);
    }
}
