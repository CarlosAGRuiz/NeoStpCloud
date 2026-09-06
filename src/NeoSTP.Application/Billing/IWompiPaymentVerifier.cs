using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

/// <summary>Read-only remote evidence verification; never applies a payment or grants a license.</summary>
public interface IWompiPaymentVerifier
{
    Task<Result<WompiVerifiedPayment>> VerifyAsync(WompiPaymentVerificationRequest request, CancellationToken ct = default);
}

/// <summary>Expected identities and commercial values must come from the durable checkout snapshot.</summary>
public sealed record WompiPaymentVerificationRequest(Guid TransactionId, string ExternalCheckoutId,
    Guid CorrelationId, string ProviderAccountId, string BeneficiaryId, decimal Amount, string Currency,
    bool IsProduction);

public sealed record WompiVerifiedPayment(Guid TransactionId, DateTimeOffset PaidAt);
