using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Billing;

namespace NeoSTP.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/billing/webhooks/wompi")]
public sealed class WompiWebhookController(IWompiWebhookReceiver receiver) : ControllerBase
{
    private const int BodyLimit = 65536;
    [HttpPost]
    [RequestSizeLimit(BodyLimit)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var values = Request.Headers["wompi_hash"];
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])) return Unauthorized();
        if (Request.ContentLength > BodyLimit) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        // Enforce limit for chunked requests too. Signature covers the original bytes, never reserialized JSON.
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var count = await Request.Body.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, BodyLimit + 1 - (int)buffer.Length)), ct);
            if (count == 0) break;
            buffer.Write(chunk, 0, count);
            if (buffer.Length > BodyLimit) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        try
        {
            var result = await receiver.ReceiveAsync(buffer.ToArray(), values[0]!, ct);
            if (result.IsSuccess) return Accepted(new { receiptId = result.Value!.ReceiptId, status = result.Value.Status });
            var status = result.ErrorCode switch
            {
                "WOMPI_WEBHOOK_UNAVAILABLE" or "WOMPI_VERIFICATION_PENDING" => 503,
                "WOMPI_SIGNATURE_INVALID" or "WOMPI_IDENTITY_MISMATCH" => 401,
                "WOMPI_PAYLOAD_TOO_LARGE" => 413,
                "WOMPI_EVENT_CONFLICT" => 409,
                _ => 400,
            };
            return StatusCode(status, new { code = result.ErrorCode, receiptId = result.Value?.ReceiptId, traceId = HttpContext.TraceIdentifier });
        }
        catch (Exception)
        {
            // No 2xx until receipt commit; a provider retry can safely recover persisted work.
            return StatusCode(503, new { code = "WOMPI_VERIFICATION_PENDING", traceId = HttpContext.TraceIdentifier });
        }
    }
}