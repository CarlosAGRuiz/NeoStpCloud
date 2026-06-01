using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using Stripe;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Recibe webhooks de Stripe y MercadoPago.
/// Estos endpoints NO requieren autenticación JWT (los proveedores no envían token);
/// la verificación de firma se hace con el webhook secret de cada proveedor.
/// </summary>
[Route("api/billing/webhooks")]
[ApiController]
public class BillingWebhookController : ControllerBase
{
    private readonly IBillingWebhookHandler _handler;
    private readonly BillingOptions _opts;
    private readonly ILogger<BillingWebhookController> _logger;

    public BillingWebhookController(
        IBillingWebhookHandler handler,
        IOptions<BillingOptions> opts,
        ILogger<BillingWebhookController> logger)
    {
        _handler = handler;
        _opts    = opts.Value;
        _logger  = logger;
    }

    // ─── Stripe ───────────────────────────────────────────────────────────

    /// <summary>POST /api/billing/webhooks/stripe</summary>
    [HttpPost("stripe")]
    public async Task<IActionResult> Stripe(CancellationToken ct)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);

        // Verificar firma si está configurado el webhook secret.
        string eventType;
        string eventId;
        try
        {
            if (!string.IsNullOrWhiteSpace(_opts.Stripe.WebhookSecret)
                && !_opts.Stripe.WebhookSecret.StartsWith("REPLACE"))
            {
                var stripeSignature = Request.Headers["Stripe-Signature"].ToString();
                var stripeEvent = EventUtility.ConstructEvent(payload, stripeSignature, _opts.Stripe.WebhookSecret);
                eventType = stripeEvent.Type;
                eventId   = stripeEvent.Id;
            }
            else
            {
                // Sin webhook secret configurado: extraemos tipo e id del payload directo (solo para Mock/dev).
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                eventType = doc.RootElement.GetProperty("type").GetString() ?? "unknown";
                eventId   = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            }
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Stripe webhook firma inválida: {Message}", ex.Message);
            return BadRequest("Firma inválida.");
        }

        var result = await _handler.HandleAsync("Stripe", eventType, eventId, payload, ct);
        return result.IsSuccess ? Ok() : StatusCode(500, result.Error);
    }

    // ─── MercadoPago ──────────────────────────────────────────────────────

    /// <summary>POST /api/billing/webhooks/mercadopago</summary>
    [HttpPost("mercadopago")]
    public async Task<IActionResult> MercadoPago(CancellationToken ct)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);

        string eventType;
        string eventId;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            eventType = doc.RootElement.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "unknown" : "unknown";
            // MercadoPago usa "id" o "data.id" como identificador del evento.
            eventId = doc.RootElement.TryGetProperty("id", out var idEl)
                ? idEl.ToString()
                : Guid.NewGuid().ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("MercadoPago webhook payload inválido: {Message}", ex.Message);
            return BadRequest("Payload inválido.");
        }

        var result = await _handler.HandleAsync("MercadoPago", eventType, eventId, payload, ct);
        return result.IsSuccess ? Ok() : StatusCode(500, result.Error);
    }
}
