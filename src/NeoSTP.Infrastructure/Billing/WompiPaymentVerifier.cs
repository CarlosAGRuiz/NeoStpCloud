using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// Wompi SV authenticated account/application/link/transaction evidence. Contract checked against
/// https://api.wompi.sv/swagger/v1/swagger.json. The only POST obtains an OAuth token; all business
/// operations are GETs. Named-client resilience owns read retries and must never retry OAuth POST.
/// </summary>
public sealed class WompiPaymentVerifier(IHttpClientFactory httpFactory, IOptions<BillingOptions> options)
    : IWompiPaymentVerifier
{
    private readonly WompiOptions _options = options.Value.Wompi;

    public async Task<Result<WompiVerifiedPayment>> VerifyAsync(WompiPaymentVerificationRequest request, CancellationToken ct = default)
    {
        // This increment verifies sandbox evidence only. A caller cannot opt into production.
        if (_options.IsProduction || request.IsProduction) return Failure("WOMPI_PRODUCTION_NOT_READY");
        if (!OfficialOrigin(_options.BaseUrl, "api.wompi.sv") || !OfficialOrigin(_options.IdUrl, "id.wompi.sv")
            || !ConfiguredValue(_options.AppId, 200) || !ConfiguredValue(_options.ApiSecret, 4096))
            return Failure("WOMPI_VERIFICATION_NOT_CONFIGURED");
        if (request.TransactionId == Guid.Empty || request.CorrelationId == Guid.Empty
            || !int.TryParse(request.ExternalCheckoutId, NumberStyles.None, CultureInfo.InvariantCulture, out var linkId) || linkId <= 0
            || !ConfiguredValue(request.ProviderAccountId, 200) || !ConfiguredValue(request.BeneficiaryId, 200)
            || request.Currency != "USD" || request.Amount <= 0 || request.Amount > 9999999999999999.99m
            || decimal.Round(request.Amount, 2) != request.Amount)
            return Failure("WOMPI_VERIFICATION_INVALID_REQUEST");

        try
        {
            using var http = httpFactory.CreateClient(WompiBillingProvider.HttpClientName);
            http.MaxResponseContentBufferSize = 262144;
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://id.wompi.sv/connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials", ["audience"] = "wompi_api",
                    ["client_id"] = _options.AppId, ["client_secret"] = _options.ApiSecret,
                }),
            };
            using var tokenResponse = await http.SendAsync(tokenRequest, ct);
            if (!tokenResponse.IsSuccessStatusCode) return Failure("WOMPI_VERIFICATION_UNAVAILABLE");
            using var tokenJson = await ReadJsonAsync(tokenResponse, ct);
            var token = Text(tokenJson.RootElement, "access_token");
            if (string.IsNullOrWhiteSpace(token)) return Failure("WOMPI_VERIFICATION_UNAVAILABLE");

            using var account = await ReadResourceAsync(http, "https://api.wompi.sv/Cuenta", token, ct);
            if (account is null) return Failure("WOMPI_VERIFICATION_UNAVAILABLE");
            if (Text(account.RootElement, "idCuenta") != request.ProviderAccountId)
                return Failure("WOMPI_PAYMENT_IDENTITY_MISMATCH");

            using var application = await ReadResourceAsync(http, "https://api.wompi.sv/Aplicativo", token, ct);
            if (application is null) return Failure("WOMPI_VERIFICATION_UNAVAILABLE");
            if (Text(application.RootElement, "idAplicativo") != request.BeneficiaryId
                || Text(application.RootElement, "clientIdApi") != _options.AppId
                || !BooleanEquals(application.RootElement, "estaProductivo", request.IsProduction))
                return Failure("WOMPI_PAYMENT_IDENTITY_MISMATCH");

            using var link = await ReadResourceAsync(http,
                $"https://api.wompi.sv/EnlacePago/{linkId.ToString(CultureInfo.InvariantCulture)}", token, ct);
            if (link is null) return Failure("WOMPI_VERIFICATION_UNAVAILABLE");
            var linkRoot = link.RootElement;
            if (!Property(linkRoot, "idEnlace", out var remoteLink) || remoteLink.ValueKind != JsonValueKind.Number
                || !remoteLink.TryGetInt32(out var remoteLinkId) || remoteLinkId != linkId
                || Text(linkRoot, "idAplicativo") != request.BeneficiaryId
                || Text(linkRoot, "nombreEnlace") != $"neostp:checkout:{request.CorrelationId:N}"
                || !BooleanEquals(linkRoot, "estaProductivo", request.IsProduction)
                || !AmountEquals(linkRoot, request.Amount)
                || !Property(linkRoot, "transacciones", out var transactions) || transactions.ValueKind != JsonValueKind.Array)
                return Failure("WOMPI_PAYMENT_NOT_VERIFIED");

            // A transaction ID alone is insufficient: it must belong to the authenticated link.
            // Do not substitute idExterno or the optional legacy transaccionCompra object.
            var matching = transactions.EnumerateArray().Where(t => TransactionMatches(t, request.TransactionId)).ToArray();
            if (matching.Length != 1 || !ApprovedPaymentMatches(matching[0], request))
                return Failure("WOMPI_PAYMENT_NOT_VERIFIED");

            using var transaction = await ReadResourceAsync(http,
                $"https://api.wompi.sv/TransaccionCompra/{request.TransactionId:D}", token, ct);
            if (transaction is null) return Failure("WOMPI_VERIFICATION_UNAVAILABLE");
            if (!ApprovedPaymentMatches(transaction.RootElement, request)
                || !PaidAt(transaction.RootElement, out var paidAt))
                return Failure("WOMPI_PAYMENT_NOT_VERIFIED");

            return Result<WompiVerifiedPayment>.Ok(new(request.TransactionId, paidAt));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Never return provider bodies, credentials, token or exception messages.
            return Failure("WOMPI_VERIFICATION_UNAVAILABLE");
        }
    }

    private static bool ApprovedPaymentMatches(JsonElement transaction, WompiPaymentVerificationRequest request)
        => TransactionMatches(transaction, request.TransactionId)
            && BooleanEquals(transaction, "esReal", request.IsProduction)
            && BooleanEquals(transaction, "esAprobada", true) && AmountEquals(transaction, request.Amount);

    private static bool TransactionMatches(JsonElement value, Guid id)
        => Guid.TryParse(Text(value, "idTransaccion"), out var actual) && actual == id;

    private static bool AmountEquals(JsonElement value, decimal expected)
        => Property(value, "monto", out var amount) && amount.ValueKind == JsonValueKind.Number
            && amount.TryGetDecimal(out var actual) && actual == expected;

    private static bool BooleanEquals(JsonElement value, string name, bool expected)
        => Property(value, name, out var actual) && actual.ValueKind is JsonValueKind.True or JsonValueKind.False
            && actual.GetBoolean() == expected;

    private static bool PaidAt(JsonElement value, out DateTimeOffset paidAt)
    {
        paidAt = default;
        if (!Property(value, "fechaTransaccion", out var date) || date.ValueKind != JsonValueKind.String) return false;
        var text = date.GetString();
        // A date-time without an offset would otherwise use the server's local timezone.
        // Keep ambiguous dates unverified until the provider's timezone is established.
        if (text is null || !(text.EndsWith('Z') || text.Length >= 6
            && (text[^6] is '+' or '-') && text[^3] == ':')) return false;
        return date.TryGetDateTimeOffset(out paidAt) && paidAt != default;
    }

    private static bool Property(JsonElement value, string name, out JsonElement property)
    {
        property = default;
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out property);
    }

    private static string? Text(JsonElement value, string name)
        => Property(value, name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static async Task<JsonDocument?> ReadResourceAsync(HttpClient http, string url, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? await ReadJsonAsync(response, ct) : null;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct), new JsonDocumentOptions { MaxDepth = 32 });

    private static bool ConfiguredValue(string? value, int maxLength)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && value == value.Trim() && !value.Any(char.IsControl)
            && !value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase) && !value.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);

    private static bool OfficialOrigin(string? value, string host)
        => value is not null && value == value.Trim() && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 && uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    private static Result<WompiVerifiedPayment> Failure(string code)
        => Result<WompiVerifiedPayment>.Fail("Wompi no confirmó evidencia de pago coincidente; la operación requiere verificación o conciliación.", code);
}
