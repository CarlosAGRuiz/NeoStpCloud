using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Cliente HTTP real contra la API de Evento de Contingencia de Hacienda El Salvador.
/// Endpoint:  POST {base}/fesv/contingencia
/// Headers:   Authorization: Bearer {token}, Content-Type: application/json
/// Body:      { nit, documento(JWS firmado del evento) }
/// </summary>
public class HttpHaciendaContingenciaClient : IHaciendaContingenciaClient
{
    public const string HttpClientName = "HaciendaContingencia";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HaciendaOptions _options;
    private readonly ILogger<HttpHaciendaContingenciaClient> _logger;

    public HttpHaciendaContingenciaClient(
        IHttpClientFactory httpClientFactory,
        IOptions<HaciendaOptions> options,
        ILogger<HttpHaciendaContingenciaClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ContingenciaResult> EnviarAsync(ContingenciaRequest req, CancellationToken ct = default)
    {
        var baseUrl = req.AmbienteCodigo == "PRODUCCION" ? _options.ProduccionBaseUrl : _options.PruebasBaseUrl;
        var url = $"{baseUrl}/fesv/contingencia";
        _logger.LogInformation("HttpHaciendaContingenciaClient: POST {Url} nit={Nit}", url, req.Nit);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var body = JsonSerializer.Serialize(new { nit = req.Nit, documento = req.Documento });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.Token);

        try
        {
            var resp = await http.SendAsync(message, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            var code = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hacienda contingencia fail {Code}: {Body}", code, Truncate(raw, 500));
                return new ContingenciaResult
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

            return new ContingenciaResult
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
            _logger.LogError(ex, "HttpHaciendaContingenciaClient: error HTTP");
            return new ContingenciaResult { Success = false, CodigoHttp = 0, Estado = "ERROR", DescripcionMsg = ex.Message };
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
