using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;

// Existing protected restore ONLY. No hosts, workers, external providers, seeding or active database writes.
var repo = Path.GetFullPath(Environment.CurrentDirectory);
var clientCertification = args.SequenceEqual(new[] { "--client-certification" });
if (args.Length != 0 && !clientCertification) throw new ArgumentException("Unknown rehearsal mode.");
var expectedCount = clientCertification ? 91 : 89;
var expectedLast = clientCertification ? "20260905221056_CERT2_TenantDteTypeAuthorization" : "20260905150044_GL1H_AtomicPaymentApplication";
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
var evidenceDirectory = clientCertification ? "tmp/client-certification-release-2026-09-05" : "tmp/neo-production";
var manifestPath = Path.Combine(repo, evidenceDirectory, "clone-manifest.json");
var evidencePath = Path.Combine(repo, evidenceDirectory, "rehearsal-results.json");
var manifest = JsonSerializer.Deserialize<CloneManifest>(await File.ReadAllTextAsync(manifestPath), jsonOptions)
    ?? throw new InvalidOperationException("Clone manifest missing.");
if (!Regex.IsMatch(manifest.RunId, "^[a-f0-9]{32}$") || manifest.TargetDatabase != "NeoProductionAudit_" + manifest.RunId
    || manifest.SourceDatabase != "NeoSTP_Cloud" || !manifest.Restored || !manifest.Verified || !manifest.BackupCompleted || !manifest.SourceUnchanged || !manifest.CloneMatchesSourceBefore)
    throw new InvalidOperationException("Protected clone identity is not approved.");
var allowedDirectory = Path.GetFullPath(Path.Combine(@"C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup", manifest.TargetDatabase));
if (!string.Equals(Path.GetFullPath(manifest.ProtectedDirectory), allowedDirectory, StringComparison.OrdinalIgnoreCase)
    || Path.GetFullPath(manifest.DataFile) != Path.Combine(allowedDirectory, manifest.TargetDatabase + ".mdf")
    || Path.GetFullPath(manifest.LogFile) != Path.Combine(allowedDirectory, manifest.TargetDatabase + ".ldf"))
    throw new InvalidOperationException("Clone file identity mismatch.");
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, "src/NeoSTP.Api/appsettings.Local.json")));
var sourceBuilder = new SqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("NeoStpDb").GetString());
if (sourceBuilder.InitialCatalog != manifest.SourceDatabase) throw new InvalidOperationException("Source identity changed.");
var cloneBuilder = new SqlConnectionStringBuilder(sourceBuilder.ConnectionString) { InitialCatalog = manifest.TargetDatabase };
await using var source = new SqlConnection(sourceBuilder.ConnectionString);
await using var clone = new SqlConnection(cloneBuilder.ConnectionString);
var checks = new List<string>();
string? failedCheck = null;
void Check(bool condition, string name)
{
    if (!condition) { failedCheck = name; throw new InvalidOperationException(name); }
    checks.Add(name); Console.WriteLine("PASS: " + name);
}
var outcome = new Dictionary<string, object?> { ["GeneratedAtUtc"] = DateTime.UtcNow, ["TargetDatabase"] = manifest.TargetDatabase,
    ["BackupRetained"] = true, ["CloneRetained"] = true, ["RawRowsExported"] = false, ["Checks"] = checks };
try
{
    await source.OpenAsync(); await clone.OpenAsync();
    await AssertClone();
    var beforeSource = await Fingerprints(source);
    var beforeClone = await Fingerprints(clone);
    Check(Equal(beforeClone, manifest.CloneBeforeMigrations), "Restored clone still matches the approved pre-migration fingerprint baseline");
    Check(Equal(beforeSource, manifest.SourceAfter), "Active source remained unchanged since protected restore");
    Check(Equal(beforeClone, manifest.SourceAfter), "Restored clone fingerprints directly equal the verified source baseline");
    outcome["SourceBefore"] = beforeSource;
    var writeBlocker = new ReadOnlyStartupInterceptor();
    Check(!await StartupReady(writeBlocker), "Production startup rejects 79-migration clone without attempting automatic repair");
    Check(writeBlocker.WriteAttempts == 0 && Equal(beforeClone, await Fingerprints(clone)),
        "Rejected Production startup performs only read commands and preserves restored data");
    await using var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>().UseSqlServer(cloneBuilder.ConnectionString,
        sql => sql.CommandTimeout(180)).Options);
    Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 79, "Protected restored clone has exactly 79 source migrations");
    Check((await db.Database.GetPendingMigrationsAsync()).Count() == expectedCount - 79, $"Exact reviewed delta consists of {expectedCount - 79} migrations");
    await AssertClone();
    await db.Database.MigrateAsync();
    var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    outcome["AppliedMigrations"] = applied;
    Check(applied.Length == expectedCount && applied[^1] == expectedLast, $"Protected same-engine clone migrates from 79 to {expectedCount}");
    var afterMigration = await Fingerprints(clone);
    Check(Equal(beforeClone, afterMigration), "Reviewed migrations preserve original fields except rowversion across direct-EmpresaId tables for NEO 2 and client 23");
    Check(await StartupReady(writeBlocker), $"Production startup accepts exact {expectedCount}-migration clone without migration or seeding");
    Check(writeBlocker.WriteAttempts == 0 && Equal(afterMigration, await Fingerprints(clone)),
        "Accepted Production startup performs no writes or identity bootstrap");
    var integrityRows = 0;
    var integrityErrors = 0;
    await using (var integrity = clone.CreateCommand())
    {
        integrity.CommandTimeout = 180;
        integrity.CommandText = "DBCC CHECKDB (" + Quote(manifest.TargetDatabase) + ") WITH TABLERESULTS, NO_INFOMSGS, ALL_ERRORMSGS";
        await using var integrityReader = await integrity.ExecuteReaderAsync();
        do
        {
            var levelColumn = Enumerable.Range(0, integrityReader.FieldCount).FirstOrDefault(i => integrityReader.GetName(i).Equals("Level", StringComparison.OrdinalIgnoreCase), -1);
            while (await integrityReader.ReadAsync())
            {
                integrityRows++;
                if (levelColumn < 0 || integrityReader.IsDBNull(levelColumn) || Convert.ToInt32(integrityReader.GetValue(levelColumn)) >= 11) integrityErrors++;
            }
        } while (await integrityReader.NextResultAsync());
    }
    outcome["DbccReturnedRows"] = integrityRows; outcome["DbccErrorRows"] = integrityErrors;
    Check(integrityErrors == 0, "DBCC CHECKDB table results report zero errors on migrated protected clone");

    // The only deliberate business-data edit in the rehearsal is NEO 2 fiscal configuration on this clone.
    await AssertClone();
    var changed = await db.DteConfiguracion.Where(x => x.EmpresaId == 2).ExecuteUpdateAsync(s => s
        .SetProperty(x => x.AmbienteCodigo, "PRODUCCION").SetProperty(x => x.PasswordMhCifrado, (string?)null)
        .SetProperty(x => x.TokenMhCifrado, (string?)null).SetProperty(x => x.TokenMhExpiraAt, (DateTime?)null));
    Check(changed == 1, "Rehearsal switches only cloned NEO 2 configuration and clears test password and cached token");
    var neoConfig = await db.DteConfiguracion.AsNoTracking().SingleAsync(x => x.EmpresaId == 2);
    Check(neoConfig.AmbienteCodigo == "PRODUCCION" && neoConfig.PasswordMhCifrado is null && neoConfig.TokenMhCifrado is null
        && neoConfig.TokenMhExpiraAt is null, "Cloned production configuration cannot silently reuse test authentication material");
    Check(DteFiscalContext.Validar("PRUEBAS", neoConfig).ErrorCode == "DTE_AMBIENTE_INCOMPATIBLE",
        "Existing test-document context is rejected against cloned production configuration");
    Check(await db.DteConfiguracion.AsNoTracking().AnyAsync(x => x.EmpresaId == 23 && x.AmbienteCodigo == "PRUEBAS"),
        "Client 23 remains in PRUEBAS in the rehearsal clone");
    var afterRehearsal = await Fingerprints(clone);
    Check(Equal(beforeClone.Where(x => x.EmpresaId == 23), afterRehearsal.Where(x => x.EmpresaId == 23)),
        "Client 23 original data balances and licenses retain identical tenant-table fingerprints");
    Check(Equal(beforeClone.Where(x => x.EmpresaId == 2 && x.TableName != "Dte_Configuracion"),
        afterRehearsal.Where(x => x.EmpresaId == 2 && x.TableName != "Dte_Configuracion")),
        "NEO original test documents and all other original tenant data remain immutable");
    var resolver = new EmpresasService(db, new NoAudit());
    var neoLicense = await resolver.ResolveAsync(2);
    var clientLicense = await resolver.ResolveAsync(23);
    Check(neoLicense?.Vigente == true && string.Equals(neoLicense.PlanCodigo, "ENTERPRISE", StringComparison.OrdinalIgnoreCase)
        && neoLicense.Modulos.Count(x => x.Activo) == 18, "Migrated NEO resolves valid Enterprise with 18 active modules");
    Check(clientLicense?.Vigente == true && string.Equals(clientLicense.PlanCodigo, "STARTERFE", StringComparison.OrdinalIgnoreCase)
        && clientLicense.Modulos.Where(x => x.Activo).Select(x => x.Codigo).Order().SequenceEqual(new[] { "CORE", "NEODTE" }),
        "Client 23 resolves valid STARTERFE with exactly CORE and NEODTE active");
    var sourceAfter = await Fingerprints(source);
    Check(Equal(beforeSource, sourceAfter), "Active source tenant data remain unchanged throughout migration and fiscal rehearsal");
    await using (var sourceState = source.CreateCommand())
    {
        sourceState.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
        Check(Convert.ToInt32(await sourceState.ExecuteScalarAsync()) == 79, "Active source retains 79 migrations");
    }
    outcome["SourceAfter"] = sourceAfter; outcome["CloneAfterRehearsal"] = afterRehearsal;
    outcome["Passed"] = checks.Count; outcome["Failed"] = 0;
    Console.WriteLine($"Completed: {checks.Count}/{checks.Count}. Only protected clone was migrated/rehearsed; source unchanged.");
}
catch (Exception exception)
{
    outcome["Passed"] = checks.Count; outcome["Failed"] = 1; outcome["FailureType"] = exception.GetType().Name; outcome["FailureCheck"] = failedCheck;
    // Never expose provider SQL exceptions, restored customer rows, credentials or full configuration.
    Console.WriteLine("REHEARSAL_FAILED type=" + exception.GetType().Name + "; protected clone retained for review.");
    Environment.ExitCode = 1;
}
finally
{
    Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
    await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(outcome, jsonOptions));
}

async Task AssertClone()
{
    if (clone.Database != manifest.TargetDatabase || clone.Database == sourceBuilder.InitialCatalog || clone.DataSource != source.DataSource)
        throw new InvalidOperationException("Clone connection guard rejected target.");
    await using var command = clone.CreateCommand();
    command.CommandText = "SELECT DB_NAME(),CAST(SERVERPROPERTY('ProductMajorVersion') AS int); SELECT physical_name FROM sys.database_files";
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync() || reader.GetString(0) != manifest.TargetDatabase || reader.GetInt32(1) != 16)
        throw new InvalidOperationException("Clone server identity rejected.");
    await reader.NextResultAsync();
    var files = new List<string>();
    while (await reader.ReadAsync()) files.Add(Path.GetFullPath(reader.GetString(0)));
    if (files.Count != 2 || !files.Contains(Path.GetFullPath(manifest.DataFile), StringComparer.OrdinalIgnoreCase)
        || !files.Contains(Path.GetFullPath(manifest.LogFile), StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("Clone physical file guard rejected target.");
}
async Task<bool> StartupReady(ReadOnlyStartupInterceptor interceptor)
{
    var services = new ServiceCollection();
    services.AddDbContext<NeoStpDbContext>(builder => builder.UseSqlServer(cloneBuilder.ConnectionString).AddInterceptors(interceptor));
    await using var provider = services.BuildServiceProvider();
    try
    {
        await DatabaseStartup.InitializeAsync(provider, new ConfigurationBuilder().Build(), new ProductionEnvironment());
        return true;
    }
    catch (InvalidOperationException exception) when (exception.Message.StartsWith("DATABASE_SCHEMA_NOT_READY", StringComparison.Ordinal)) { return false; }
}
async Task<Fingerprint[]> Fingerprints(SqlConnection connection)
{
    var result = new List<Fingerprint>();
    foreach (var table in manifest.TableDefinitions)
    {
        var qualified = Quote(table.SchemaName) + "." + Quote(table.TableName);
        var columns = string.Join(',', table.Columns.Select(Quote));
        var order = string.Join(',', table.KeyColumns.Select(Quote));
        if (table.KeyColumns.Length == 0) throw new InvalidOperationException("Fingerprint ordering missing.");
        foreach (var tenant in new[] { 2, 23 })
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 60;
            command.CommandText = $"SELECT COUNT_BIG(*) [RowCount],CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT {columns} FROM {qualified} WHERE EmpresaId=@tenant ORDER BY {order} FOR JSON PATH,INCLUDE_NULL_VALUES)),2) Digest FROM {qualified} WHERE EmpresaId=@tenant";
            command.Parameters.AddWithValue("@tenant", tenant);
            await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
            result.Add(new(table.SchemaName, table.TableName, tenant, reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        }
    }
    return result.ToArray();
}
static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";
static bool Equal(IEnumerable<Fingerprint> left, IEnumerable<Fingerprint> right)
    => left.OrderBy(x => x.SchemaName).ThenBy(x => x.TableName).ThenBy(x => x.EmpresaId)
        .SequenceEqual(right.OrderBy(x => x.SchemaName).ThenBy(x => x.TableName).ThenBy(x => x.EmpresaId));
sealed record Fingerprint(string SchemaName, string TableName, int EmpresaId, long RowCount, string? Digest);
sealed record TableDefinition(string SchemaName, string TableName, string[] Columns, string[] KeyColumns);
sealed record CloneManifest(string RunId, string SourceDatabase, string TargetDatabase, string ProtectedDirectory,
    string DataFile, string LogFile, bool BackupCompleted, bool Verified, bool Restored, bool SourceUnchanged, bool CloneMatchesSourceBefore,
    TableDefinition[] TableDefinitions, Fingerprint[] CloneBeforeMigrations, Fingerprint[] SourceAfter);
sealed class NoAudit : IAuditoriaService
{
    public Task RegistrarAsync(AuditoriaEvent evento, CancellationToken ct = default) => throw new InvalidOperationException("Unexpected audit mutation in read-only resolver.");
}
sealed class ProductionEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;
    public string ApplicationName { get; set; } = "ProductionReadinessSqlVerification";
    public string ContentRootPath { get; set; } = Environment.CurrentDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
sealed class ReadOnlyStartupInterceptor : DbCommandInterceptor
{
    public int WriteAttempts { get; private set; }
    private void Validate(DbCommand command)
    {
        if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) return;
        WriteAttempts++;
        throw new InvalidOperationException("Production startup attempted a non-read command.");
    }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) { Validate(command); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Validate(command); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default) { Validate(command); return ValueTask.FromResult(result); }
}
