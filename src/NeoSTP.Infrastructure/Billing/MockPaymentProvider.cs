using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// Proveedor de pagos en memoria para desarrollo/testing.
/// Simula respuestas exitosas sin llamadas externas.
/// </summary>
public sealed class MockPaymentProvider : IPaymentProvider
{
    public string ProviderName => "Mock";

    public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Ok($"mock_cus_{empresaId}"));

    public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var sessionId = $"mock_cs_{Guid.NewGuid():N}";
        return Task.FromResult(Result<CheckoutSessionResult>.Ok(new CheckoutSessionResult(sessionId, successUrl)));
    }

    public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default)
        => Task.FromResult(Result<BillingPortalResult>.Ok(new BillingPortalResult(returnUrl)));

    public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Ok(externalSubscriptionId));

    public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
        => Task.FromResult(Result.Ok());
}
