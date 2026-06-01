using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Cliente HTTP para consultar el estado de un lote de contingencia.
/// Endpoint: GET {base}/fesv/recepcion/consultadtelote/{codigoLote}
///
/// Hacienda responde con el estado global del lote y el estado de cada DTE
/// incluido (estado + selloRecibido individual). Se invoca tras obtener el
/// sello del lote para actualizar los sellos individuales de los DTE.
/// </summary>
public class HttpHaciendaConsultaLoteClient : IHaciendaConsultaLoteClient
{
    public const string HttpClientName = "HaciendaConsultaLote";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HaciendaOptions _options;
    private readonly ILogger<HttpHaciendaConsultaLoteClient> _logger;

    public HttpHaciendaConsultaLoteClient(
        IHttpClientFactory httpClientFactory,
        IOptions<HaciendaOptions> options,
        ILogger<HttpHaciendaConsultaLoteClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HaciendaConsultaLoteResult> ConsultarLoteAsync(HaciendaConsultaLoteRequest req, CancellationToken ct = default)
    {
        var baseUrl = req.AmbienteCodigo == "PRODUCCION" ? _options.ProduccionBaseUrl : _options.PruebasBaseUrl;
        var url = $"{baseUrl}/fesv/recepcion/consultadtelote/{req.CodigoLote}";
        _logger.LogInformation("HttpHaciendaConsultaLoteClient: GET {Url}", url);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.Token);

        try
        {
            var resp = await http.SendAsync(message, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            var code = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("HaciendaConsultaLote fail {Code}: {Body}", code, Truncate(raw, 500));
                return new HaciendaConsultaLoteResult
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

            // Parsear los items individuales del lote
            var items = new List<HaciendaConsultaLoteItemResult>();
            if (root.TryGetProperty("listadoDteEnviados", out var lista) && lista.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in lista.EnumerateArray())
                {
                    var codGen = item.TryGetProperty("codigoGeneracion", out var cg) ? cg.GetString() : null;
                    var itemEstado = item.TryGetProperty("estado", out var ie) ? ie.GetString() : null;
                    var itemSello = item.TryGetProperty("selloRecibido", out var si) ? si.GetString() : null;
                    var itemCodMsg = item.TryGetProperty("codigoMsg", out var cm) ? cm.GetString() : null;
                    var itemDescMsg = item.TryGetProperty("descripcionMsg", out var dm) ? dm.GetString() : null;

                    if (!string.IsNullOrEmpty(codGen))
                        items.Add(new HaciendaConsultaLoteItemResult
                        {
                            CodigoGeneracion = codGen,
                            Estado = itemEstado,
                            SelloRecibido = itemSello,
                            CodigoMsg = itemCodMsg,
                            DescripcionMsg = itemDescMsg,
                        });
                }
            }

            return new HaciendaConsultaLoteResult
            {
                Success = true,
                CodigoHttp = code,
                Estado = estado,
                CodigoMsg = TryExtractProp(raw, "codigoMsg"),
                DescripcionMsg = TryExtractProp(raw, "descripcionMsg"),
                Items = items,
                Raw = raw,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HttpHaciendaConsultaLoteClient: error HTTP");
            return new HaciendaConsultaLoteResult { Success = false, CodigoHttp = 0, Estado = "ERROR", DescripcionMsg = ex.Message };
        }
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
