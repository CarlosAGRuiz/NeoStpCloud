using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Billing;

public sealed partial class WompiBillingProvider
{
    // Wompi SV: https://docs.wompi.sv/metodos-api/enlace-de-pago
    // No idempotency header is documented. The caller must persist the intent and
    // lease before this one attempt, and reconcile every uncertain result.
    public async Task<Result<ProviderCheckoutSession>> CreateCheckoutAsync(
        ProviderCheckoutRequest request, CancellationToken ct = default)
    {
        // H03 proves sandbox session creation only. Configuration cannot enable
        // real charges until verified payment/webhook/license processing is ready.
        if (_opts.IsProduction) return CheckoutFailure("WOMPI_PRODUCTION_NOT_READY");
        if (!IsOfficialOrigin(_opts.BaseUrl, "api.wompi.sv")
            || !IsOfficialOrigin(_opts.IdUrl, "id.wompi.sv")
            || !IsConfiguredValue(_opts.AppId) || !IsConfiguredValue(_opts.ApiSecret)
            || !TryHttpsUrl(_opts.CheckoutWebhookUrl, out var webhook)
            || !string.IsNullOrEmpty(webhook.Query))
            return CheckoutFailure("WOMPI_CHECKOUT_NOT_CONFIGURED");

        if (request.CorrelationId == Guid.Empty || request.EmpresaId <= 0
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || string.IsNullOrWhiteSpace(request.ProviderAccountId)
            || !IsConfiguredValue(request.BeneficiaryId)
            || !string.Equals(request.Currency, "USD", StringComparison.Ordinal)
            || request.Amount < 0.01m || request.Amount > 9999999999999999.99m
            || decimal.Round(request.Amount, 2) != request.Amount
            || request.BillingInterval != "MONTH"
            || string.IsNullOrWhiteSpace(request.PlanName) || request.PlanName.Length > 500
            || !TryHttpsUrl(request.SuccessUrl, out var success)
            || !TryHttpsUrl(request.CancelUrl, out var cancel)
            || !SameOrigin(success, cancel) || !SameOrigin(success, webhook))
            return CheckoutFailure("WOMPI_CHECKOUT_INVALID_REQUEST");

        try
        {
            // The named client must disable redirects and retries. Fixed URLs
            // prevent configuration from sending OAuth secrets to another origin.
            using var http = _httpFactory.CreateClient(HttpClientName);
            http.MaxResponseContentBufferSize = 262144;
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://id.wompi.sv/connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["audience"] = "wompi_api",
                    ["client_id"] = _opts.AppId,
                    ["client_secret"] = _opts.ApiSecret,
                }),
            };
            using var tokenResponse = await http.SendAsync(tokenRequest, ct);
            if (!tokenResponse.IsSuccessStatusCode) return CheckoutFailure("WOMPI_AUTH_FAILED");
            using var tokenDocument = await ReadCheckoutJsonAsync(tokenResponse, ct);
            var token = TryGetString(tokenDocument.RootElement, "access_token");
            if (string.IsNullOrWhiteSpace(token)) return CheckoutFailure("WOMPI_AUTH_FAILED");

            // Account = Wompi merchant IdCuenta. Beneficiary = authenticated application whose
            // payout configuration is held by Wompi. Verify both authenticated
            // identities before creating a link; never trust supplied snapshots.
            using var accountRequest = AuthenticatedGet("https://api.wompi.sv/Cuenta", token);
            using var accountResponse = await http.SendAsync(accountRequest, ct);
            if (!accountResponse.IsSuccessStatusCode) return CheckoutFailure("WOMPI_IDENTITY_UNVERIFIED");
            using var accountDocument = await ReadCheckoutJsonAsync(accountResponse, ct);
            if (!string.Equals(TryGetString(accountDocument.RootElement, "idCuenta"),
                request.ProviderAccountId, StringComparison.Ordinal))
                return CheckoutFailure("WOMPI_IDENTITY_MISMATCH");

            using var applicationRequest = AuthenticatedGet("https://api.wompi.sv/Aplicativo", token);
            using var applicationResponse = await http.SendAsync(applicationRequest, ct);
            if (!applicationResponse.IsSuccessStatusCode) return CheckoutFailure("WOMPI_IDENTITY_UNVERIFIED");
            using var applicationDocument = await ReadCheckoutJsonAsync(applicationResponse, ct);
            var application = applicationDocument.RootElement;
            if (!string.Equals(TryGetString(application, "idAplicativo"), request.BeneficiaryId, StringComparison.Ordinal)
                || !string.Equals(TryGetString(application, "clientIdApi"), _opts.AppId, StringComparison.Ordinal)
                || !HasExpectedMode(application, expected: false))
                return CheckoutFailure("WOMPI_IDENTITY_MISMATCH");

            using var checkoutRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.wompi.sv/EnlacePago")
            {
                Content = JsonContent.Create(new
                {
                    idAplicativo = request.BeneficiaryId,
                    identificadorEnlaceComercio = $"neostp:checkout:{request.CorrelationId:N}",
                    monto = request.Amount,
                    nombreProducto = request.PlanName,
                    formaPago = new
                    {
                        permitirTarjetaCreditoDebido = true,
                        permitirPagoConPuntoAgricola = false,
                        permitirPagoEnCuotasAgricola = false,
                        permitirPagoEnBitcoin = false,
                        permitePagoQuickPay = false,
                        permitePagoNequi = false,
                    },
                    configuracion = new
                    {
                        urlRedirect = success.AbsoluteUri,
                        urlRetorno = cancel.AbsoluteUri,
                        urlWebhook = webhook.AbsoluteUri,
                        esMontoEditable = false,
                        esCantidadEditable = false,
                        cantidadPorDefecto = 1,
                        notificarTransaccionCliente = false,
                    },
                    limitesDeUso = new { cantidadMaximaPagosExitosos = 1 },
                }),
            };
            checkoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var checkoutResponse = await http.SendAsync(checkoutRequest, ct);
            if (!checkoutResponse.IsSuccessStatusCode) return CheckoutFailure("WOMPI_CHECKOUT_UNCONFIRMED");
            using var checkoutDocument = await ReadCheckoutJsonAsync(checkoutResponse, ct);
            var checkout = checkoutDocument.RootElement;
            var redirectUrl = TryGetString(checkout, "urlEnlace");
            if (checkout.ValueKind != JsonValueKind.Object
                || !checkout.TryGetProperty("idEnlace", out var idElement)
                || !idElement.TryGetInt32(out var id) || id <= 0
                || !HasExpectedMode(checkout, expected: false)
                || !TryHttpsUrl(redirectUrl, out var redirect)
                || !(redirect.Host.Equals("wompi.sv", StringComparison.OrdinalIgnoreCase)
                    || redirect.Host.EndsWith(".wompi.sv", StringComparison.OrdinalIgnoreCase)))
                return CheckoutFailure("WOMPI_CHECKOUT_UNCONFIRMED");

            return Result<ProviderCheckoutSession>.Ok(new(ProviderName, request.ProviderAccountId,
                id.ToString(CultureInfo.InvariantCulture), redirect.AbsoluteUri));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller retains the durable lease; recovery cannot resend blindly.
            throw;
        }
        catch (Exception)
        {
            // Never persist provider bodies, credentials or exception messages.
            return CheckoutFailure("WOMPI_CHECKOUT_UNCONFIRMED");
        }
    }

    private static Result<ProviderCheckoutSession> CheckoutFailure(string code)
        => Result<ProviderCheckoutSession>.Fail(
            "Wompi no confirmó un checkout válido; revise su configuración o concilie la operación.", code);

    private static HttpRequestMessage AuthenticatedGet(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<JsonDocument> ReadCheckoutJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (content.Length > 262144) throw new JsonException("Provider response exceeds the expected limit.");
        return JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 32 });
    }

    private static bool HasExpectedMode(JsonElement element, bool expected)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("estaProductivo", out var mode)
            && mode.ValueKind is JsonValueKind.True or JsonValueKind.False && mode.GetBoolean() == expected;

    private static bool IsConfiguredValue(string value)
        => !string.IsNullOrWhiteSpace(value) && value == value.Trim()
            && !value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);

    private static bool IsOfficialOrigin(string value, string host)
        => TryHttpsUrl(value, out var uri) && uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query);

    private static bool TryHttpsUrl(string? value, out Uri uri)
    {
        uri = null!;
        return !string.IsNullOrWhiteSpace(value) && value == value.Trim()
            && Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443
            && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool SameOrigin(Uri first, Uri second)
        => first.Scheme == second.Scheme && first.Port == second.Port
            && first.Host.Equals(second.Host, StringComparison.OrdinalIgnoreCase);
}