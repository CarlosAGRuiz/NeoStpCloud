using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

/// <summary>Internal consumer of durable captured evidence; never accepts payment facts from a caller.</summary>
public interface IBillingPaymentApplicationProcessor
{
    /// <remarks>Resolve in its own dependency-injection scope; the processor owns the DbContext unit of work.</remarks>
    Task<Result> ApplyVerifiedPaymentAsync(Guid receiptId, CancellationToken ct = default);
}
