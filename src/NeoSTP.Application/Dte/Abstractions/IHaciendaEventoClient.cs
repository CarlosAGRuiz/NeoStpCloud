namespace NeoSTP.Application.Dte.Abstractions;

/// <summary>
/// Cliente genérico para transmitir eventos firmados a Hacienda
/// (invalidación, retorno, operaciones especiales). Cada evento usa su propio
/// endpoint y cuerpo, por eso se reciben como parámetros.
/// </summary>
public interface IHaciendaEventoClient
{
    /// <param name="endpointPath">Ruta relativa, ej. "/fesv/anulardte".</param>
    /// <param name="bodyJson">Cuerpo JSON ya serializado (incluye el JWS firmado del evento).</param>
    Task<EventoResult> PostAsync(string endpointPath, string bodyJson, string token, string ambienteCodigo, CancellationToken ct = default);
}

public class EventoResult
{
    public bool Success { get; set; }
    public int? CodigoHttp { get; set; }
    public string? Estado { get; set; }
    public string? SelloRecibido { get; set; }
    public string? CodigoMsg { get; set; }
    public string? DescripcionMsg { get; set; }
    public IReadOnlyList<string> Observaciones { get; set; } = Array.Empty<string>();
    public string? Raw { get; set; }
}
