using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NeoSTP.Infrastructure.Notificaciones;

/// <summary>Obtiene un access token de Google para FCM (scope firebase.messaging).</summary>
public interface IFcmAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}

/// <summary>
/// Implementación OAuth2 "service account" (JWT bearer): firma un JWT RS256 con la clave
/// privada del service account y lo canjea por un access token en el endpoint de Google.
/// Cachea el token hasta poco antes de expirar. Secretos en <c>Push:Fcm</c>.
/// </summary>
public sealed class ServiceAccountTokenProvider : IFcmAccessTokenProvider
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly IHttpClientFactory _httpFactory;
    private readonly FcmOptions _opts;
    private readonly ILogger<ServiceAccountTokenProvider> _logger;

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ServiceAccountTokenProvider(
        IHttpClientFactory httpFactory,
        IOptions<FcmOptions> options,
        ILogger<ServiceAccountTokenProvider> logger)
    {
        _httpFactory = httpFactory;
        _opts = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _cachedToken;

        if (string.IsNullOrWhiteSpace(_opts.ClientEmail) || string.IsNullOrWhiteSpace(_opts.PrivateKey))
        {
            _logger.LogWarning("FCM: faltan credenciales del service account (Push:Fcm:ClientEmail/PrivateKey).");
            return null;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _cachedToken;

            var jwt = ConstruirJwt();
            var http = _httpFactory.CreateClient(FcmPushSender.HttpClientName);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = jwt,
            });

            using var resp = await http.PostAsync(_opts.TokenUri, content, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("FCM token falló {Status}: {Body}", (int)resp.StatusCode, json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var token = root.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            var expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var s) ? s : 3600;
            if (string.IsNullOrEmpty(token)) return null;

            _cachedToken = token;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60); // margen de 60s
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM: error obteniendo access token.");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ConstruirJwt()
    {
        var now = DateTimeOffset.UtcNow;
        var header = new { alg = "RS256", typ = "JWT" };
        var claims = new
        {
            iss = _opts.ClientEmail,
            scope = Scope,
            aud = _opts.TokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddMinutes(60).ToUnixTimeSeconds(),
        };

        var encHeader = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var encClaims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        var unsigned = $"{encHeader}.{encClaims}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(NormalizarPem(_opts.PrivateKey));
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64Url(signature)}";
    }

    /// <summary>La private_key del JSON suele traer "\n" escapados; los convertimos a saltos reales.</summary>
    private static string NormalizarPem(string pem) => pem.Replace("\\n", "\n").Trim();

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
