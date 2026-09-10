using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NeoSTP.Domain.Core.Dte;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Cliente HTTP real contra la API de recepción DTE de Hacienda El Salvador.
///
/// Endpoint:  POST {base}/fesv/recepciondte
/// Headers:   Authorization: Bearer {token}, Content-Type: application/json
/// Body:      { ambiente, idEnvio, version, tipoDte, documento, codigoGeneracion }
/// Respuesta: { estado, selloRecibido, fhProcesamiento, clasificaMsg, codigoMsg, descripcionMsg, observaciones[] }
/// </summary>
public class HttpHaciendaReceptionClient : IHaciendaReceptionClient
{
    public const string HttpClientName = "HaciendaReception";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HaciendaOptions _options;
    private readonly ILogger<HttpHaciendaReceptionClient> _logger;

    public HttpHaciendaReceptionClient(
        IHttpClientFactory httpClientFactory,
        IOptions<HaciendaOptions> options,
        ILogger<HttpHaciendaReceptionClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HaciendaReceptionResult> EnviarAsync(HaciendaReceptionRequest req, CancellationToken ct = default)
    {
        if (!DteFiscalContext.CoincideRecepcion(req))
            return new HaciendaReceptionResult { Success = false, CodigoMsg = "DTE_PAYLOAD_INCOMPATIBLE", DescripcionMsg = "El documento firmado y el sobre de recepción no coinciden en versión o identidad fiscal." };

        var baseUrl = req.AmbienteCodigo == "PRODUCCION" ? _options.ProduccionBaseUrl : _options.PruebasBaseUrl;
        var url = $"{baseUrl}/fesv/recepciondte";
        _logger.LogInformation("HttpHaciendaReceptionClient: POST {Url} tipo={Tipo} cod={Cod}", url, req.TipoDte, req.CodigoGeneracion);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Incluir nit solo si fue provisto; algunos endpoints MH lo requieren para
        // identificar el certificado del emisor cuando el x5t no coincide exactamente.
        object bodyObj = string.IsNullOrEmpty(req.Nit)
            ? new { ambiente = req.Ambiente, idEnvio = req.IdEnvio, version = req.Version,
                    tipoDte = req.TipoDte, documento = req.Documento, codigoGeneracion = req.CodigoGeneracion }
            : (object)new { nit = req.Nit, ambiente = req.Ambiente, idEnvio = req.IdEnvio, version = req.Version,
                            tipoDte = req.TipoDte, documento = req.Documento, codigoGeneracion = req.CodigoGeneracion };
        var body = JsonSerializer.Serialize(bodyObj);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.Token);

        try
        {
            using var resp = await http.SendAsync(message, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            var code = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hacienda reception fail {Code}: {Body}", code, Truncate(raw, 500));
                return new HaciendaReceptionResult
                {
                    Success = false, CodigoHttp = code,
                    Estado = code == 401 ? "NO_AUTORIZADO" : "ERROR",
                    ClasificaMsg = code >= 500 ? "ERROR_SERVIDOR" : "ERROR",
                    DescripcionMsg = ExtractProp(raw, "descripcionMsg") ?? Truncate(raw, 500),
                    CodigoMsg = ExtractProp(raw, "codigoMsg"),
                    Observaciones = ExtractObservaciones(raw),
                    Raw = raw,
                };
            }

            var estado = ExtractProp(raw, "estado");
            var sello = ExtractProp(raw, "selloRecibido");
            if (string.IsNullOrWhiteSpace(estado))
                return new HaciendaReceptionResult
                {
                    Success = false, CodigoHttp = code, Estado = "ENVIADO",
                    ClasificaMsg = "RESPUESTA_INVALIDA", CodigoMsg = "DTE_RESULTADO_INCIERTO",
                    DescripcionMsg = "Hacienda no devolvió un estado interpretable. Se debe conciliar el documento existente.", Raw = raw,
                };
            DateTime? fhProc = null;
            if (DateTime.TryParse(ExtractProp(raw, "fhProcesamiento"), out var parsed))
                fhProc = parsed;

            return new HaciendaReceptionResult
            {
                Success = string.Equals(estado, "PROCESADO", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(sello),
                CodigoHttp = code,
                Estado = estado,
                SelloRecibido = sello,
                FhProcesamiento = fhProc ?? DateTime.UtcNow,
                ClasificaMsg = ExtractProp(raw, "clasificaMsg"),
                CodigoMsg = ExtractProp(raw, "codigoMsg"),
                DescripcionMsg = ExtractProp(raw, "descripcionMsg"),
                Observaciones = ExtractObservaciones(raw),
                Raw = raw,
            };
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            return new HaciendaReceptionResult
            {
                Success = false, CodigoHttp = 0, Estado = "ENVIADO", ClasificaMsg = "TIMEOUT",
                DescripcionMsg = "La recepción no pudo confirmarse dentro del tiempo de espera. Consulte Hacienda antes de reintentar.",
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new HaciendaReceptionResult
            {
                Success = false, CodigoHttp = 0,
                Estado = "CONTINGENCIA",
                ClasificaMsg = "TIMEOUT",
                DescripcionMsg = $"Timeout {_options.TimeoutSeconds}s contra {baseUrl}.",
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HttpHaciendaReceptionClient: error HTTP");
            return new HaciendaReceptionResult
            {
                Success = false, CodigoHttp = 0,
                Estado = "CONTINGENCIA",
                ClasificaMsg = "NETWORK_ERROR",
                DescripcionMsg = ex.Message,
            };
        }
    }

    private static string? ExtractProp(string raw, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(prop, out var v))
                return v.ValueKind == JsonValueKind.String ? v.GetString()
                    : prop == "codigoMsg" && v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null;
        }
        catch { /* no-op */ }
        return null;
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    private static IReadOnlyList<string> ExtractObservaciones(string raw)
    {
        try
        {
            using var json = JsonDocument.Parse(raw);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("observaciones", out var obs) && obs.ValueKind == JsonValueKind.Array)
                return obs.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.String)
                    .Select(o => o.GetString()!).Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
        }
        catch (JsonException) { /* Se conserva el cuerpo original para diagnóstico. */ }
        return [];
    }
}
