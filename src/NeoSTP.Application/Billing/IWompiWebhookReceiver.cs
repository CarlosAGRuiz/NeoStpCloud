using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

public interface IWompiWebhookReceiver
{
    Task<Result<WompiWebhookReceipt>> ReceiveAsync(ReadOnlyMemory<byte> body, string signature, CancellationToken ct = default);
}
public sealed record WompiWebhookReceipt(Guid ReceiptId, string Status);