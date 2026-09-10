namespace NeoSTP.Application.Billing.Dtos;

public sealed record BillingCheckoutDto(Guid CorrelationId, int EmpresaId, int PlanId,
    string Provider, decimal Amount, string Currency, string Status, string? SessionId,
    string? RedirectUrl, DateTime? ExpiresAt, string? ErrorCode);
