using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;

namespace NeoSTP.Infrastructure.Notificaciones;

/// <summary>
/// Envío real de push con Firebase Cloud Messaging HTTP v1. Obtiene un access token vía
/// service account (<see cref="IFcmAccessTokenProvider"/>) y envía un mensaje por token
/// (FCM v1 no acepta multicast). Reporta tokens inválidos/no registrados para que el
/// llamador los desactive. Toggle: <c>Push:Provider=Fcm</c>; secretos en <c>Push:Fcm</c>.
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    public const string HttpClientName = "FcmClient";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IFcmAccessTokenProvider _tokenProvider;
    private readonly FcmOptions _opts;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(
        IHttpClientFactory httpFactory,
        IFcmAccessTokenProvider tokenProvider,
        IOptions<FcmOptions> options,
        ILogger<FcmPushSender> logger)
    {
        _httpFactory = httpFactory;
        _tokenProvider = tokenProvider;
        _opts = options.Value;
        _logger = logger;
    }

    public async Task<PushResult> EnviarAsync(PushMessage message, CancellationToken ct = default)
    {
        var tokens = message.Tokens?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList() ?? [];
        if (tokens.Count == 0)
            return new PushResult { Success = true, Enviados = 0, Detalle = "sin destinatarios" };

        if (string.IsNullOrWhiteSpace(_opts.ProjectId))
            return new PushResult { Success = false, Detalle = "FCM no configurado (Push:Fcm:ProjectId)." };

        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        if (string.IsNullOrEmpty(accessToken))
            return new PushResult { Success = false, Detalle = "No se pudo autenticar con FCM." };

        var http = _httpFactory.CreateClient(HttpClientName);
        http.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        var url = $"{_opts.BaseUrl.TrimEnd('/')}/v1/projects/{_opts.ProjectId}/messages:send";

        var enviados = 0;
        var invalidos = new List<string>();

        foreach (var token in tokens)
        {
            try
            {
                var body = new
                {
                    message = new
                    {
                        token,
                        notification = new { title = message.Titulo, body = message.Cuerpo },
                        data = message.Data ?? new Dictionary<string, string>(),
                    },
                };

                using var resp = await http.PostAsJsonAsync(url, body, ct);
                if (resp.IsSuccessStatusCode) { enviados++; continue; }

                var error = await resp.Content.ReadAsStringAsync(ct);
                if (EsTokenInvalido(resp.StatusCode, error))
                {
                    invalidos.Add(token);
                    _logger.LogInformation("FCM: token inválido descartado ({Status}).", (int)resp.StatusCode);
                }
                else
                {
                    _logger.LogWarning("FCM send falló {Status}: {Body}", (int)resp.StatusCode, Truncar(error));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM: error enviando a un token.");
            }
        }

        return new PushResult
        {
            Success = enviados > 0 || invalidos.Count == tokens.Count,
            Enviados = enviados,
            InvalidTokens = invalidos,
            Detalle = $"FCM: {enviados} enviados, {invalidos.Count} inválidos de {tokens.Count}",
        };
    }

    /// <summary>Determina si la respuesta de FCM indica un token de registro inválido/no registrado.</summary>
    private static bool EsTokenInvalido(HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.NotFound) return true; // UNREGISTERED
        if (status != HttpStatusCode.BadRequest && status != HttpStatusCode.Forbidden) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("status", out var st))
            {
                var s = st.GetString();
                return s is "UNREGISTERED" or "INVALID_ARGUMENT" or "NOT_FOUND";
            }
        }
        catch (JsonException) { /* cuerpo no-JSON: tratamos como error genérico */ }
        return body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
            || body.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncar(string s) => s.Length <= 500 ? s : s[..500];
}
