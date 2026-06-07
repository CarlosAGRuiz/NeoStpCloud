using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>
/// Readiness check de la base de datos: verifica que el DbContext pueda conectar.
/// Etiquetado <c>ready</c> para que <c>/health/ready</c> lo incluya y <c>/health/live</c> no.
/// </summary>
public sealed class DbHealthCheck : IHealthCheck
{
    private readonly NeoStpDbContext _db;

    public DbHealthCheck(NeoStpDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _db.Database.CanConnectAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy("Base de datos accesible.")
                : HealthCheckResult.Unhealthy("No se pudo conectar a la base de datos.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error al conectar con la base de datos.", ex);
        }
    }
}
