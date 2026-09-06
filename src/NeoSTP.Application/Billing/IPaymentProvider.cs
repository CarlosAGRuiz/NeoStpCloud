using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

/// <summary>
/// Abstracción de bajo nivel sobre el proveedor de pagos externo.
/// </summary>
public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default);
    Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default);
    Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default);
    Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default);
    Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default);

    /// <summary>
    /// Cancela usando una clave durable cuando el proveedor admite idempotencia. La
    /// implementación por defecto conserva compatibilidad con proveedores locales o
    /// adaptadores que todavía no exponen esa capacidad.
    /// </summary>
    Task<Result> CancelSubscriptionAsync(
        string externalSubscriptionId,
        bool atPeriodEnd,
        string idempotencyKey,
        CancellationToken ct = default)
        => CancelSubscriptionAsync(externalSubscriptionId, atPeriodEnd, ct);
}
