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

    // ── Transferencia bancaria (verificación manual) ──
    Task<Result<TransferenciaInstruccionesDto>> IniciarTransferenciaAsync(IniciarTransferenciaRequest request, CancellationToken ct = default);
    Task<Result> RegistrarComprobanteAsync(int empresaId, int paymentId, string comprobanteUrl, CancellationToken ct = default);
    Task<Result> ConfirmarTransferenciaAsync(int paymentId, string actor, CancellationToken ct = default);
    Task<Result> RechazarTransferenciaAsync(int paymentId, string motivo, string actor, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TransferenciaPendienteDto>>> GetTransferenciasPendientesAsync(int? empresaId, CancellationToken ct = default);
}
