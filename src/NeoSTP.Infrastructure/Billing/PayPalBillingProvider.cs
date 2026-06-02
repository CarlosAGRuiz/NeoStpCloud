using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// Proveedor de pagos PayPal (REST Orders v2).
///
/// Flujo: OAuth2 client-credentials (Basic clientId:secret) → crear una Order
/// (intent CAPTURE) con return/cancel URL → el cliente aprueba en PayPal (checkout
/// hospedado) → el webhook PAYMENT.CAPTURE.COMPLETED confirma el pago. Suscripción
/// y portal se gestionan localmente.
/// </summary>
public sealed class PayPalBillingProvider : IPaymentProvider
{
    public const string HttpClientName = "PayPalClient";

    private readonly IHttpClientFactory _httpFactory;
    private readonly PayPalOptions _opts;
    private readonly ILogger<PayPalBillingProvider> _logger;

    public PayPalBillingProvider(IHttpClientFactory httpFactory, IOptions<BillingOptions> options, ILogger<PayPalBillingProvider> logger)
    {
        _httpFactory = httpFactory;
        _opts = options.Value.PayPal;
        _logger = logger;
    }

    public string ProviderName => "PayPal";

    public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Ok($"paypal_cus_{empresaId}"));

    public async Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        try
        {
            var token = await ObtenerTokenAsync(ct);
            if (token is null)
                return Result<CheckoutSessionResult>.Fail("No se pudo autenticar con PayPal.", "PAYPAL_AUTH_FAILED");

            var http = _httpFactory.CreateClient(HttpClientName);
            http.DefaultRequestHeaders.Authorization = new("Bearer", token);

            var body = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        custom_id = customerId,
                        description = "Suscripción NeoSTP",
                        amount = new { currency_code = "USD", value = "0.00" }, // monto real desde mapping en iteración posterior
                    },
                },
                application_context = new
                {
                    return_url = successUrl,
                    cancel_url = cancelUrl,
                    brand_name = "NeoSTP Cloud",
                    user_action = "PAY_NOW",
                },
            };

            using var resp = await http.PostAsJsonAsync($"{_opts.BaseUrl}/v2/checkout/orders", body, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal create order falló {Status}: {Body}", (int)resp.StatusCode, json);
                return Result<CheckoutSessionResult>.Fail($"PayPal rechazó la orden ({(int)resp.StatusCode}).", "PAYPAL_ORDER_FAILED");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var approve = ExtractApproveLink(root) ?? successUrl;

            return Result<CheckoutSessionResult>.Ok(new CheckoutSessionResult(id ?? string.Empty, approve));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando orden PayPal");
            return Result<CheckoutSessionResult>.Fail(ex.Message, "PAYPAL_ORDER_FAILED");
        }
    }

    public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default)
        => Task.FromResult(Result<BillingPortalResult>.Ok(new BillingPortalResult(returnUrl)));

    public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Ok(externalSubscriptionId));

    public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
        => Task.FromResult(Result.Ok());

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string?> ObtenerTokenAsync(CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(HttpClientName);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.Secret}"));

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("access_token", out var v) ? v.GetString() : null;
    }

    private static string? ExtractApproveLink(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var link in links.EnumerateArray())
        {
            if (link.TryGetProperty("rel", out var rel) && rel.GetString() is "approve" or "payer-action"
                && link.TryGetProperty("href", out var href))
                return href.GetString();
        }
        return null;
    }
}
