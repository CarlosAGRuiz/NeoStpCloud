using System.Text.Json;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

internal static class HaciendaReceptionEvidence
{
    // Conservar sin cambios el cuerpo MH normal. En errores de transporte guardar su
    // contexto y el cuerpo original aparte: HTTP 500 no equivale a un rechazo fiscal.
    internal static string ForStorage(HaciendaReceptionResult response)
        => !string.IsNullOrWhiteSpace(response.Raw) && response.CodigoHttp is >= 200 and < 300
            && response.ClasificaMsg != "RESPUESTA_INVALIDA"
            ? response.Raw
            : JsonSerializer.Serialize(new
            {
                origen = "NEOSTP_TRANSPORTE", codigoHttp = response.CodigoHttp, estado = response.Estado,
                codigoMsg = response.CodigoMsg, descripcionMsg = response.DescripcionMsg,
                clasificaMsg = response.ClasificaMsg, observaciones = response.Observaciones,
                respuestaOriginal = response.Raw,
            });
}
