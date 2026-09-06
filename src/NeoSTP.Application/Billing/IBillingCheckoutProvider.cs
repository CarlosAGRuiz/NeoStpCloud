using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

/// <summary>Opt-in capability. Never adapt implicitly to the legacy amount-less API.</summary>
public interface IBillingCheckoutProvider
{
    string ProviderName { get; }
    Task<Result<ProviderCheckoutSession>> CreateCheckoutAsync(ProviderCheckoutRequest request, CancellationToken ct = default);
}

public sealed record ProviderCheckoutRequest(Guid CorrelationId, string IdempotencyKey,
    int EmpresaId, string ProviderAccountId, string BeneficiaryId, string ExternalPlanId,
    string? ExternalCustomerId, string PlanName, decimal Amount, string Currency,
    string BillingInterval, string SuccessUrl, string CancelUrl);

// Session creation is not evidence of capture. No Paid/LicenseActive flag is accepted here.
public sealed record ProviderCheckoutSession(string Provider, string ProviderAccountId,
    string SessionId, string RedirectUrl, DateTime? ExpiresAt = null);
