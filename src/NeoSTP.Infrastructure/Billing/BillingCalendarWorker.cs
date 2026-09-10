using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>Idempotent monthly invoice generation. Does not collect money or suspend access.</summary>
public sealed class BillingCalendarWorker(IServiceScopeFactory scopes, ILogger<BillingCalendarWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
                var companies = await db.BillingCalendarAgreements.AsNoTracking().Where(x => x.Active).Select(x => x.EmpresaId).ToListAsync(stoppingToken);
                foreach (var empresaId in companies)
                {
                    using var tenantScope = scopes.CreateScope();
                    var result = await BillingCalendarCycle.AdvanceAsync(tenantScope.ServiceProvider.GetRequiredService<NeoStpDbContext>(), empresaId, DateTime.UtcNow, stoppingToken);
                    if (result.IsFailure) log.LogWarning("Monthly billing reconciliation required for company {EmpresaId}: {ErrorCode}", empresaId, result.ErrorCode);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError("Monthly billing cycle failed: {ExceptionType}", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
