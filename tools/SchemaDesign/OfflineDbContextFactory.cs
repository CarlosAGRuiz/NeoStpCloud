using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NeoSTP.Infrastructure.Persistence;

/// <summary>Diseño y revisión SQL sin cargar configuración o secretos del entorno.</summary>
public sealed class OfflineDbContextFactory : IDesignTimeDbContextFactory<NeoStpDbContext>
{
    public NeoStpDbContext CreateDbContext(string[] args)
    {
        if (!args.Contains("--offline-schema", StringComparer.Ordinal))
            throw new InvalidOperationException("Requiere --offline-schema; esta herramienta no aplica migraciones.");

        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=NeoStp_DesignOnly;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True")
            .AddInterceptors(new RejectConnectionInterceptor())
            .Options;
        return new NeoStpDbContext(options);
    }

    // Una invocación accidental de database update tampoco puede abrir conexiones.
    private sealed class RejectConnectionInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
    {
        public override Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult ConnectionOpening(
            System.Data.Common.DbConnection connection,
            Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result) =>
            throw new InvalidOperationException("Conexiones deshabilitadas en SchemaDesign.");

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult> ConnectionOpeningAsync(
            System.Data.Common.DbConnection connection,
            Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Conexiones deshabilitadas en SchemaDesign.");
    }
}
