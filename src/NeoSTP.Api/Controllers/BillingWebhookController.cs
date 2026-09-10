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

        var signingSecret = _opts.Stripe.WebhookSecret;
        if (!IsConfiguredStripeSigningSecret(signingSecret))
        {
            _logger.LogWarning("Stripe webhook rechazado: el secreto de firma no está configurado correctamente.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Webhook de Stripe no disponible.");
        }

        string eventType;
        string eventId;
        try
        {
            // Se valida también el JSON de forma acotada para devolver un rechazo genérico
            // ante contenido malformado, sin registrar el cuerpo ni datos de la firma.
            using var _ = System.Text.Json.JsonDocument.Parse(payload);
            var stripeSignature = Request.Headers["Stripe-Signature"].ToString();
            var stripeEvent = EventUtility.ConstructEvent(payload, stripeSignature, signingSecret!);
            eventType = stripeEvent.Type;
            eventId   = stripeEvent.Id;
        }
        catch (Exception ex) when (ex is StripeException or System.Text.Json.JsonException
            || ex.GetType().Namespace?.StartsWith("Newtonsoft.Json", StringComparison.Ordinal) == true)
        {
            _logger.LogWarning("Stripe webhook rechazado: firma o payload inválido.");
            return BadRequest("Webhook inválido.");
        }

        var result = await _handler.HandleAsync("Stripe", eventType, eventId, payload, ct);
        return result.IsSuccess ? Ok() : StatusCode(500, result.Error);
    }

    private static bool IsConfiguredStripeSigningSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || !string.Equals(secret, secret.Trim(), StringComparison.Ordinal))
            return false;
        if (!secret.StartsWith("whsec_", StringComparison.Ordinal) || secret.Length < 18)
            return false;
        return !secret.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)
            && !secret.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);
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
