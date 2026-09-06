using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

public interface IBillingCalendarService
{
    Task<Result> ApplyVerifiedPaymentAsync(int empresaId, int periodId, decimal amount, string currency,
        string reference, DateTime paidAtUtc, CancellationToken ct = default);
}
