using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Persistence;

// Synthetic SQL verification only. No host, DI providers, migrations, customer queries or fiscal calls.
if (args.Length != 0) throw new ArgumentException("No arguments accepted; the SQL target is generated internally.");
var repo = Path.GetFullPath(Environment.CurrentDirectory);
var runId = Guid.NewGuid().ToString("N");
var database = "NeoCertificationAudit_" + runId;
var directory = Path.Combine(repo, "tmp", "certification-sql", runId);
Directory.CreateDirectory(directory);
var checks = new List<string>();
var report = new Dictionary<string, object?> {
    ["StartedAtUtc"] = DateTimeOffset.UtcNow, ["Database"] = database, ["SyntheticDataOnly"] = true,
    ["CustomerDatabaseOpened"] = false, ["HaciendaCalls"] = 0, ["MigrationsApplied"] = false,
    ["Checks"] = checks, ["Passed"] = false, ["DatabaseRemoved"] = false
};
var owned = false;
int? databaseId = null;
SqlConnection? master = null;
void Check(bool condition, string name)
{
    if (!condition) { report["FailedCheck"] = name; throw new InvalidOperationException("Verification assertion failed."); }
    checks.Add(name); Console.WriteLine("PASS: " + name);
}
try
{
    using var local = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, "src/NeoSTP.Api/appsettings.Local.json")));
    var builder = new SqlConnectionStringBuilder(local.RootElement.GetProperty("ConnectionStrings").GetProperty("NeoStpDb").GetString());
    Check(builder.InitialCatalog == "NeoSTP_Cloud" && new[] { ".", "(local)", "localhost", "127.0.0.1", Environment.MachineName }
        .Contains(builder.DataSource, StringComparer.OrdinalIgnoreCase) && !builder.AttachDBFilename.Any(), "Local source configuration identity checked before opening master");
    builder.InitialCatalog = "master";
    builder.ApplicationName = "NeoSTP Certification Synthetic Verification";
    builder.ConnectTimeout = 10;
    master = new SqlConnection(builder.ConnectionString);
    await master.OpenAsync();
    await using (var identity = master.CreateCommand())
    {
        identity.CommandText = "SELECT CONVERT(int,SERVERPROPERTY('ProductMajorVersion')), CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')), DB_NAME()";
        await using var reader = await identity.ExecuteReaderAsync(); await reader.ReadAsync();
        Check(reader.GetInt32(0) == 16 && reader.GetString(1).Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            && reader.GetString(2) == "master", "SQL Server 16 on this Windows machine verified");
        report["SqlMajorVersion"] = reader.GetInt32(0);
    }
    Check(Regex.IsMatch(database, "\\ANeoCertificationAudit_[a-f0-9]{32}\\z") && database != "NeoSTP_Cloud", "Synthetic database name is fixed prefix plus owned GUID");
    await using (var create = master.CreateCommand())
    {
        create.CommandText = $"CREATE DATABASE [{database}]";
        await create.ExecuteNonQueryAsync(); owned = true;
    }
    await using (var identity = master.CreateCommand())
    {
        identity.CommandText = "SELECT DB_ID(@name)"; identity.Parameters.AddWithValue("@name", database);
        databaseId = Convert.ToInt32(await identity.ExecuteScalarAsync());
    }
    builder.InitialCatalog = database;
    var options = new DbContextOptionsBuilder<NeoStpDbContext>().UseSqlServer(builder.ConnectionString, sql => sql.CommandTimeout(120)).Options;
    NeoStpDbContext Db() => new(options);
    await using var schema = Db();
    await schema.Database.EnsureCreatedAsync();
    Check(await schema.Empresas.CountAsync() == 0 && await schema.CertificationCampaigns.CountAsync() == 0, "Synthetic schema has no companies or campaigns; application seeder and migrations not invoked");
    report["ModelStaticCatalogSeedIncluded"] = true;
    var clock = new FixedClock();
    var company = new Empresa { Nit = "00000000000001", RazonSocial = "SYNTHETIC CERTIFICATION SQL" };
    var foreign = new Empresa { Nit = "00000000000002", RazonSocial = "SYNTHETIC FOREIGN TENANT" };
    var plan = new Plan { Codigo = "CERT_SQL", Nombre = "Synthetic license", LimiteDteMensual = 100, PrecioMensual = 15 };
    var core = await schema.Modulos.SingleAsync(x => x.Codigo == "CORE");
    var dte = await schema.Modulos.SingleAsync(x => x.Codigo == "NEODTE");
    schema.AddRange(company, foreign, plan); await schema.SaveChangesAsync();
    schema.AddRange(new DteConfiguracion { EmpresaId = company.Id, AmbienteCodigo = "PRUEBAS" },
        new DteConfiguracion { EmpresaId = foreign.Id, AmbienteCodigo = "PRUEBAS" },
        new EmpresaPlan { EmpresaId = company.Id, PlanId = plan.Id, FechaInicio = clock.Now.UtcDateTime.AddDays(-1), FechaFin = clock.Now.UtcDateTime.AddDays(5) });
    foreach (var module in new[] { core, dte })
        schema.AddRange(new PlanModulo { PlanId = plan.Id, ModuloId = module.Id }, new EmpresaModulo { EmpresaId = company.Id, ModuloId = module.Id });
    await schema.SaveChangesAsync();
    async Task<CertificationCampaign> Campaign(int budget01 = 1, int budget03 = 0)
    {
        var campaign = new CertificationCampaign { EmpresaId = company.Id, ExpectedNit = company.Nit, Status = "ACTIVE",
            StartsAtUtc = clock.Now.AddHours(-1), ExpiresAtUtc = clock.Now.AddDays(1), TotalBudget = budget01 + budget03,
            MatrixReference = "SYNTHETIC-NOT-OFFICIAL-MATRIX", CreatedBy = "synthetic-verifier" };
        campaign.TypeBudgets.Add(new() { TipoDteCodigo = "01", Budget = budget01 });
        if (budget03 > 0) campaign.TypeBudgets.Add(new() { TipoDteCodigo = "03", Budget = budget03 });
        schema.Add(campaign); await schema.SaveChangesAsync(); return campaign;
    }
    CertificationConsumptionRequest Request(CertificationCampaign campaign, string key, string type = "01")
        => new(campaign.PublicId, company.Id, company.Nit, type, key, new string('A', 64), "synthetic-scenario", "synthetic-verifier");
    DteDocumento Document(CertificationConsumptionRequest request) => new() {
        EmpresaId = request.EmpresaId, TipoDteCodigo = request.TipoDteCodigo, AmbienteCodigo = "PRUEBAS", EstadoCodigo = "BORRADOR",
        NumeroControl = "DTE-" + Guid.NewGuid().ToString("N"), CodigoGeneracion = Guid.NewGuid().ToString("D")
    };
    async Task<Result<CertificationConsumptionResult>> Reserve(CertificationConsumptionRequest request, bool rollback = false)
    {
        await using var db = Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await new CertificationCampaignQuotaService(db, clock).StageConsumptionAsync(request, Document(request));
        if (result.IsSuccess && !result.Value!.Replayed) await db.SaveChangesAsync();
        if (rollback || result.IsFailure) await tx.RollbackAsync(); else await tx.CommitAsync();
        return result;
    }
    var raceCampaign = await Campaign();
    var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var racing = Enumerable.Range(0, 12).Select(async i => { await start.Task; return await Reserve(Request(raceCampaign, "race-" + i)); }).ToArray();
    start.SetResult();
    var race = await Task.WhenAll(racing);
    Check(race.Count(x => x.IsSuccess) == 1 && race.Count(x => x.ErrorCode == "CERT_CAMPAIGN_EXHAUSTED") == 11,
        "Twelve concurrent distinct requests compete for one final allowance: exactly one commit");
    Check(await schema.CertificationCampaignConsumptions.CountAsync(x => x.CampaignId == raceCampaign.Id) == 1,
        "Last allowance race persists exactly one consumption");
    var replayCampaign = await Campaign();
    var replayRequests = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Reserve(Request(replayCampaign, "same-key"))));
    Check(replayRequests.All(x => x.IsSuccess) && replayRequests.Count(x => !x.Value!.Replayed) == 1
        && replayRequests.Select(x => x.Value!.Consumption.DteDocumentoId).Distinct().Count() == 1,
        "Twelve concurrent identical keys return one committed DTE and eleven read replays");
    Check((await Reserve(Request(replayCampaign, "same-key") with { PayloadHash = new string('B', 64) })).ErrorCode == "IDEMPOTENCY_CONFLICT",
        "Committed key with a changed payload is rejected");
    var rollbackCampaign = await Campaign();
    Check((await Reserve(Request(rollbackCampaign, "rollback-key"), rollback: true)).IsSuccess, "Draft and consumption staged then rolled back together");
    Check(await schema.CertificationCampaignConsumptions.CountAsync(x => x.CampaignId == rollbackCampaign.Id) == 0,
        "Rolled back transaction leaves no allowance consumption");
    var afterRollback = await Reserve(Request(rollbackCampaign, "rollback-key"));
    Check(afterRollback.IsSuccess && !afterRollback.Value!.Replayed, "Same key after rollback creates a fresh draft using the restored allowance");
    var types = await Campaign(1, 2);
    Check((await Reserve(Request(types, "type01"))).IsSuccess
        && (await Reserve(Request(types, "type01-over"))).ErrorCode == "CERT_CAMPAIGN_EXHAUSTED"
        && (await Reserve(Request(types, "type03", "03"))).IsSuccess, "Per-type allowance cannot borrow remaining capacity from another type");
    foreach (var state in new[] { "expired", "revoked" })
    {
        var campaign = await Campaign(2);
        Check((await Reserve(Request(campaign, "committed"))).IsSuccess, state + ": original committed allowance exists");
        if (state == "expired") await schema.CertificationCampaigns.Where(x => x.Id == campaign.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, clock.Now));
        else await schema.CertificationCampaigns.Where(x => x.Id == campaign.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "REVOKED"));
        Check((await Reserve(Request(campaign, "new-key"))).ErrorCode == "CERT_CAMPAIGN_INACTIVE", state + ": new allowance rejected");
        var old = await Reserve(Request(campaign, "committed"));
        Check(old.IsSuccess && old.Value!.Replayed, state + ": original request remains a read replay, not transmission permission");
    }
    var protectedCampaign = await Campaign(2);
    Check((await Reserve(Request(protectedCampaign, "foreign") with { EmpresaId = foreign.Id, ExpectedNit = foreign.Nit })).ErrorCode == "CERT_CAMPAIGN_FORBIDDEN",
        "Foreign tenant cannot spend another company campaign");
    Check((await Reserve(Request(protectedCampaign, "wrong-nit") with { ExpectedNit = foreign.Nit })).ErrorCode == "CERT_CAMPAIGN_FORBIDDEN",
        "Mismatched expected NIT rejected");
    await schema.DteConfiguracion.Where(x => x.EmpresaId == company.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AmbienteCodigo, "PRODUCCION"));
    Check((await Reserve(Request(protectedCampaign, "production"))).ErrorCode == "CERT_CAMPAIGN_FORBIDDEN", "Production configuration cannot consume test allowance");
    await schema.DteConfiguracion.Where(x => x.EmpresaId == company.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AmbienteCodigo, "PRUEBAS"));
    await schema.EmpresaPlanes.Where(x => x.EmpresaId == company.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.FechaFin, clock.Now.UtcDateTime));
    Check((await Reserve(Request(protectedCampaign, "license-expired"))).ErrorCode == "LICENSE_INVALID", "Campaign does not bypass expired commercial license");
    await using (var db = Db())
    {
        var req = Request(protectedCampaign, "no-transaction");
        Check((await new CertificationCampaignQuotaService(db, clock).StageConsumptionAsync(req, Document(req))).ErrorCode == "CERT_CAMPAIGN_TRANSACTION_REQUIRED",
            "Relational staging without caller transaction rejected");
    }
    Check(await schema.DteDocumentos.CountAsync() == await schema.CertificationCampaignConsumptions.CountAsync(), "Every persisted synthetic draft has exactly one consumption after races and rollback");
    Check((await schema.Planes.SingleAsync(x => x.Id == plan.Id)).LimiteDteMensual == 100 && await schema.BillingPayments.CountAsync() == 0,
        "Verification does not change commercial quota or generate billing payments");
    report["Passed"] = true;
}
catch (Exception error)
{
    report["ErrorType"] = error.GetType().Name;
    if (error is SqlException sql) report["SqlErrorNumber"] = sql.Number;
    if (error.InnerException is SqlException innerSql) report["InnerSqlErrorNumber"] = innerSql.Number;
    Console.Error.WriteLine("Verification failed; see sanitized evidence for assertion/type.");
    Environment.ExitCode = 1;
}
finally
{
    if (owned && master is not null)
    {
        try
        {
            SqlConnection.ClearAllPools();
            if (master.State != ConnectionState.Open) await master.OpenAsync();
            await using var drop = master.CreateCommand();
            drop.CommandText = $"IF DB_ID(@name) <> @expected THROW 50001, 'Owned database identity changed.', 1; ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];";
            drop.Parameters.AddWithValue("@name", database); drop.Parameters.AddWithValue("@expected", databaseId ?? -1);
            await drop.ExecuteNonQueryAsync();
            await using var verify = master.CreateCommand(); verify.CommandText = "SELECT DB_ID(@name)"; verify.Parameters.AddWithValue("@name", database);
            Check(await verify.ExecuteScalarAsync() is DBNull, "Owned synthetic database removed and absence verified");
            report["DatabaseRemoved"] = true;
        }
        catch (Exception cleanup) { report["CleanupErrorType"] = cleanup.GetType().Name; Environment.ExitCode = 1; report["Passed"] = false; }
    }
    if (master is not null) await master.DisposeAsync();
    report["CompletedAtUtc"] = DateTimeOffset.UtcNow;
    var hashPaths = new[] { "tools/CertificationSqlVerification/Program.cs", "tools/CertificationSqlVerification/CertificationSqlVerification.csproj", "src/NeoSTP.Infrastructure/Dte/Certificacion/CertificationCampaignQuotaService.cs", "src/NeoSTP.Infrastructure/Persistence/Configurations/CertificationCampaignConfiguration.cs" };
    report["SourceSha256"] = hashPaths.ToDictionary(x => x, x => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo, x)))));
    await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Evidence: " + Path.Combine(directory, "results.json"));
}

sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
