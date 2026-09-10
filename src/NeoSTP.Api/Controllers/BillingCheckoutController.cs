using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/billing/empresas/{empresaId:int}/checkouts")]
public sealed class BillingCheckoutController(IBillingService billing, IOptions<BillingOptions> options) : ControllerBase
{
    public sealed record CreateRequest(int PlanId, string? Metodo = null);

    [HttpPost]
    public async Task<IActionResult> Create(int empresaId, CreateRequest request, CancellationToken ct)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            return Reply(Result<CheckoutSessionResult>.Fail("Use una sola clave idempotente.", "IDEMPOTENCY_KEY_INVALID"));
        return Reply(await billing.CreateCheckoutSessionAsync(new(empresaId, request.PlanId,
            options.Value.Checkout?.SuccessUrl ?? string.Empty, request.Metodo, keys.Count == 1 ? keys[0] : null), ct));
    }

    [HttpGet("{correlationId:guid}")]
    public async Task<IActionResult> Get(int empresaId, Guid correlationId, CancellationToken ct)
        => Reply(await billing.GetCheckoutAsync(empresaId, correlationId, ct));

    private IActionResult Reply<T>(Result<T> result)
    {
        if (result.IsSuccess) return Ok(ApiResponse<T>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
        var body = ApiResponse<T>.Fail(result.Error ?? "No se pudo completar la operación.", result.ValidationErrors, HttpContext.TraceIdentifier);
        body.Code = result.ErrorCode; body.Data = result.Value;
        var status = result.ErrorCode switch
        {
            "BILLING_FORBIDDEN" => StatusCodes.Status403Forbidden,
            "BILLING_CHECKOUT_NOT_FOUND" or "PLAN_NOT_FOUND" => StatusCodes.Status404NotFound,
            "BILLING_CHECKOUT_UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
            "BILLING_CHECKOUT_MAPPING_INVALID" => StatusCodes.Status422UnprocessableEntity,
            "IDEMPOTENCY_CONFLICT" or "BILLING_CHECKOUT_PENDING" or "BILLING_RECONCILIATION_REQUIRED"
                or "BILLING_CANCELLATION_PENDING" or "BILLING_TRANSFER_PENDING" or "BILLING_SUBSCRIPTION_AMBIGUOUS" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return StatusCode(status, body);
    }
}
