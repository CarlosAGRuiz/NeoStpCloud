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
/// Cliente HTTP para el envío del lote de DTE en contingencia (MOMENTO 3).
/// Endpoint: POST {base}/fesv/recepcionlote
///
/// El cuerpo incluye: nit, selloEvento y la lista de documentos JWS firmados.
/// Hacienda responde con un codigoLote y un sello del lote; después se
/// consulta el estado individual de cada DTE con HaciendaConsultaLoteClient.
/// </summary>
public class HttpHaciendaLoteClient : IHaciendaLoteClient
{
    public const string HttpClientName = "HaciendaLote";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HaciendaOptions _options;
    private readonly ILogger<HttpHaciendaLoteClient> _logger;

    public HttpHaciendaLoteClient(
        IHttpClientFactory httpClientFactory,
        IOptions<HaciendaOptions> options,
        ILogger<HttpHaciendaLoteClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HaciendaLoteResult> EnviarLoteAsync(HaciendaLoteRequest req, CancellationToken ct = default)
    {
        if (!DteAmbientes.EsValido(req.AmbienteCodigo) || req.Ambiente != DteAmbientes.CodigoMh(req.AmbienteCodigo) || req.Items.Count == 0 || req.Items.Any(i => !DteFiscalContext.CoincideJws(i.Documento, req.AmbienteCodigo)))
            return new HaciendaLoteResult { Success = false, CodigoMsg = "DTE_PAYLOAD_INCOMPATIBLE", DescripcionMsg = "Todos los documentos del lote deben pertenecer al ambiente indicado." };

        var baseUrl = req.AmbienteCodigo == "PRODUCCION" ? _options.ProduccionBaseUrl : _options.PruebasBaseUrl;
        var url = $"{baseUrl}/fesv/recepcionlote";
        _logger.LogInformation("HttpHaciendaLoteClient: POST {Url} nit={Nit} items={Count}", url, req.Nit, req.Items.Count);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var body = JsonSerializer.Serialize(new
        {
            nit = req.Nit,
            selloEvento = req.SelloEvento,
            listadoDte = req.Items.Select(i => new
            {
                tipoDte = i.TipoDte,
                codigoGeneracion = i.CodigoGeneracion,
                documento = i.Documento,
            }).ToArray(),
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.Token);

        string? raw = null;
        int? code = null;
        try
        {
            using var resp = await http.SendAsync(message, ct);
            raw = await resp.Content.ReadAsStringAsync(ct);
            code = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("HaciendaLote fail {Code}: {Body}", code, Truncate(raw, 500));
                return new HaciendaLoteResult
                {
                    Success = false,
                    CodigoHttp = code,
                    Estado = code == 401 ? "NO_AUTORIZADO" : "ERROR",
                    DescripcionMsg = TryExtractProp(raw, "descripcionMsg") ?? Truncate(raw, 500),
                    CodigoMsg = TryExtractProp(raw, "codigoMsg"),
                    Raw = raw,
                };
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var estado = root.TryGetProperty("estado", out var e) ? e.GetString() : null;
            var sello = root.TryGetProperty("selloRecibido", out var s) ? s.GetString() : null;
            var codigoLote = root.TryGetProperty("codigoLote", out var cl) ? cl.GetString() : null;
            var observaciones = ParseObservaciones(root);

            return new HaciendaLoteResult
            {
                Success = !string.IsNullOrWhiteSpace(codigoLote)
                    && (!string.IsNullOrWhiteSpace(sello) || string.Equals(estado, "PROCESADO", StringComparison.OrdinalIgnoreCase)),
                CodigoHttp = code,
                CodigoLote = codigoLote,
                SelloRecibido = sello,
                Estado = estado,
                CodigoMsg = TryExtractProp(raw, "codigoMsg"),
                DescripcionMsg = TryExtractProp(raw, "descripcionMsg"),
                Observaciones = observaciones,
                Raw = raw,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HttpHaciendaLoteClient: error HTTP");
            return new HaciendaLoteResult { Success = false, CodigoHttp = 0, Estado = "ENVIADO", CodigoMsg = "NETWORK_ERROR", DescripcionMsg = "No se confirmó la respuesta del lote. Consulte Hacienda antes de reenviar." };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new HaciendaLoteResult { Success = false, CodigoHttp = 0, Estado = "ENVIADO", CodigoMsg = "TIMEOUT", DescripcionMsg = "Se agotó la espera del lote; su recepción no está confirmada." };
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            return new HaciendaLoteResult { Success = false, CodigoHttp = 0, Estado = "ENVIADO", CodigoMsg = "TIMEOUT", DescripcionMsg = "Se agotó la espera del lote; su recepción no está confirmada." };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return new HaciendaLoteResult { Success = false, CodigoHttp = code, Estado = "ENVIADO", CodigoMsg = "RESPUESTA_INVALIDA", Raw = raw, DescripcionMsg = "Hacienda devolvió una respuesta no interpretable. Concilie el lote antes de reenviar." };
        }
    }

    private static List<string> ParseObservaciones(JsonElement root)
    {
        var list = new List<string>();
        if (root.TryGetProperty("observaciones", out var obs) && obs.ValueKind == JsonValueKind.Array)
            foreach (var o in obs.EnumerateArray())
                if (o.GetString() is { Length: > 0 } v) list.Add(v);
        return list;
    }

    private static string? TryExtractProp(string raw, string prop)
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
