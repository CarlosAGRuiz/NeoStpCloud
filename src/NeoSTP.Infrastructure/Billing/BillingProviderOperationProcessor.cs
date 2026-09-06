using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// Procesa una intención ya comprometida en SQL. La llamada externa ocurre sin una
/// transacción abierta; su confirmación se persiste antes de aplicar el estado local.
/// </summary>
public sealed class BillingProviderOperationProcessor : IBillingProviderOperationProcessor
{
    private const string ReconciliationCode = "BILLING_RECONCILIATION_REQUIRED";
    private readonly NeoStpDbContext _db;
    private readonly IPaymentProviderResolver _payments;
    private readonly IEmailSender _email;
    private readonly BillingProviderOperationsOptions _options;
    private readonly ILogger<BillingProviderOperationProcessor> _logger;

    public BillingProviderOperationProcessor(
        NeoStpDbContext db,
        IPaymentProviderResolver payments,
        IEmailSender email,
        IOptions<BillingOptions> options,
        ILogger<BillingProviderOperationProcessor> logger)
    {
        _db = db;
        _payments = payments;
        _email = email;
        _options = options.Value.ProviderOperations;
        _logger = logger;
    }

    public async Task<Result> ProcessAsync(int operationId, CancellationToken ct = default)
    {
        _db.ChangeTracker.Clear();
        var now = DateTime.UtcNow;
        var current = await _db.BillingProviderOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == operationId, ct);
        if (current is null)
            return Result.Fail("Operación de cancelación no encontrada.", "BILLING_OPERATION_NOT_FOUND");
        if (current.Status == BillingProviderOperationStatuses.Completed)
            return Result.Ok();
        if (current.Status == BillingProviderOperationStatuses.RequiresReconciliation)
            return Result.Fail("La cancelación requiere conciliación operativa; no se repetirá automáticamente.",
                ReconciliationCode);
        if (current.OperationType != BillingProviderOperationTypes.CancelSubscription
            || current.BillingSubscriptionId is null)
            return await QuarantineUnconfirmedAsync(current, null,
                "La operación durable no contiene una suscripción válida.", ct);

        if (current.Status == BillingProviderOperationStatuses.Processing)
        {
            if (current.LeaseExpiresAt is not DateTime leaseEnd)
            {
                if (current.ProviderConfirmedAt is null)
                    return await QuarantineUnconfirmedAsync(current, null,
                        "La operación estaba en proceso sin un lease verificable.", ct);
                // El ACK es durable: un lease ausente no autoriza repetir el proveedor,
                // pero sí permite reclamar y terminar exclusivamente la fase local.
            }
            else if (leaseEnd > now)
                return Result.Fail("La cancelación ya está siendo procesada.", "BILLING_OPERATION_IN_PROGRESS");
            else if (current.ProviderConfirmedAt is null)
                return await QuarantineUnconfirmedAsync(current, null,
                    "El proceso anterior venció sin confirmar el resultado del proveedor.", ct);
        }

        if (current.Status != BillingProviderOperationStatuses.Pending
            && current.Status != BillingProviderOperationStatuses.Processing)
            return Result.Fail("La operación de cancelación no se encuentra en un estado procesable.",
                "BILLING_OPERATION_INVALID_STATE");
        if (current.Status == BillingProviderOperationStatuses.Pending
            && current.NextAttemptAt is DateTime nextAttempt && nextAttempt > now)
            return Result.Fail("La cancelación aún no está disponible para procesamiento.",
                "BILLING_OPERATION_NOT_DUE");

        var leaseId = Guid.NewGuid().ToString("N");
        if (!await TryClaimAsync(current.Id, leaseId, now, ct))
            return Result.Fail("Otro proceso tomó la operación de cancelación.",
                "BILLING_OPERATION_IN_PROGRESS");

        _db.ChangeTracker.Clear();
        current = await _db.BillingProviderOperations.AsNoTracking()
            .FirstAsync(o => o.Id == operationId, ct);

        if (current.ProviderConfirmedAt is null)
        {
            var correlationError = await ValidatePreProviderCorrelationAsync(current, ct);
            if (correlationError is not null)
                return await QuarantineOwnedAsync(current.Id, leaseId, ReconciliationCode,
                    correlationError, ct);

            if (!string.IsNullOrWhiteSpace(current.ExternalResourceId))
            {
                var provider = _payments.Resolve(current.Provider);
                if (!string.Equals(provider.ProviderName, current.Provider, StringComparison.OrdinalIgnoreCase))
                    return await QuarantineOwnedAsync(current.Id, leaseId, "BILLING_PROVIDER_NOT_CONFIGURED",
                        "El proveedor registrado en la operación no está configurado.", ct);

                Result providerResult;
                try
                {
                    providerResult = await provider.CancelSubscriptionAsync(
                        current.ExternalResourceId,
                        current.CancelAtPeriodEnd,
                        current.IdempotencyKey,
                        ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Proveedor no confirmó operación billing id={OperationId} provider={Provider}",
                        current.Id, current.Provider);
                    return await QuarantineOwnedAsync(current.Id, leaseId, ReconciliationCode,
                        "El proveedor no confirmó el resultado de la cancelación.", ct);
                }

                if (providerResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Proveedor rechazó o no confirmó operación billing id={OperationId} provider={Provider} code={Code}",
                        current.Id, current.Provider, providerResult.ErrorCode);
                    return await QuarantineOwnedAsync(current.Id, leaseId,
                        SanitizeCode(providerResult.ErrorCode) ?? ReconciliationCode,
                        "El proveedor no confirmó el resultado de la cancelación.", ct);
                }
            }

            if (!await ConfirmProviderAsync(current.Id, leaseId, ct))
                return Result.Fail("La confirmación del proveedor quedó pendiente de conciliación.",
                    ReconciliationCode);
        }

        // Provider work is already durably acknowledged. Run the following explicit SQL
        // transaction under a zero-retry strategy so EF never rejects it and never widens
        // this workflow into an implicit replay of external side effects.
        return await SingleAttemptAsync(() => ApplyLocalStateAsync(current.Id, leaseId, ct));
    }

    public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var batchSize = Math.Clamp(_options.BatchSize, 1, 100);
        var operations = await _db.BillingProviderOperations.AsNoTracking()
            .Where(o => (o.Status == BillingProviderOperationStatuses.Pending
                         && (o.NextAttemptAt == null || o.NextAttemptAt <= now))
                     || (o.Status == BillingProviderOperationStatuses.Processing
                         && (o.LeaseExpiresAt == null || o.LeaseExpiresAt <= now)))
            .OrderBy(o => o.NextAttemptAt).ThenBy(o => o.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var operation in operations)
        {
            if (operation.Status == BillingProviderOperationStatuses.Processing
                && operation.ProviderConfirmedAt is null)
            {
                await QuarantineUnconfirmedAsync(operation, null,
                    "El lease venció sin una confirmación durable del proveedor.", ct);
                processed++;
                continue;
            }

            await ProcessAsync(operation.Id, ct);
            processed++;
        }
        return processed;
    }

    private async Task<bool> TryClaimAsync(int id, string leaseId, DateTime now, CancellationToken ct)
    {
        var leaseUntil = now.AddSeconds(Math.Clamp(_options.LeaseSeconds, 30, 900));
        if (_db.Database.IsRelational())
        {
            var changed = await _db.BillingProviderOperations
                .Where(o => o.Id == id
                    && (o.Status == BillingProviderOperationStatuses.Pending
                        || o.Status == BillingProviderOperationStatuses.Processing
                        && (o.LeaseExpiresAt == null || o.LeaseExpiresAt <= now)
                        && o.ProviderConfirmedAt != null))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, BillingProviderOperationStatuses.Processing)
                    .SetProperty(o => o.LeaseId, leaseId)
                    .SetProperty(o => o.LeaseExpiresAt, leaseUntil)
                    .SetProperty(o => o.Attempts, o => o.Attempts + 1)
                    .SetProperty(o => o.UpdatedAt, now), ct);
            return changed == 1;
        }

        var operation = await _db.BillingProviderOperations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (operation is null
            || operation.Status == BillingProviderOperationStatuses.Processing
               && (operation.LeaseExpiresAt > now || operation.ProviderConfirmedAt is null)
            || operation.Status != BillingProviderOperationStatuses.Pending
               && operation.Status != BillingProviderOperationStatuses.Processing)
            return false;
        operation.Status = BillingProviderOperationStatuses.Processing;
        operation.LeaseId = leaseId;
        operation.LeaseExpiresAt = leaseUntil;
        operation.Attempts++;
        operation.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> ConfirmProviderAsync(int id, string leaseId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (_db.Database.IsRelational())
        {
            var changed = await _db.BillingProviderOperations
                .Where(o => o.Id == id && o.Status == BillingProviderOperationStatuses.Processing
                    && o.LeaseId == leaseId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.ProviderConfirmedAt, now)
                    .SetProperty(o => o.UpdatedAt, now), ct);
            return changed == 1;
        }

        var operation = await _db.BillingProviderOperations.FirstOrDefaultAsync(
            o => o.Id == id && o.Status == BillingProviderOperationStatuses.Processing
                 && o.LeaseId == leaseId, ct);
        if (operation is null) return false;
        operation.ProviderConfirmedAt = now;
        operation.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Result> ApplyLocalStateAsync(int id, string leaseId, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        var snapshot = await _db.BillingProviderOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (snapshot is null)
            return Result.Fail("Operación de cancelación no encontrada.", "BILLING_OPERATION_NOT_FOUND");

        await using var transaction = await BeginCompanyTransactionAsync(snapshot.EmpresaId, ct);
        var operation = await _db.BillingProviderOperations
            .Include(o => o.BillingSubscription!).ThenInclude(s => s.Customer)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (operation is null)
            return Result.Fail("Operación de cancelación no encontrada.", "BILLING_OPERATION_NOT_FOUND");
        if (operation.Status == BillingProviderOperationStatuses.Completed)
            return Result.Ok();
        if (operation.Status != BillingProviderOperationStatuses.Processing
            || operation.LeaseId != leaseId || operation.ProviderConfirmedAt is null
            || operation.BillingSubscription is null)
            return Result.Fail("La operación requiere conciliación antes de aplicar el estado local.",
                ReconciliationCode);
        if (operation.BillingSubscription.Customer.EmpresaId != operation.EmpresaId)
            return await QuarantineTrackedAsync(operation, transaction,
                "La empresa de la suscripción no coincide con la operación de cancelación.", ct);

        var subscription = operation.BillingSubscription;
        if (subscription.PlanId != operation.PlanId
            || !string.Equals(subscription.Customer.Provider, operation.Provider, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(subscription.ExternalSubscriptionId, operation.ExternalResourceId,
                StringComparison.Ordinal))
            return await QuarantineTrackedAsync(operation, transaction,
                "La suscripción cambió después de crear la operación.", ct);

        NeoSTP.Domain.Core.Licenciamiento.EmpresaPlan? license = null;
        if (operation.EmpresaPlanId is int licenseId)
        {
            license = await _db.EmpresaPlanes.FirstOrDefaultAsync(
                p => p.Id == licenseId && p.EmpresaId == operation.EmpresaId, ct);
            if (license is null || license.PlanId != operation.PlanId || license.EstadoCodigo != "ACTIVO")
                return await QuarantineTrackedAsync(operation, transaction,
                    "La licencia ya no coincide con la operación de cancelación.", ct);
        }
        else if (await _db.EmpresaPlanes.AnyAsync(
                     p => p.EmpresaId == operation.EmpresaId && p.EstadoCodigo == "ACTIVO", ct))
        {
            return await QuarantineTrackedAsync(operation, transaction,
                "Apareció una licencia activa después de crear la operación de cancelación.", ct);
        }

        var now = DateTime.UtcNow;
        var preserveAccess = operation.CancelAtPeriodEnd
            && operation.AccessEndsAt is DateTime accessEnd && accessEnd > now && license is not null;

        subscription.Status = SubscriptionStatus.Canceled;
        subscription.CanceledAt ??= operation.RequestedAt;
        subscription.CancelAtPeriodEnd = preserveAccess;
        subscription.UpdatedAt = now;
        subscription.UpdatedBy = operation.CreatedBy;

        if (license is not null)
        {
            var effectiveEnd = preserveAccess ? operation.AccessEndsAt!.Value : operation.RequestedAt;
            license.FechaFin = license.FechaFin is DateTime previousEnd && previousEnd < effectiveEnd
                ? previousEnd : effectiveEnd;
            if (!preserveAccess) license.EstadoCodigo = "CANCELADO";
            license.UpdatedAt = now;
            license.UpdatedBy = operation.CreatedBy;
        }

        operation.Status = BillingProviderOperationStatuses.Completed;
        operation.CompletedAt = now;
        operation.LeaseId = null;
        operation.LeaseExpiresAt = null;
        operation.LastError = null;
        operation.LastErrorCode = null;
        operation.UpdatedAt = now;
        operation.UpdatedBy = operation.CreatedBy;
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        if (!string.IsNullOrWhiteSpace(subscription.Customer.Email))
        {
            try
            {
                await _email.EnviarAsync(new()
                {
                    To = subscription.Customer.Email,
                    Subject = "Tu suscripción ha sido cancelada",
                    HtmlBody = preserveAccess
                        ? $"<p>Tu suscripción ha sido cancelada. Mantendrás acceso hasta el <strong>{operation.AccessEndsAt:dd/MM/yyyy}</strong>.</p>"
                        : "<p>Tu suscripción ha sido cancelada inmediatamente.</p>",
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "No se pudo notificar cancelación billing operationId={OperationId}", operation.Id);
            }
        }
        return Result.Ok();
    }

    private async Task<string?> ValidatePreProviderCorrelationAsync(
        BillingProviderOperation operation,
        CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        var subscription = await _db.BillingSubscriptions.AsNoTracking()
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == operation.BillingSubscriptionId, ct);
        if (subscription is null)
            return "La suscripción registrada en la operación ya no existe.";
        if (subscription.Customer.EmpresaId != operation.EmpresaId)
            return "La empresa de la suscripción no coincide con la operación de cancelación.";
        if (subscription.PlanId != operation.PlanId)
            return "El plan de la suscripción cambió después de crear la operación.";
        if (!string.Equals(subscription.Customer.Provider, operation.Provider, StringComparison.OrdinalIgnoreCase))
            return "El proveedor de la suscripción cambió después de crear la operación.";
        if (!string.Equals(subscription.ExternalSubscriptionId, operation.ExternalResourceId,
                StringComparison.Ordinal))
            return "El identificador externo de la suscripción cambió después de crear la operación.";

        if (operation.EmpresaPlanId is int licenseId)
        {
            var license = await _db.EmpresaPlanes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == licenseId, ct);
            if (license is null || license.EmpresaId != operation.EmpresaId
                || license.PlanId != operation.PlanId || license.EstadoCodigo != "ACTIVO")
                return "La licencia ya no coincide con la empresa y plan de la operación.";
        }
        else if (await _db.EmpresaPlanes.AsNoTracking().AnyAsync(
                     p => p.EmpresaId == operation.EmpresaId && p.EstadoCodigo == "ACTIVO", ct))
        {
            return "Apareció una licencia activa después de crear la operación.";
        }

        return null;
    }

    private async Task<Result> QuarantineUnconfirmedAsync(
        BillingProviderOperation expected, string? code, string message, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        var errorCode = SanitizeCode(code) ?? ReconciliationCode;
        var error = message[..Math.Min(message.Length, 2000)];
        var now = DateTime.UtcNow;

        if (_db.Database.IsRelational())
        {
            var changed = await _db.BillingProviderOperations
                .Where(o => o.Id == expected.Id
                    && o.Status == expected.Status
                    && o.LeaseId == expected.LeaseId
                    && o.LeaseExpiresAt == expected.LeaseExpiresAt
                    && o.ProviderConfirmedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, BillingProviderOperationStatuses.RequiresReconciliation)
                    .SetProperty(o => o.LeaseId, (string?)null)
                    .SetProperty(o => o.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(o => o.NextAttemptAt, (DateTime?)null)
                    .SetProperty(o => o.LastErrorCode, errorCode)
                    .SetProperty(o => o.LastError, error)
                    .SetProperty(o => o.UpdatedAt, now), ct);
            return changed == 1
                ? ReconciliationFailure(errorCode)
                : await ResolveQuarantineRaceAsync(expected.Id, ct);
        }

        var operation = await _db.BillingProviderOperations.FirstOrDefaultAsync(o => o.Id == expected.Id, ct);
        if (operation is null)
            return Result.Fail("Operación de cancelación no encontrada.", "BILLING_OPERATION_NOT_FOUND");
        if (operation.Status != expected.Status || operation.LeaseId != expected.LeaseId
            || operation.LeaseExpiresAt != expected.LeaseExpiresAt || operation.ProviderConfirmedAt is not null)
            return await ResolveQuarantineRaceAsync(expected.Id, ct);
        ApplyQuarantine(operation, errorCode, error, now);
        await _db.SaveChangesAsync(ct);
        return ReconciliationFailure(errorCode);
    }

    private async Task<Result> QuarantineOwnedAsync(
        int id, string leaseId, string? code, string message, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        var errorCode = SanitizeCode(code) ?? ReconciliationCode;
        var error = message[..Math.Min(message.Length, 2000)];
        var now = DateTime.UtcNow;

        if (_db.Database.IsRelational())
        {
            var changed = await _db.BillingProviderOperations
                .Where(o => o.Id == id && o.Status == BillingProviderOperationStatuses.Processing
                    && o.LeaseId == leaseId && o.ProviderConfirmedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, BillingProviderOperationStatuses.RequiresReconciliation)
                    .SetProperty(o => o.LeaseId, (string?)null)
                    .SetProperty(o => o.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(o => o.NextAttemptAt, (DateTime?)null)
                    .SetProperty(o => o.LastErrorCode, errorCode)
                    .SetProperty(o => o.LastError, error)
                    .SetProperty(o => o.UpdatedAt, now), ct);
            return changed == 1
                ? ReconciliationFailure(errorCode)
                : await ResolveQuarantineRaceAsync(id, ct);
        }

        var operation = await _db.BillingProviderOperations.FirstOrDefaultAsync(
            o => o.Id == id && o.Status == BillingProviderOperationStatuses.Processing
                 && o.LeaseId == leaseId && o.ProviderConfirmedAt == null, ct);
        if (operation is null) return await ResolveQuarantineRaceAsync(id, ct);
        ApplyQuarantine(operation, errorCode, error, now);
        await _db.SaveChangesAsync(ct);
        return ReconciliationFailure(errorCode);
    }

    private async Task<Result> ResolveQuarantineRaceAsync(int id, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        var current = await _db.BillingProviderOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (current is null)
            return Result.Fail("Operación de cancelación no encontrada.", "BILLING_OPERATION_NOT_FOUND");
        if (current.Status == BillingProviderOperationStatuses.Completed)
            return Result.Ok();
        if (current.Status == BillingProviderOperationStatuses.RequiresReconciliation)
            return ReconciliationFailure(current.LastErrorCode ?? ReconciliationCode);
        return Result.Fail("La operación cambió mientras se conciliaba y continúa bajo control de otro proceso.",
            "BILLING_OPERATION_IN_PROGRESS");
    }

    private static void ApplyQuarantine(
        BillingProviderOperation operation, string errorCode, string error, DateTime now)
    {
        operation.Status = BillingProviderOperationStatuses.RequiresReconciliation;
        operation.LeaseId = null;
        operation.LeaseExpiresAt = null;
        operation.NextAttemptAt = null;
        operation.LastErrorCode = errorCode;
        operation.LastError = error;
        operation.UpdatedAt = now;
    }

    private static Result ReconciliationFailure(string code)
        => Result.Fail("La cancelación requiere conciliación operativa; no se repetirá automáticamente.", code);

    private async Task<Result> QuarantineTrackedAsync(
        BillingProviderOperation operation,
        IDbContextTransaction? transaction,
        string message,
        CancellationToken ct)
    {
        operation.Status = BillingProviderOperationStatuses.RequiresReconciliation;
        operation.LeaseId = null;
        operation.LeaseExpiresAt = null;
        operation.NextAttemptAt = null;
        operation.LastErrorCode = ReconciliationCode;
        operation.LastError = message[..Math.Min(message.Length, 2000)];
        operation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Result.Fail("La cancelación requiere conciliación operativa; no se repetirá automáticamente.",
            ReconciliationCode);
    }

    private async Task<IDbContextTransaction?> BeginCompanyTransactionAsync(int empresaId, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return null;
        var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (_db.Database.IsSqlServer())
            {
                var resource = $"NeoSTP:BILLING:{empresaId}";
                await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource = {resource},
                        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                    IF @result < 0 THROW 50001, 'No fue posible reservar la modificación de la suscripción.', 1;
                    """, ct);
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private Task<T> SingleAttemptAsync<T>(Func<Task<T>> operation)
        => new BillingSingleAttemptStrategy(_db).ExecuteAsync(operation);

    private sealed class BillingSingleAttemptStrategy(DbContext context)
        : ExecutionStrategy(context, 0, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }

    private static string? SanitizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var safe = new string(code.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (safe.Length == 0) return null;
        return safe[..Math.Min(safe.Length, 100)];
    }
}
