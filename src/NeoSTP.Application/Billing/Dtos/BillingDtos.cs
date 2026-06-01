namespace NeoSTP.Application.Billing.Dtos;

public record BillingCustomerDto(
    int Id,
    int EmpresaId,
    string Email,
    string Provider,
    string? ExternalCustomerId
);

public record BillingSubscriptionDto(
    int Id,
    int BillingCustomerId,
    int PlanId,
    string PlanNombre,
    string Status,
    DateTime TrialEnd,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd
);

public record BillingPaymentDto(
    int Id,
    decimal Amount,
    string Currency,
    string Status,
    DateTime PaidAt,
    string? ReceiptUrl
);

public record BillingInvoiceDto(
    int Id,
    decimal Amount,
    string Currency,
    string Status,
    DateTime InvoiceDate,
    DateTime? DueDate,
    string? PdfUrl
);

public record StartTrialRequest(int EmpresaId, int PlanId, string Email);
public record CreateCheckoutRequest(int EmpresaId, int PlanId, string ReturnUrl);
public record ChangePlanRequest(int EmpresaId, int NewPlanId);
public record CancelSubscriptionRequest(int EmpresaId, bool AtPeriodEnd = true);

public record CheckoutSessionResult(string SessionId, string RedirectUrl);
public record BillingPortalResult(string PortalUrl);
