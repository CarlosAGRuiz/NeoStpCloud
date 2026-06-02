using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// Proveedor de pagos Wompi El Salvador (wompi.sv) vía API REST.
///
/// Flujo: OAuth2 client-credentials contra el servidor de identidad → crear un
/// "EnlacePago" (link de pago hospedado) → el cliente paga en la página de Wompi
/// (los datos de tarjeta nunca tocan nuestros servidores). El webhook de Wompi
/// notifica la transacción aprobada/rechazada (ver BillingWebhookHandler).
///
/// No maneja "customer" ni "subscription" nativos: se gestionan localmente, como
/// en MercadoPago. Los nombres de campos siguen la API v1 de Wompi.sv.
/// </summary>
public sealed class WompiBillingProvider : IPaymentProvider
{
    public const string HttpClientName = "WompiClient";

    private readonly IHttpClientFactory _httpFactory;
    private readonly WompiOptions _opts;
    private readonly ILogger<WompiBillingProvider> _logger;

    public WompiBillingProvider(IHttpClientFactory httpFactory, IOptions<BillingOptions> options, ILogger<WompiBillingProvider> logger)
    {
        _httpFactory = httpFactory;
        _opts = options.Value.Wompi;
        _logger = logger;
    }

    public string ProviderName => "Wompi";

    public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Ok($"wompi_cus_{empresaId}"));

    public async Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        try
        {
            var token = await ObtenerTokenAsync(ct);
            if (token is null)
                return Result<CheckoutSessionResult>.Fail("No se pudo autenticar con Wompi.", "WOMPI_AUTH_FAILED");

            var http = _httpFactory.CreateClient(HttpClientName);
            http.DefaultRequestHeaders.Authorization = new("Bearer", token);

            var idEnlace = $"neostp-{customerId}-{Guid.NewGuid():N}";
            var body = new
            {
                identificadorEnlaceComercio = idEnlace,
                monto = 0m, // el monto real se fija desde el mapping de plan en una iteración posterior
                nombreProducto = "Suscripción NeoSTP",
                formaPago = new { permitirTarjetaCreditoDebido = true, permitirPagoConPuntoAgricola = true },
                configuracion = new { urlRedirect = successUrl, esMontoEditable = false, urlWebhook = (string?)null },
            };

            using var resp = await http.PostAsJsonAsync($"{_opts.BaseUrl}/EnlacePago", body, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Wompi EnlacePago falló {Status}: {Body}", (int)resp.StatusCode, json);
                return Result<CheckoutSessionResult>.Fail($"Wompi rechazó la creación del enlace ({(int)resp.StatusCode}).", "WOMPI_LINK_FAILED");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var url = TryGetString(root, "urlEnlace") ?? TryGetString(root, "urlQrCodeEmv") ?? successUrl;
            var id = TryGetString(root, "idEnlace") ?? idEnlace;

            return Result<CheckoutSessionResult>.Ok(new CheckoutSessionResult(id, url));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando enlace de pago Wompi");
            return Result<CheckoutSessionResult>.Fail(ex.Message, "WOMPI_LINK_FAILED");
        }
    }

    // Wompi no tiene portal ni suscripciones nativas: se gestionan localmente.
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
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _opts.AppId,
            ["client_secret"] = _opts.ApiSecret,
            ["audience"] = "wompi_api",
        });

        using var resp = await http.PostAsync($"{_opts.IdUrl}/connect/token", form, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return TryGetString(doc.RootElement, "access_token");
    }

    private static string? TryGetString(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
