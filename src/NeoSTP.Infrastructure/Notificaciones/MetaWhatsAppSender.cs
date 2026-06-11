using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;

namespace NeoSTP.Infrastructure.Notificaciones;

public sealed class MetaWhatsAppOptions
{
    /// <summary>Token permanente del sistema (Meta Business, producto WhatsApp).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Id del número emisor registrado en WhatsApp Business (Phone Number ID).</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://graph.facebook.com";
    public string ApiVersion { get; set; } = "v20.0";

    /// <summary>Código de país por defecto cuando el destino viene sin él (El Salvador).</summary>
    public string CodigoPaisDefecto { get; set; } = "503";
}

/// <summary>
/// V2.5-S2 — proveedor real de WhatsApp vía Meta Cloud API
/// (POST /{version}/{phoneNumberId}/messages, tipo "text"). Se activa con
/// WhatsApp:Provider=Meta + credenciales; el mock sigue siendo el default.
/// Nota Meta: fuera de la ventana de 24 h solo se entregan plantillas aprobadas;
/// los recordatorios de cobro deben usar una plantilla aprobada en producción.
/// </summary>
public class MetaWhatsAppSender : IWhatsAppSender
{
    public const string HttpClientName = "meta-whatsapp";

    private readonly IHttpClientFactory _httpFactory;
    private readonly MetaWhatsAppOptions _options;
    private readonly ILogger<MetaWhatsAppSender> _logger;

    public MetaWhatsAppSender(IHttpClientFactory httpFactory, IOptions<MetaWhatsAppOptions> options, ILogger<MetaWhatsAppSender> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WhatsAppSendResult> EnviarAsync(WhatsAppMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Token) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
            return new WhatsAppSendResult { Success = false, Error = "WhatsApp:Meta sin Token/PhoneNumberId configurados." };

        var destino = NormalizarTelefono(message.To, _options.CodigoPaisDefecto);
        if (destino is null)
            return new WhatsAppSendResult { Success = false, Error = $"Teléfono de destino inválido: '{message.To}'." };

        var url = $"{_options.BaseUrl.TrimEnd('/')}/{_options.ApiVersion}/{_options.PhoneNumberId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to = destino,
                type = "text",
                text = new { preview_url = false, body = message.Body },
            }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.Token);

        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = ExtraerError(payload) ?? $"HTTP {(int)response.StatusCode}";
                _logger.LogWarning("Meta WhatsApp rechazó el envío a {To}: {Error}", destino, error);
                return new WhatsAppSendResult { Success = false, Error = error };
            }

            var ok = JsonSerializer.Deserialize<MetaSendResponse>(payload);
            return new WhatsAppSendResult { Success = true, MessageId = ok?.Messages?.FirstOrDefault()?.Id };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Fallo de red enviando WhatsApp a {To}.", destino);
            return new WhatsAppSendResult { Success = false, Error = $"Fallo de red: {ex.Message}" };
        }
    }

    /// <summary>
    /// Meta exige E.164 sin '+': solo dígitos con código de país. Números locales de
    /// El Salvador (8 dígitos) reciben el código por defecto.
    /// </summary>
    internal static string? NormalizarTelefono(string? telefono, string codigoPaisDefecto)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return null;
        var digitos = new string(telefono.Where(char.IsDigit).ToArray());
        if (digitos.Length == 8) digitos = codigoPaisDefecto + digitos;
        return digitos.Length is >= 10 and <= 15 ? digitos : null;
    }

    internal static string? ExtraerError(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m)
                ? m.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class MetaSendResponse
    {
        [JsonPropertyName("messages")]
        public List<MetaMessageId>? Messages { get; set; }
    }

    private sealed class MetaMessageId
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
