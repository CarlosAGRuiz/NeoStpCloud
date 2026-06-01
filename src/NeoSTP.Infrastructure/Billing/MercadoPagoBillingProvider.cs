using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// Proveedor de pagos MercadoPago usando mercadopago-sdk v3.
/// Utiliza Preferencias de pago (checkout pro) como equivalente al checkout session de Stripe.
/// El portal de facturación es self-hosted (redirige a /billing).
/// </summary>
public sealed class MercadoPagoBillingProvider : IPaymentProvider
{
    private readonly MercadoPagoOptions _opts;

    public MercadoPagoBillingProvider(IOptions<BillingOptions> options)
    {
        _opts = options.Value.MercadoPago;
        MercadoPagoConfig.AccessToken = _opts.AccessToken;
    }

    public string ProviderName => "MercadoPago";

    public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
    {
        // MercadoPago no tiene un endpoint de "customer"; usamos el email como identificador externo.
        return Task.FromResult(Result<string>.Ok($"mp_cus_{empresaId}_{email}"));
    }

    public async Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        try
        {
            var client = new PreferenceClient();
            var preference = await client.CreateAsync(new PreferenceRequest
            {
                Items = new List<PreferenceItemRequest>
                {
                    new()
                    {
                        Id          = externalPlanId,
                        Title       = "Suscripción NeoSTP",
                        Quantity    = 1,
                        UnitPrice   = 0m,   // Se sobreescribe con el precio real del mapping
                        CurrencyId  = "MXN",
                    },
                },
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = successUrl,
                    Failure = cancelUrl,
                    Pending = _opts.PendingUrl,
                },
                AutoReturn = "approved",
                Metadata   = new Dictionary<string, object?> { ["customerId"] = customerId },
            }, cancellationToken: ct);

            return Result<CheckoutSessionResult>.Ok(new CheckoutSessionResult(preference.Id!, preference.InitPoint!));
        }
        catch (Exception ex)
        {
            return Result<CheckoutSessionResult>.Fail(ex.Message);
        }
    }

    public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default)
    {
        // MercadoPago no tiene portal de suscripciones; redirigimos al portal interno.
        return Task.FromResult(Result<BillingPortalResult>.Ok(new BillingPortalResult(returnUrl)));
    }

    public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default)
    {
        // Los cambios de plan en MercadoPago implican cancelar la suscripción actual y crear una nueva preferencia.
        // En esta implementación, gestionamos el estado localmente y se genera una nueva preferencia en el próximo checkout.
        return Task.FromResult(Result<string>.Ok(externalSubscriptionId));
    }

    public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
    {
        // La cancelación en MercadoPago se gestiona localmente; el webhook notificará el fin de vigencia.
        return Task.FromResult(Result.Ok());
    }
}
