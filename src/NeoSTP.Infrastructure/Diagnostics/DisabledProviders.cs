using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Scan;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>Estado explícito para capacidades opcionales no habilitadas en un deployment.</summary>
public sealed class DisabledEmailSender : IEmailSender
{
    public Task<EmailSendResult> EnviarAsync(EmailMessage message, CancellationToken ct = default)
        => Task.FromResult(new EmailSendResult
        {
            Success = false,
            Mensaje = "PROVIDER_DISABLED",
            Detalle = "El proveedor global de correo está deshabilitado."
        });
}

public sealed class DisabledScanExtractionService : IScanExtractionService
{
    public Task<ScanExtraccion> ExtraerAsync(byte[] contenido, string contentType, CancellationToken ct = default)
        => Task.FromResult(new ScanExtraccion
        {
            Confianza = 0m,
            OcrProveedor = "Disabled",
            OcrErrorResumen = "PROVIDER_DISABLED",
            OcrIntentoAt = DateTime.UtcNow
        });
}

public sealed class DisabledPushSender : IPushSender
{
    public Task<PushResult> EnviarAsync(PushMessage message, CancellationToken ct = default)
        => Task.FromResult(new PushResult
        {
            Success = false,
            Enviados = 0,
            Detalle = "PROVIDER_DISABLED"
        });
}

public sealed class DisabledWhatsAppSender : IWhatsAppSender
{
    public Task<WhatsAppSendResult> EnviarAsync(WhatsAppMessage message, CancellationToken ct = default)
        => Task.FromResult(new WhatsAppSendResult
        {
            Success = false,
            Error = "PROVIDER_DISABLED"
        });
}

public sealed class DisabledPaymentProvider : IPaymentProvider
{
    public string ProviderName => "Disabled";

    public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Fail("Billing está deshabilitado.", "PROVIDER_DISABLED"));

    public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId,
        string externalPlanId,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
        => Task.FromResult(Result<CheckoutSessionResult>.Fail("Billing está deshabilitado.", "PROVIDER_DISABLED"));

    public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken ct = default)
        => Task.FromResult(Result<BillingPortalResult>.Fail("Billing está deshabilitado.", "PROVIDER_DISABLED"));

    public Task<Result<string>> ChangePlanAsync(
        string externalSubscriptionId,
        string newExternalPlanId,
        CancellationToken ct = default)
        => Task.FromResult(Result<string>.Fail("Billing está deshabilitado.", "PROVIDER_DISABLED"));

    public Task<Result> CancelSubscriptionAsync(
        string externalSubscriptionId,
        bool atPeriodEnd,
        CancellationToken ct = default)
        => Task.FromResult(Result.Fail("Billing está deshabilitado.", "PROVIDER_DISABLED"));
}
