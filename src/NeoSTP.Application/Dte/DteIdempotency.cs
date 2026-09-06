using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Dtos;

namespace NeoSTP.Application.Dte;

public static class DteIdempotency
{
    public static Result ValidateKey(string? key) => key is null || (key.Length is >= 1 and <= 128
        && key.All(c => c is >= '!' and <= '~'))
        ? Result.Ok()
        : Result.Fail("Idempotency-Key debe tener entre 1 y 128 caracteres ASCII visibles, sin espacios.", "IDEMPOTENCY_KEY_INVALID");

    public static string HashKey(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static string Fingerprint(CreateDteDocumentoRequest request)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, json.RootElement, root: true);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element, bool root = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (root && p.Name == nameof(CreateDteDocumentoRequest.IdempotencyKey)) continue;
                    writer.WritePropertyName(p.Name);
                    WriteCanonical(writer, p.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number when element.TryGetDecimal(out var number):
                writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
                break;
            default: element.WriteTo(writer); break;
        }
    }
}
