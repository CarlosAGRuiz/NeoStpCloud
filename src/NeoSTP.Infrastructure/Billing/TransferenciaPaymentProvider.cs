using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// "Proveedor" de pago por transferencia bancaria (offline). No integra una pasarela:
/// el flujo real (crear pago pendiente, subir comprobante, confirmar/rechazar) vive en
/// <see cref="BillingService"/>. Esta clase existe para que el resolver exponga el método
/// "Transferencia" y para que el checkout redirija a la página de instrucciones interna.
/// </summary>
public sealed class TransferenciaPaymentProvider : IPaymentProvider
{
    public const string Metodo = "Transferencia";

    public string ProviderName => Metodo;

    public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Ok($"transfer_cus_{empresaId}"));

    public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
        // El checkout de transferencia se maneja con IniciarTransferenciaAsync; aquí solo
        // devolvemos una redirección a la página de instrucciones interna.
        => Task.FromResult(Result<CheckoutSessionResult>.Ok(new CheckoutSessionResult("transferencia", "/Billing/Transferencia")));

    public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default)
        => Task.FromResult(Result<BillingPortalResult>.Ok(new BillingPortalResult(returnUrl)));

    public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Ok(externalSubscriptionId));

    public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
        => Task.FromResult(Result.Ok());
}
