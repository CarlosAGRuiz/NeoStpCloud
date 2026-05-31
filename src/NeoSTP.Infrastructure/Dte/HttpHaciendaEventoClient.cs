using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Cliente HTTP genérico para eventos DTE de Hacienda (invalidación, retorno, operaciones especiales).
/// POST {base}{endpointPath} con Authorization: Bearer {token} y el cuerpo JSON provisto.
/// </summary>
public class HttpHaciendaEventoClient : IHaciendaEventoClient
{
    public const string HttpClientName = "HaciendaEvento";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HaciendaOptions _options;
    private readonly ILogger<HttpHaciendaEventoClient> _logger;

    public HttpHaciendaEventoClient(
        IHttpClientFactory httpClientFactory,
        IOptions<HaciendaOptions> options,
        ILogger<HttpHaciendaEventoClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EventoResult> PostAsync(string endpointPath, string bodyJson, string token, string ambienteCodigo, CancellationToken ct = default)
    {
        var baseUrl = ambienteCodigo == "PRODUCCION" ? _options.ProduccionBaseUrl : _options.PruebasBaseUrl;
        var url = $"{baseUrl}{endpointPath}";
        _logger.LogInformation("HttpHaciendaEventoClient: POST {Url}", url);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        using var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var resp = await http.SendAsync(message, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            var code = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hacienda evento fail {Code}: {Body}", code, Truncate(raw, 500));
                return new EventoResult
                {
                    Success = false, CodigoHttp = code,
                    Estado = code == 401 ? "NO_AUTORIZADO" : "ERROR",
                    DescripcionMsg = ExtractProp(raw, "descripcionMsg") ?? Truncate(raw, 500),
                    CodigoMsg = ExtractProp(raw, "codigoMsg"),
                    Raw = raw,
                };
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var estado = root.TryGetProperty("estado", out var e) ? e.GetString() : null;
            var sello = root.TryGetProperty("selloRecibido", out var s) ? s.GetString() : null;
            var observaciones = new List<string>();
            if (root.TryGetProperty("observaciones", out var obs) && obs.ValueKind == JsonValueKind.Array)
                foreach (var o in obs.EnumerateArray())
                    if (o.GetString() is { Length: > 0 } v) observaciones.Add(v);

            return new EventoResult
            {
                Success = string.Equals(estado, "PROCESADO", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(estado, "RECIBIDO", StringComparison.OrdinalIgnoreCase),
                CodigoHttp = code,
                Estado = estado,
                SelloRecibido = sello,
                CodigoMsg = ExtractProp(raw, "codigoMsg"),
                DescripcionMsg = ExtractProp(raw, "descripcionMsg"),
                Observaciones = observaciones,
                Raw = raw,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HttpHaciendaEventoClient: error HTTP");
            return new EventoResult { Success = false, CodigoHttp = 0, Estado = "ERROR", DescripcionMsg = ex.Message };
        }
    }

    private static string? ExtractProp(string raw, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty(prop, out var v)) return v.GetString();
        }
        catch { /* no-op */ }
        return null;
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
