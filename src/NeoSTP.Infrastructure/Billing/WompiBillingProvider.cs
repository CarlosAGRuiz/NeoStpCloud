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
/// notifica las transacciones exitosas.
///
/// Este adaptador usa enlaces de un pago y períodos locales; los cargos recurrentes
/// de Wompi pertenecen a otro contrato. Campos según la API v1 de Wompi.sv.
/// </summary>
public sealed partial class WompiBillingProvider : IPaymentProvider, IBillingCheckoutProvider
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

    public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
        => Task.FromResult(Result<CheckoutSessionResult>.Fail(
            "Wompi requiere un checkout durable con importe y correlación verificados.", "BILLING_CHECKOUT_CAPABILITY_REQUIRED"));

    // Este adaptador no implementa el producto separado de cargos recurrentes de Wompi.
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
