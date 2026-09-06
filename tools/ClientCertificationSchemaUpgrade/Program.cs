using System.Text.Json;
using ClientCertificationSchemaUpgrade;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeoSTP.Infrastructure.Persistence;

if (args.SequenceEqual(new[] { "--self-test" })) return UpgradeSelfTests.Run();
var repo = Path.GetFullPath(Environment.CurrentDirectory);
var run = Guid.NewGuid().ToString("N");
var output = Path.Combine(repo, "tmp/client-schema-upgrade", run, "results.json");
var checks = new List<string>();
var report = new Dictionary<string, object?> { ["AtUtc"] = DateTimeOffset.UtcNow, ["RunId"] = run, ["Passed"] = false,
    ["Checks"] = checks, ["SourceDatabase"] = "NeoSTP_Cloud", ["SqlSchemaChangesStarted"] = false,
    ["HostsChanged"] = false, ["FiscalEnvironmentChanged"] = false, ["CampaignsProvisioned"] = false,
    ["ProviderCalls"] = 0, ["RawRowsExported"] = false, ["AutomaticRollbackOrRestart"] = false };
void Check(bool value, string name) { UpgradePolicy.Require(value, name); checks.Add(name); Console.WriteLine("PASS: " + name); }
try
{
    var apply = UpgradePolicy.ApplyMode(args); report["Mode"] = apply ? "APPLY_ACTIVE_SCHEMA_79_TO_91" : "PREVIEW";
    report["Evidence"] = await UpgradePolicy.Evidence(repo);
    Check(true, "FIXED_CANDIDATE_647_HASHES_AND_2304_PLUS_9_TESTS_AND_TWO_REHEARSALS_VERIFIED");
    var windows = await UpgradePolicy.Windows(repo); report["WindowsPreflight"] = windows;
    if (apply) Check(windows.GetProperty("Passed").GetBoolean(), "HOST_TASKS_DISABLED_ZERO_HOST_PROCESSES_ZERO_LISTENERS");
    using var local = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, "src/NeoSTP.Api/appsettings.Local.json")));
    var connection = new SqlConnectionStringBuilder(local.RootElement.GetProperty("ConnectionStrings").GetProperty("NeoStpDb").GetString());
    Check(connection.InitialCatalog == "NeoSTP_Cloud" && connection.AttachDBFilename.Length == 0
        && new[] { ".", "(local)", "localhost", "127.0.0.1", Environment.MachineName }.Contains(connection.DataSource, StringComparer.OrdinalIgnoreCase), "LOCAL_SQL_CONFIGURATION_TARGET_VERIFIED");
    connection.ApplicationName = "NeoSTP Client Schema Upgrade";
    var options = new DbContextOptionsBuilder<NeoStpDbContext>().UseSqlServer(connection.ConnectionString, sql => sql.CommandTimeout(180)).Options;
    await using var db = new NeoStpDbContext(options);
    await db.Database.OpenConnectionAsync();
    var sql = (SqlConnection)db.Database.GetDbConnection();
    await using (var identity = sql.CreateCommand())
    {
        identity.CommandText = "SELECT DB_NAME(),DB_ID(),CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),CONVERT(int,SERVERPROPERTY('ProductMajorVersion')),SERVERPROPERTY('InstanceName')";
        await using var reader = await identity.ExecuteReaderAsync();
        Check(await reader.ReadAsync() && reader.GetString(0) == "NeoSTP_Cloud" && reader.GetString(2).Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            && reader.GetInt32(3) == 16 && reader.IsDBNull(4), "LOCAL_DEFAULT_SQL_SERVER_16_ACTIVE_DATABASE_IDENTITY_VERIFIED");
        report["DatabaseId"] = Convert.ToInt32(reader.GetValue(1));
    }
    var expected = db.Database.GetMigrations().ToArray();
    var beforeHistory = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    UpgradePolicy.Baseline(beforeHistory, expected); Check(true, "EXACT_79_PREFIX_AND_FIXED_12_PENDING_MIGRATIONS_VERIFIED");
    report["BeforeMigrationCount"] = beforeHistory.Length; report["ExpectedDelta"] = UpgradePolicy.Delta;
    var definitions = await SqlEvidence.Definitions(sql);
    var before = await SqlEvidence.Fingerprints(sql, definitions);
    report["OriginalTableCount"] = definitions.Length; report["OriginalTableDefinitions"] = definitions; report["BeforeFingerprints"] = before;
    report["FingerprintScope"] = "ALL_ORIGINAL_TABLE_ROWS_ALL_TENANTS_AND_GLOBAL_EXCLUDING_ROWVERSION_AND_MIGRATION_HISTORY";
    if (!apply)
    {
        report["ReadyForApply"] = windows.GetProperty("Passed").GetBoolean();
        report["Passed"] = true; report["SqlWritesIssued"] = false;
    }
    else
    {
        var prepared = await UpgradePolicy.Windows(repo, "PrepareDirectory", run);
        var directory = Path.GetFullPath(Path.Combine(UpgradePolicy.BackupRoot, "NeoClientUpgrade_" + run));
        Check(prepared.GetProperty("Passed").GetBoolean() && prepared.GetProperty("Directory").GetString() == directory,
            "FRESH_SERVER_BACKUP_DIRECTORY_RESTRICTED_INHERITED_ACL_VERIFIED");
        var backup = Path.Combine(directory, "source-copy-only.bak");
        report["ProtectedBackupDirectory"] = directory; report["BackupFile"] = backup;
        await using (var backupCommand = sql.CreateCommand())
        {
            backupCommand.CommandTimeout = 180; backupCommand.CommandText = "BACKUP DATABASE [NeoSTP_Cloud] TO DISK=@path WITH COPY_ONLY,CHECKSUM";
            backupCommand.Parameters.AddWithValue("@path", backup); await backupCommand.ExecuteNonQueryAsync();
        }
        report["BackupCompleted"] = true;
        await using (var verify = sql.CreateCommand())
        {
            verify.CommandTimeout = 180; verify.CommandText = "RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM";
            verify.Parameters.AddWithValue("@path", backup); await verify.ExecuteNonQueryAsync();
        }
        report["BackupVerified"] = true; report["BackupSha256"] = UpgradePolicy.Hash(backup); report["BackupBytes"] = new FileInfo(backup).Length;
        report["BackupAcl"] = await UpgradePolicy.Windows(repo, "VerifyDirectory", run);
        var afterBackup = await SqlEvidence.Fingerprints(sql, definitions);
        Check(before.SequenceEqual(afterBackup), "ALL_ORIGINAL_ROWS_UNCHANGED_DURING_FRESH_BACKUP");
        UpgradePolicy.Baseline((await db.Database.GetAppliedMigrationsAsync()).ToArray(), expected);
        Check((await UpgradePolicy.Windows(repo)).GetProperty("Passed").GetBoolean(), "HOSTS_STILL_DISABLED_AND_STOPPED_IMMEDIATELY_BEFORE_MIGRATION");
        // No app/service container. The explicit target prevents applying a future migration accidentally.
        report["SqlSchemaChangesStarted"] = true;
        await db.GetService<IMigrator>().MigrateAsync(UpgradePolicy.Last);
        var afterHistory = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        report["AfterMigrations"] = afterHistory;
        Check(afterHistory.SequenceEqual(expected), "EXACT_91_MIGRATIONS_APPLIED");
        var after = await SqlEvidence.Fingerprints(sql, definitions); report["AfterFingerprints"] = after;
        Check(before.SequenceEqual(after), "ALL_ORIGINAL_ROWS_AND_FIELDS_PRESERVED_EXCEPT_ROWVERSION");
        var errors = await SqlEvidence.Integrity(sql); report["DbccErrors"] = errors;
        Check(errors == 0, "DBCC_CHECKDB_ZERO_ERRORS");
        await using (var empty = sql.CreateCommand())
        {
            empty.CommandText = "SELECT (SELECT COUNT_BIG(*) FROM Dte_CertificationCampaigns),(SELECT COUNT_BIG(*) FROM Dte_CertificationCampaignConsumptions),(SELECT COUNT_BIG(*) FROM Dte_Configuracion WHERE TiposDteAutorizadosCsv IS NOT NULL)";
            await using var reader = await empty.ExecuteReaderAsync(); await reader.ReadAsync();
            Check(reader.GetInt64(0) == 0 && reader.GetInt64(1) == 0 && reader.GetInt64(2) == 0, "SCHEMA_ONLY_NO_CAMPAIGNS_OR_TENANT_CONFIGURATION_PROVISIONED");
        }
        report["WindowsAfter"] = await UpgradePolicy.Windows(repo);
        Check(((JsonElement)report["WindowsAfter"]!).GetProperty("Passed").GetBoolean(), "HOSTS_REMAIN_DISABLED_AND_STOPPED_FOR_REVIEW");
        report["Passed"] = true;
    }
}
catch (Exception error)
{
    report["FailureType"] = error.GetType().Name; report["FailureCode"] = error is UpgradeRejected rejected ? rejected.Code : "UPGRADE_REVIEW_REQUIRED";
    if (error is SqlException sql) report["SqlErrorNumber"] = sql.Number;
    report["Passed"] = false;
    Console.Error.WriteLine("Schema operation stopped. No automatic rollback or host restart; inspect sanitized report and retained backup.");
}
finally
{
    report["CompletedAtUtc"] = DateTimeOffset.UtcNow;
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Evidence: " + output);
}
return report["Passed"] is true ? 0 : 1;
