using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>POST de consulta, de solo lectura fiscal, para un DTE previamente transmitido.</summary>
public sealed class HttpHaciendaConsultaDteClient : IHaciendaConsultaDteClient
{
    public const string HttpClientName = "HaciendaConsultaDte";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HaciendaOptions _options;
    private readonly ILogger<HttpHaciendaConsultaDteClient> _logger;

    public HttpHaciendaConsultaDteClient(IHttpClientFactory httpClientFactory,
        IOptions<HaciendaOptions> options, ILogger<HttpHaciendaConsultaDteClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HaciendaConsultaDteResult> ConsultarAsync(HaciendaConsultaDteRequest req, CancellationToken ct = default)
    {
        if (!DteAmbientes.EsValido(req.AmbienteCodigo)
            || req.Ambiente != DteAmbientes.CodigoMh(req.AmbienteCodigo)
            || string.IsNullOrWhiteSpace(req.NitEmisor) || req.NitEmisor.Length != 14 || req.NitEmisor.Any(c => !char.IsAsciiDigit(c))
            || string.IsNullOrWhiteSpace(req.TipoDte) || req.TipoDte.Length != 2 || req.TipoDte.Any(c => !char.IsAsciiDigit(c))
            || !Guid.TryParse(req.CodigoGeneracion, out _)
            || string.IsNullOrWhiteSpace(req.Token))
            return new() { CodigoMsg = "DTE_CONSULTA_INCOMPATIBLE", DescripcionMsg = "La identidad fiscal de la consulta no es válida." };

        var baseUrl = req.AmbienteCodigo == DteAmbientes.Produccion
            ? _options.ProduccionBaseUrl : _options.PruebasBaseUrl;
        var url = $"{baseUrl.TrimEnd('/')}/fesv/recepcion/consultadte/";
        _logger.LogInformation("Consulta DTE Hacienda: POST {Url} tipo={Tipo} codigo={Codigo}", url, req.TipoDte, req.CodigoGeneracion);

        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                nitEmisor = req.NitEmisor,
                tdte = req.TipoDte,
                codigoGeneracion = req.CodigoGeneracion,
            }), Encoding.UTF8, "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.Token);
        var http = _httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        string? raw = null;
        int? status = null;
        try
        {
            using var response = await http.SendAsync(message, ct);
            status = (int)response.StatusCode;
            raw = await response.Content.ReadAsStringAsync(ct);
            var parsed = Parse(raw);
            return new()
            {
                Success = response.IsSuccessStatusCode && parsed.Valid,
                CodigoHttp = status,
                Ambiente = parsed.Ambiente,
                Estado = parsed.Estado,
                CodigoGeneracion = parsed.CodigoGeneracion,
                SelloRecibido = parsed.SelloRecibido,
                FhProcesamiento = parsed.FhProcesamiento,
                CodigoMsg = parsed.CodigoMsg,
                DescripcionMsg = parsed.DescripcionMsg,
                Raw = raw,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new() { CodigoHttp = 0, CodigoMsg = "TIMEOUT", DescripcionMsg = "La consulta agotó su tiempo de espera; el DTE conserva su estado anterior." };
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            return new() { CodigoHttp = 0, CodigoMsg = "TIMEOUT", DescripcionMsg = "La consulta agotó su tiempo de espera; el DTE conserva su estado anterior." };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar el DTE en Hacienda.");
            return new() { CodigoHttp = 0, CodigoMsg = "NETWORK_ERROR", DescripcionMsg = "No se pudo consultar Hacienda; el DTE conserva su estado anterior." };
        }
        catch (JsonException)
        {
            return new() { CodigoHttp = status, CodigoMsg = "RESPUESTA_INVALIDA", DescripcionMsg = "Hacienda devolvió una respuesta no interpretable; el DTE conserva su estado anterior.", Raw = raw };
        }
    }

    private static ParsedResponse Parse(string raw)
    {
        using var json = JsonDocument.Parse(raw);
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        var root = json.RootElement;
        var codigo = Text(root, "codigoMsg");
        var generacion = Text(root, "codigoGeneracion")?.Trim();
        var ambiente = Text(root, "ambiente")?.Trim();
        var sello = Text(root, "selloRecibido") ?? Text(root, "numValidacion");
        return new(true, ambiente, Text(root, "estado"), generacion, sello,
            ParseDate(Text(root, "fhProcesamiento")), codigo, Text(root, "descripcionMsg"));
    }

    private static string? Text(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString()
            : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (DateTime.TryParseExact(value,
            ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm.ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],
            CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)) return parsed;
        return null;
    }

    private sealed record ParsedResponse(bool Valid, string? Ambiente, string? Estado,
        string? CodigoGeneracion, string? SelloRecibido, DateTime? FhProcesamiento,
        string? CodigoMsg, string? DescripcionMsg);
}
