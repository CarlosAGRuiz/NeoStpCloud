using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Cliente HTTP real contra la API de Hacienda El Salvador.
///
/// Endpoint:  POST {base}/seguridad/auth
/// Body:      Content-Type: application/x-www-form-urlencoded
///            user=<NIT>&pwd=<password>
/// Respuesta (200): { "body": { "token": "...", "rol": {...} }, "status": "OK" }
/// Respuesta (401): { "body": null, "status": "ERROR", "descripcionMsg": "..." }
///
/// El token es de tipo Bearer y dura ~24h.
/// </summary>
public class HttpHaciendaAuthClient : IHaciendaAuthClient
{
    public const string HttpClientName = "HaciendaAuth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HaciendaOptions _options;
    private readonly ILogger<HttpHaciendaAuthClient> _logger;

    public HttpHaciendaAuthClient(
        IHttpClientFactory httpClientFactory,
        IOptions<HaciendaOptions> options,
        ILogger<HttpHaciendaAuthClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HaciendaAuthResult> AutenticarAsync(string usuario, string password, string ambienteCodigo, CancellationToken ct = default)
    {
        var baseUrl = ambienteCodigo == "PRODUCCION" ? _options.ProduccionBaseUrl : _options.PruebasBaseUrl;
        _logger.LogInformation("HttpHaciendaAuthClient: POST {Base}/seguridad/auth usuario={Usuario} ambiente={Amb}", baseUrl, usuario, ambienteCodigo);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("user", usuario),
            new KeyValuePair<string, string>("pwd", password),
        });

        try
        {
            var resp = await http.PostAsync($"{baseUrl}/seguridad/auth", form, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            var code = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hacienda auth fail {Code}: {Body}", code, Truncate(raw, 500));
                return new HaciendaAuthResult
                {
                    Success = false, CodigoHttp = code,
                    Mensaje = code == 401 ? "CREDENCIALES_INVALIDAS"
                            : code == 403 ? "USUARIO_BLOQUEADO"
                            : code == 429 ? "RATE_LIMIT"
                            : code >= 500 ? "HACIENDA_ERROR_SERVIDOR"
                            : "HACIENDA_ERROR",
                    Detalle = ExtractDescripcion(raw) ?? Truncate(raw, 500),
                };
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            var body = root.TryGetProperty("body", out var b) ? b : default;
            // Hacienda devuelve el token con el prefijo "Bearer " incluido en el valor.
            // Hay que quitarlo para que AuthenticationHeaderValue no lo duplique.
            var rawToken = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("token", out var t)
                ? t.GetString() : null;
            var token = rawToken?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                ? rawToken[7..].Trim()
                : rawToken;

            if (string.IsNullOrEmpty(token) || !string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return new HaciendaAuthResult
                {
                    Success = false, CodigoHttp = code,
                    Mensaje = "RESPUESTA_INESPERADA",
                    Detalle = ExtractDescripcion(raw) ?? Truncate(raw, 500),
                };
            }

            return new HaciendaAuthResult
            {
                Success = true, CodigoHttp = code,
                Token = token,
                // El token de Hacienda no expone exp; asumimos 24h conservadores
                ExpiresAt = DateTime.UtcNow.AddHours(23),
                Mensaje = "OK",
                Detalle = $"Autenticación HTTP exitosa contra {baseUrl}.",
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new HaciendaAuthResult
            {
                Success = false, CodigoHttp = 0,
                Mensaje = "TIMEOUT",
                Detalle = $"Timeout {_options.TimeoutSeconds}s contra {baseUrl}.",
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HttpHaciendaAuthClient: error HTTP");
            return new HaciendaAuthResult
            {
                Success = false, CodigoHttp = 0,
                Mensaje = "NETWORK_ERROR",
                Detalle = ex.Message,
            };
        }
    }

    private static string? ExtractDescripcion(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("descripcionMsg", out var d)) return d.GetString();
            if (doc.RootElement.TryGetProperty("mensaje", out var m)) return m.GetString();
        }
        catch { /* no-op */ }
        return null;
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
