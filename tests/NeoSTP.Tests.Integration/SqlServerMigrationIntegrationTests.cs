using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Integration;

/// <summary>
/// Gate de Release Candidate sobre SQL Server real. Se activa únicamente cuando
/// NEOSTP_SQLSERVER_TEST_CONNECTION apunta a una instancia efímera autorizada.
/// </summary>
public sealed class SqlServerMigrationIntegrationTests
{
    private const string ConnectionVariable = "NEOSTP_SQLSERVER_TEST_CONNECTION";

    [SqlServerFact]
    public async Task Migrations_create_the_expected_schema_and_constraints()
    {
        var rootConnection = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var databaseName = $"NeoSTP_CI_{Guid.NewGuid():N}";
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

            var known = db.Database.GetMigrations().ToArray();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();

            known.Should().NotBeEmpty();
            applied.Should().Equal(known);
            pending.Should().BeEmpty();

            (await ScalarAsync(db, """
                SELECT COUNT(*)
                FROM sys.tables
                WHERE name IN ('Core_Empresas', 'Core_Usuarios', 'Dte_Documentos', 'Pos_Ventas')
                """)).Should().Be(4);

            (await ScalarAsync(db, """
                SELECT COUNT(*)
                FROM sys.foreign_keys
                WHERE is_disabled = 0
                """)).Should().BeGreaterThan(0);

            (await ScalarAsync(db, """
                SELECT COUNT(*)
                FROM sys.indexes AS i
                INNER JOIN sys.tables AS t ON t.object_id = i.object_id
                WHERE i.is_unique = 1
                  AND t.name IN ('Core_UsuarioEmpresas', 'Dte_Documentos')
                """)).Should().BeGreaterThanOrEqualTo(3);
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

    private static async Task<int> ScalarAsync(NeoStpDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEOSTP_SQLSERVER_TEST_CONNECTION")))
            Skip = "Requires an authorized ephemeral SQL Server via NEOSTP_SQLSERVER_TEST_CONNECTION.";
    }
}
