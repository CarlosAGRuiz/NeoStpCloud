using NeoSTP.Application.Common;

namespace NeoSTP.Application.Billing;

/// <summary>
/// Ejecuta y recupera operaciones de billing cuya intención ya fue confirmada en SQL.
/// </summary>
public interface IBillingProviderOperationProcessor
{
    Task<Result> ProcessAsync(int operationId, CancellationToken ct = default);
    Task<int> ProcessPendingAsync(CancellationToken ct = default);
}
