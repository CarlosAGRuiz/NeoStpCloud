using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Persistence;
using System.Text.Json;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// Procesa webhooks de Stripe y MercadoPago con idempotencia garantizada
/// mediante la tabla Billing_WebhookEvents.
/// </summary>
public sealed class BillingWebhookHandler : IBillingWebhookHandler
{
    private readonly NeoStpDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<BillingWebhookHandler> _logger;

    public BillingWebhookHandler(NeoStpDbContext db, IEmailSender email, ILogger<BillingWebhookHandler> logger)
    {
        _db    = db;
        _email = email;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        string provider, string eventType, string externalEventId, string rawPayload, CancellationToken ct = default)
    {
        // ── Idempotencia ────────────────────────────────────────────────────
        var existing = await _db.BillingWebhookEvents
            .FirstOrDefaultAsync(e => e.Provider == provider && e.ExternalEventId == externalEventId, ct);

        if (existing?.Processed == true)
        {
            _logger.LogInformation("Webhook {Provider}/{EventId} ya procesado, se omite.", provider, externalEventId);
            return Result.Ok();
        }

        var webhookEvent = existing ?? new BillingWebhookEvent
        {
            Provider        = provider,
            EventType       = eventType,
            ExternalEventId = externalEventId,
            RawPayload      = rawPayload,
            ReceivedAt      = DateTime.UtcNow,
        };

        if (existing is null) _db.BillingWebhookEvents.Add(webhookEvent);

        try
        {
            await DispatchAsync(provider, eventType, rawPayload, ct);

            webhookEvent.Processed   = true;
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            webhookEvent.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 1000)];
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Error procesando webhook {Provider}/{EventType}/{EventId}", provider, eventType, externalEventId);
            return Result.Fail(ex.Message);
        }
    }

    // ─── Dispatch por proveedor ────────────────────────────────────────────

    private Task DispatchAsync(string provider, string eventType, string rawPayload, CancellationToken ct)
        => provider switch
        {
            "Stripe"      => HandleStripeEventAsync(eventType, rawPayload, ct),
            "MercadoPago" => HandleMercadoPagoEventAsync(eventType, rawPayload, ct),
            _             => Task.CompletedTask,
        };

    // ─── Stripe ───────────────────────────────────────────────────────────

    private async Task HandleStripeEventAsync(string eventType, string rawPayload, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(rawPayload);
        var root = doc.RootElement;

        switch (eventType)
        {
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
            {
                var externalSubId = root.GetProperty("data").GetProperty("object").GetProperty("id").GetString();
                var status        = root.GetProperty("data").GetProperty("object").GetProperty("status").GetString();
                await UpdateSubscriptionStatusAsync(externalSubId!, MapStripeStatus(status!), ct);
                break;
            }
            case "invoice.payment_succeeded":
            {
                var externalSubId = root.GetProperty("data").GetProperty("object").GetProperty("subscription").GetString();
                var amount        = root.GetProperty("data").GetProperty("object").GetProperty("amount_paid").GetDecimal() / 100m;
                var currency      = root.GetProperty("data").GetProperty("object").GetProperty("currency").GetString()?.ToUpperInvariant() ?? "USD";
                var invoiceId     = root.GetProperty("data").GetProperty("object").GetProperty("id").GetString();
                await RecordPaymentAsync(externalSubId!, amount, currency, invoiceId!, "SUCCEEDED", ct);
                break;
            }
            case "invoice.payment_failed":
            {
                var externalSubId = root.GetProperty("data").GetProperty("object").GetProperty("subscription").GetString();
                await UpdateSubscriptionStatusAsync(externalSubId!, SubscriptionStatus.PastDue, ct);
                break;
            }
        }
    }

    // ─── MercadoPago ──────────────────────────────────────────────────────

    private async Task HandleMercadoPagoEventAsync(string eventType, string rawPayload, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(rawPayload);
        var root = doc.RootElement;

        // MercadoPago envía eventos de tipo "payment" con un id de pago externo.
        if (eventType == "payment" && root.TryGetProperty("data", out var data))
        {
            var paymentId = data.GetProperty("id").GetString();
            // Registramos como pago pendiente de reconciliación; la lógica completa
            // requiere consultar la API de MP con el payment id y reconciliar la suscripción.
            _logger.LogInformation("MercadoPago payment webhook recibido para payment id: {PaymentId}", paymentId);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private async Task UpdateSubscriptionStatusAsync(string externalSubId, string newStatus, CancellationToken ct)
    {
        var sub = await _db.BillingSubscriptions
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.ExternalSubscriptionId == externalSubId, ct);

        if (sub is null) return;

        sub.Status = newStatus;
        if (newStatus == SubscriptionStatus.Canceled) sub.CanceledAt = DateTime.UtcNow;

        var email = sub.Customer.Email;
        if (!string.IsNullOrWhiteSpace(email))
        {
            await _email.EnviarAsync(new()
            {
                To      = email,
                Subject = $"Estado de suscripción actualizado: {newStatus}",
                HtmlBody = $"<p>El estado de tu suscripción ha cambiado a <strong>{newStatus}</strong>.</p>",
            }, ct);
        }
    }

    private async Task RecordPaymentAsync(
        string externalSubId, decimal amount, string currency, string externalInvoiceId, string status, CancellationToken ct)
    {
        var sub = await _db.BillingSubscriptions
            .FirstOrDefaultAsync(s => s.ExternalSubscriptionId == externalSubId, ct);

        if (sub is null) return;

        _db.BillingPayments.Add(new BillingPayment
        {
            BillingSubscriptionId = sub.Id,
            ExternalPaymentId     = externalInvoiceId,
            Amount                = amount,
            Currency              = currency,
            Status                = status,
            PaidAt                = DateTime.UtcNow,
        });

        if (sub.Status == SubscriptionStatus.PastDue || sub.Status == SubscriptionStatus.Trialing)
            sub.Status = SubscriptionStatus.Active;
    }

    private static string MapStripeStatus(string stripeStatus) => stripeStatus switch
    {
        "active"    => SubscriptionStatus.Active,
        "trialing"  => SubscriptionStatus.Trialing,
        "past_due"  => SubscriptionStatus.PastDue,
        "canceled"  => SubscriptionStatus.Canceled,
        "incomplete"=> SubscriptionStatus.Incomplete,
        "unpaid"    => SubscriptionStatus.Suspended,
        _           => stripeStatus.ToUpperInvariant(),
    };
}
