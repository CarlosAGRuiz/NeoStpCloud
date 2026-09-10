using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NeoSTP.Application.Common;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>Serializes administrative license changes with checkout reservation and payment application.</summary>
public static class BillingCompanyTransaction
{
    public static Task<T> RunAsync<T>(NeoStpDbContext db, int empresaId, Func<Task<T>> action, CancellationToken ct) where T : Result
        => new SingleAttempt(db).ExecuteAsync(async () =>
        {
            if (!db.Database.IsRelational()) return await action();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @r int;
                    EXEC @r = sys.sp_getapplock @Resource = {$"NeoSTP:BILLING:{empresaId}"}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                    IF @r < 0 THROW 50001, 'License mutation unavailable.', 1;
                    """, ct);
            var result = await action();
            if (result.IsSuccess) await tx.CommitAsync(ct);
            return result;
        });
    private sealed class SingleAttempt(DbContext context) : ExecutionStrategy(context, 0, TimeSpan.Zero)
    { protected override bool ShouldRetryOn(Exception exception) => false; }
}