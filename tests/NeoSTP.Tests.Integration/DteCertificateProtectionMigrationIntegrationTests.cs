using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Integration;

public sealed class DteCertificateProtectionMigrationIntegrationTests
{
    private const string ConnectionVariable = "NEOSTP_SQLSERVER_TEST_CONNECTION";

    [SqlServerFact]
    public async Task Migration_runs_inside_sql_server_retry_strategy_and_protects_all_certificates()
    {
        var rootConnection = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var databaseName = $"NeoSTP_CI_CertMigration_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(rootConnection)
        {
            InitialCatalog = databaseName,
            ConnectTimeout = 5,
        };
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseSqlServer(builder.ConnectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        await using var db = new NeoStpDbContext(options);
        try
        {
            await MigrateWithStartupRetryAsync(db);

            var empresa = new Empresa
            {
                Nit = $"TEST-{Guid.NewGuid():N}",
                RazonSocial = "Synthetic certificate migration tenant",
                EstadoCodigo = EmpresaEstados.Activa,
            };
            var configuration = new DteConfiguracion
            {
                Empresa = empresa,
                CertificadoBlob = [1, 2, 3],
            };
            db.DteConfiguracion.Add(configuration);
            await db.SaveChangesAsync();

            db.DteConfiguracionVersiones.Add(new DteConfiguracionVersion
            {
                EmpresaId = empresa.Id,
                ConfiguracionId = configuration.Id,
                Motivo = "SYNTHETIC",
                CertificadoBlob = [4, 5, 6],
            });
            await db.SaveChangesAsync();

            var protector = Protector();
            var migrator = new DteCertificateProtectionMigrator(
                db, protector, NullLogger<DteCertificateProtectionMigrator>.Instance);

            var report = await migrator.MigrateAsync();

            report.Converted.Should().Be(2);
            report.Protected.Should().Be(2);
            report.Legacy.Should().Be(0);
            (await db.DteConfiguracion.AsNoTracking().SingleAsync()).CertificadoBlob!
                .Should().Match<byte[]>(blob => protector.IsProtectedBytes(blob));
            (await db.DteConfiguracionVersiones.AsNoTracking().SingleAsync()).CertificadoBlob!
                .Should().Match<byte[]>(blob => protector.IsProtectedBytes(blob));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static async Task MigrateWithStartupRetryAsync(NeoStpDbContext db)
    {
        const int attempts = 12;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                return;
            }
            catch (SqlException) when (attempt < attempts)
            {
                SqlConnection.ClearAllPools();
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static DataProtectionSecretProtector Protector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("NeoSTP.Tests.CertificateMigration.SqlServer");
        var provider = services.BuildServiceProvider();
        return new DataProtectionSecretProtector(
            provider.GetRequiredService<IDataProtectionProvider>());
    }
}
