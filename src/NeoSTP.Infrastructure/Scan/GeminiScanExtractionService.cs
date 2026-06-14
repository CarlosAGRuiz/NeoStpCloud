using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Scan;

namespace NeoSTP.Infrastructure.Scan;

/// <summary>
/// Extracción OCR/IA real con Gemini Flash (Google Generative Language API), multimodal.
/// Envía la imagen/PDF + un prompt que pide los campos de un DTE salvadoreño en JSON
/// estricto y los mapea a <see cref="ScanExtraccion"/>.
///
/// Resiliente por diseño: ante cualquier fallo (HTTP, JSON inválido, sin API key) devuelve
/// <c>Confianza = 0</c> para que el documento caiga a REQUIERE_REVISION (captura manual),
/// nunca lanza. Toggle: <c>Scan:Provider=Gemini</c>; secretos en <c>Scan:Gemini</c>.
/// </summary>
public sealed class GeminiScanExtractionService : IScanExtractionService
{
    public const string HttpClientName = "GeminiScanClient";

    private const string Prompt = """
        Eres un extractor de datos de documentos tributarios electrónicos (DTE) de El Salvador
        (facturas, comprobantes de crédito fiscal, notas de crédito/débito). Analiza la imagen o
        PDF adjunto y devuelve EXCLUSIVAMENTE un objeto JSON con estas claves (usa null si no
        aparece el dato, no inventes valores):
        {
          "emisorNombre": string|null,
          "emisorNit": string|null,
          "emisorNrc": string|null,
          "fecha": "YYYY-MM-DD"|null,
          "tipoDocumento": string|null,
          "numeroControl": string|null,
          "selloRecibido": string|null,
          "subtotal": number|null,
          "iva": number|null,
          "total": number|null,
          "confianza": number
        }
        "confianza" es tu confianza global (0 a 1) en la extracción. Devuelve solo el JSON.
        """;

    private readonly IHttpClientFactory _httpFactory;
    private readonly GeminiScanOptions _opts;
    private readonly ILogger<GeminiScanExtractionService> _logger;

    public GeminiScanExtractionService(
        IHttpClientFactory httpFactory,
        IOptions<GeminiScanOptions> options,
        ILogger<GeminiScanExtractionService> logger)
    {
        _httpFactory = httpFactory;
        _opts = options.Value;
        _logger = logger;
    }

    public async Task<ScanExtraccion> ExtraerAsync(byte[] contenido, string contentType, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            _logger.LogWarning("GeminiScanExtractionService: sin API key (Scan:Gemini:ApiKey). Se deja captura manual.");
            return Manual(startedAt, sw, "GEMINI_API_KEY_MISSING");
        }
        if (contenido is null || contenido.Length == 0)
            return Manual(startedAt, sw, "EMPTY_CONTENT");

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            var url = $"{_opts.BaseUrl.TrimEnd('/')}/v1beta/models/{_opts.Model}:generateContent";

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = Prompt },
                            new { inlineData = new { mimeType = NormalizarMime(contentType), data = Convert.ToBase64String(contenido) } },
                        },
                    },
                },
                generationConfig = new { temperature = 0, responseMimeType = "application/json" },
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body),
            };
            req.Headers.TryAddWithoutValidation("x-goog-api-key", _opts.ApiKey);

            using var resp = await http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini OCR falló {Status}: {Body}", (int)resp.StatusCode, Truncar(json));
                return Manual(startedAt, sw, $"HTTP_{(int)resp.StatusCode}");
            }

            var texto = ExtraerTextoCandidato(json);
            if (string.IsNullOrWhiteSpace(texto))
                return Manual(startedAt, sw, "NO_CANDIDATE_TEXT");

            var ext = Mapear(texto!);
            ext.OcrProveedor = "Gemini";
            ext.OcrModelo = _opts.Model;
            ext.OcrDuracionMs = sw.ElapsedMilliseconds;
            ext.OcrIntentoAt = startedAt;
            return ext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en extracción Gemini; se deja captura manual.");
            return Manual(startedAt, sw, ex.GetType().Name);
        }
    }

    private ScanExtraccion Manual(DateTime startedAt, Stopwatch sw, string error)
        => new()
        {
            Confianza = 0m,
            OcrProveedor = "Gemini",
            OcrModelo = _opts.Model,
            OcrDuracionMs = sw.ElapsedMilliseconds,
            OcrErrorResumen = Truncar(error),
            OcrIntentoAt = startedAt,
        };

    /// <summary>Extrae el texto del primer candidato de la respuesta de Gemini.</summary>
    private static string? ExtraerTextoCandidato(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            return null;
        return parts[0].TryGetProperty("text", out var text) ? text.GetString() : null;
    }

    /// <summary>Parsea el JSON de campos devuelto por el modelo a <see cref="ScanExtraccion"/>.</summary>
    private ScanExtraccion Mapear(string textoJson)
    {
        try
        {
            // El modelo puede envolver el JSON en ```json ... ```; lo limpiamos por si acaso.
            var limpio = textoJson.Trim().TrimStart('`');
            var inicio = limpio.IndexOf('{');
            var fin = limpio.LastIndexOf('}');
            if (inicio < 0 || fin <= inicio) return new ScanExtraccion { Confianza = 0m };
            limpio = limpio.Substring(inicio, fin - inicio + 1);

            using var doc = JsonDocument.Parse(limpio);
            var r = doc.RootElement;

            var ext = new ScanExtraccion
            {
                EmisorNombre = Str(r, "emisorNombre"),
                EmisorNit = Str(r, "emisorNit"),
                EmisorNrc = Str(r, "emisorNrc"),
                Fecha = Fecha(r, "fecha"),
                TipoDocumento = Str(r, "tipoDocumento"),
                NumeroControl = Str(r, "numeroControl"),
                SelloRecibido = Str(r, "selloRecibido"),
                Subtotal = Dec(r, "subtotal"),
                Iva = Dec(r, "iva"),
                Total = Dec(r, "total"),
            };

            var confModelo = Dec(r, "confianza");
            ext.Confianza = confModelo is decimal c && c > 0 ? Math.Clamp(c, 0m, 1m) : Derivar(ext);
            return ext;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Respuesta Gemini no es JSON válido; se deja captura manual.");
            return new ScanExtraccion { Confianza = 0m };
        }
    }

    /// <summary>Confianza derivada si el modelo no la reporta: según campos clave presentes.</summary>
    private static decimal Derivar(ScanExtraccion e)
    {
        if (e.Total is not null && !string.IsNullOrWhiteSpace(e.EmisorNombre)) return 0.8m;
        if (e.Total is not null || !string.IsNullOrWhiteSpace(e.EmisorNombre)) return 0.5m;
        return 0m;
    }

    private static string NormalizarMime(string? contentType)
        => string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim().ToLowerInvariant();

    private static string Truncar(string s) => s.Length <= 500 ? s : s[..500];

    private static string? Str(JsonElement r, string name)
        => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString())
            : null;

    private static decimal? Dec(JsonElement r, string name)
    {
        if (!r.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String &&
            decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds)) return ds;
        return null;
    }

    private static DateOnly? Fecha(JsonElement r, string name)
    {
        var s = Str(r, name);
        if (s is null) return null;
        return DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;
    }
}
