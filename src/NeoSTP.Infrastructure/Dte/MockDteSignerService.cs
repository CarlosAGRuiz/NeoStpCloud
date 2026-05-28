using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Firmador "mock" para desarrollo sin certificado real.
///
/// Produce un JWS sintético con header { alg: "none-mock", typ: "JWT" }, el
/// payload codificado en base64url, y una "firma" determinística (SHA-256 hex
/// del payload). NO es válido para Hacienda — solo simula la forma del JWS
/// para poder ejercitar el flujo completo sin certificado.
/// </summary>
public class MockDteSignerService : IDteSignerService
{
    private readonly ILogger<MockDteSignerService> _logger;

    public MockDteSignerService(ILogger<MockDteSignerService> logger)
    {
        _logger = logger;
    }

    public Task<DteSignResult> FirmarAsync(string jsonDte, byte[]? certificadoBlob, string? certificadoPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(jsonDte))
        {
            return Task.FromResult(new DteSignResult
            {
                Success = false,
                Mensaje = "JSON_VACIO",
                Detalle = "El JSON DTE está vacío.",
            });
        }

        var header = """{"alg":"none-mock","typ":"JWT"}""";
        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(jsonDte));
        var signingInput = $"{headerB64}.{payloadB64}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(signingInput));
        var signatureB64 = Base64UrlEncode(digest);

        _logger.LogInformation("MockDteSignerService: firma mock generada (len={Len} bytes).", jsonDte.Length);

        return Task.FromResult(new DteSignResult
        {
            Success = true,
            JsonFirmado = $"{signingInput}.{signatureB64}",
            Mensaje = "OK",
            Detalle = "Firma mock (no válida para Hacienda).",
        });
    }

    internal static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
