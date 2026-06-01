using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

public interface IBillingService
{
    Task<Result<BillingSubscriptionDto>> StartTrialAsync(StartTrialRequest request, CancellationToken ct = default);
    Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default);
    Task<Result<BillingPortalResult>> GetPortalUrlAsync(int empresaId, CancellationToken ct = default);
    Task<Result> ChangePlanAsync(ChangePlanRequest request, CancellationToken ct = default);
    Task<Result> CancelSubscriptionAsync(CancelSubscriptionRequest request, CancellationToken ct = default);
    Task<Result<BillingSubscriptionDto?>> GetActiveSubscriptionAsync(int empresaId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<BillingPaymentDto>>> GetPaymentsAsync(int empresaId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<BillingInvoiceDto>>> GetInvoicesAsync(int empresaId, CancellationToken ct = default);
}
