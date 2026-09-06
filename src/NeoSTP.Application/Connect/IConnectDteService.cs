using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Dtos;

namespace NeoSTP.Application.Connect;

/// <summary>
/// Orquestación de emisión de DTE para consumo por API (NeoConnect).
/// Ejecuta el pipeline completo borrador → generar → validar → firmar → enviar
/// reusando <see cref="Dte.IDteDocumentosService"/>; se detiene en el primer fallo
/// devolviendo el estado alcanzado.
/// </summary>
public interface IConnectDteService
{
    /// <summary>
    /// Crea y procesa un DTE en un solo paso. Devuelve el documento en su estado final
    /// (idealmente PROCESADO). Una clave repetida devuelve el DTE existente sin reemitirlo.
    /// Si falla un paso después de crear el documento, el resultado puede contener Value con
    /// su referencia aunque IsFailure sea true. No cambiar la clave para reintentar esa venta.
    /// </summary>
    Task<Result<DteDocumentoDto>> EmitirAsync(
        int empresaId, CreateDteDocumentoRequest request, string? actor, CancellationToken ct = default);
}
