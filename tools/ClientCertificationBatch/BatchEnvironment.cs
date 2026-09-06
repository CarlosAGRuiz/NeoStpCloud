using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NeoSTP.Infrastructure.Persistence;

namespace ClientCertificationBatch;

public sealed record BatchEnvironment(string Repo, string ReleaseId, IConfigurationRoot Configuration,
    DbContextOptions<NeoStpDbContext> Options, string InfrastructureHash, string ToolHash,IReadOnlyDictionary<string,string> RuntimeHashes)
{
    public NeoStpDbContext Db() => new(Options);
    public static BatchEnvironment Load(string repo)
    {
        repo = Path.GetFullPath(repo);
        var wrapper = SafePath(repo,"tools/ClientCertificationDeployment/Start-StagedHost.ps1");
        var text = File.ReadAllText(wrapper); // Read literals only; never execute or dot-source the wrapper.
        var ids = Regex.Matches(text,@"out/client-certification-release/(?<id>[0-9]{8}T[0-9]{6}Z-[a-f0-9]{32})'");
        var hashes = Regex.Matches(text,@"CandidateManifestSha256-cne'(?<hash>[A-F0-9]{64})'");
        BatchPolicy.Require(ids.Count == 1 && hashes.Count == 1,"BATCH_ACTIVE_RELEASE_SELECTOR_REJECTED");
        var id = ids[0].Groups["id"].Value;
        var root = SafePath(repo,"out/client-certification-release/"+id);
        using var staged = JsonDocument.Parse(File.ReadAllText(SafePath(root,"staging-manifest.json")));
        BatchPolicy.Require(staged.RootElement.GetProperty("CandidateId").GetString() == id
            && staged.RootElement.GetProperty("CandidateManifestSha256").GetString() == hashes[0].Groups["hash"].Value,"BATCH_STAGED_MANIFEST_REJECTED");
        var candidate = SafePath(repo,"tmp/production-candidate/"+id+"/candidate-manifest.json");
        BatchPolicy.Require(BatchPolicy.FileHash(candidate) == hashes[0].Groups["hash"].Value,"BATCH_CANDIDATE_CHANGED");
        using var manifest = JsonDocument.Parse(File.ReadAllText(candidate));
        foreach(var entry in manifest.RootElement.GetProperty("Files").EnumerateArray().Where(x=>x.GetProperty("Root").GetString()=="api"))
            BatchPolicy.Require(BatchPolicy.FileHash(SafePath(root,"api/"+entry.GetProperty("Path").GetString())) == entry.GetProperty("Sha256").GetString(),"BATCH_ACTIVE_BINARY_CHANGED");
        var infrastructureHash = BatchPolicy.FileHash(typeof(NeoStpDbContext).Assembly.Location);
        BatchPolicy.Require(infrastructureHash == BatchPolicy.FileHash(SafePath(root,"api/NeoSTP.Infrastructure.dll")),"BATCH_GENERATOR_DIFFERS_FROM_ACTIVE_RELEASE");
        var runtimeHashes=new Dictionary<string,string>();
        foreach(var assembly in new[]{typeof(NeoStpDbContext).Assembly,typeof(NeoSTP.Application.Dte.DteCalculator).Assembly,typeof(NeoSTP.Domain.Core.Dte.DteDocumento).Assembly})
        {
            var name=Path.GetFileName(assembly.Location); var hash=BatchPolicy.FileHash(assembly.Location);
            BatchPolicy.Require(hash==BatchPolicy.FileHash(SafePath(root,"api/"+name)),"BATCH_RUNTIME_DIFFERS_FROM_ACTIVE_RELEASE");
            runtimeHashes.Add(name,hash);
        }
        runtimeHashes.Add("ClientCertificationRunner.dll",BatchPolicy.FileHash(typeof(ClientCertificationRunner.PilotFixture).Assembly.Location));
        var configuration = new ConfigurationBuilder().SetBasePath(SafePath(root,"api"))
            .AddJsonFile("appsettings.json",false).AddJsonFile("appsettings.Development.json",false).AddJsonFile("appsettings.Local.json",false).Build();
        BatchPolicy.Require(!configuration.GetValue<bool>("Dte:EsquemaNuevo")
            && BatchPolicy.Normalize(configuration["Dte:TenantSchemas:23:Nit"])==BatchPolicy.Nit
            && configuration["Dte:TenantSchemas:23:Ambiente"]=="PRUEBAS"
            && configuration["Dte:TenantSchemas:23:Profile"]=="MH_20260825","BATCH_TENANT_SCHEMA_POLICY_REQUIRED");
        var connection = new SqlConnectionStringBuilder(configuration.GetConnectionString("NeoStpDb"));
        BatchPolicy.Require(connection.InitialCatalog=="NeoSTP_Cloud" && connection.AttachDBFilename.Length==0
            && new[]{".","(local)","localhost","127.0.0.1",Environment.MachineName}.Contains(connection.DataSource,StringComparer.OrdinalIgnoreCase),"BATCH_SQL_TARGET_REJECTED");
        connection.ApplicationName="NeoSTP Client Certification Batch";
        return new(repo,id,configuration,new DbContextOptionsBuilder<NeoStpDbContext>().UseSqlServer(connection.ConnectionString).Options,
            infrastructureHash,BatchPolicy.FileHash(typeof(BatchEnvironment).Assembly.Location),runtimeHashes);
    }
    public async Task ValidateSqlAsync(NeoStpDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command=db.Database.GetDbConnection().CreateCommand();
        command.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),CONVERT(int,SERVERPROPERTY('ProductMajorVersion')),SERVERPROPERTY('InstanceName')";
        await using(var r=await command.ExecuteReaderAsync())
            BatchPolicy.Require(await r.ReadAsync() && r.GetString(0)=="NeoSTP_Cloud" && r.GetString(1).Equals(Environment.MachineName,StringComparison.OrdinalIgnoreCase)
                && r.GetInt32(2)==16 && r.IsDBNull(3),"BATCH_SQL_IDENTITY_REJECTED");
        var expected=db.Database.GetMigrations().ToArray();
        BatchPolicy.Require(expected.Length==91 && expected[^1]=="20260905221056_CERT2_TenantDteTypeAuthorization"
            && (await db.Database.GetAppliedMigrationsAsync()).SequenceEqual(expected),"BATCH_EXACT_91_MIGRATIONS_REQUIRED");
    }
    public static string SafePath(string root,string relative)
    {
        BatchPolicy.Require(!Path.IsPathRooted(relative),"BATCH_PATH_REJECTED");
        var full=Path.GetFullPath(Path.Combine(root,relative));
        BatchPolicy.Require(full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"BATCH_PATH_REJECTED");
        for(string? p=full;p is not null;p=Path.GetDirectoryName(p))
            if(File.Exists(p)||Directory.Exists(p)) BatchPolicy.Require((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"BATCH_REPARSE_REJECTED");
        return full;
    }
}
