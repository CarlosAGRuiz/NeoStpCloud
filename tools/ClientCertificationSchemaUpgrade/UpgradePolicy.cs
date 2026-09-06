using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace ClientCertificationSchemaUpgrade;

internal sealed class UpgradeRejected(string code) : Exception(code) { public string Code { get; } = code; }
internal static class UpgradePolicy
{
    internal const string Candidate = "tmp/production-candidate/20260905T222711Z-7b556dd2c9b74a8680c6dbeb4edc03d6/candidate-manifest.json";
    internal const string CandidateSha = "5236622FFC1CB17BF7C0DEDED29B0703E35FE72D26407583248F26BF9032CC16";
    internal const string Last = "20260905221056_CERT2_TenantDteTypeAuthorization";
    internal const string BackupRoot = @"C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup";
    internal static readonly string[] Delta = ["20260904021242_GL0A_RefreshSessionContext", "20260904030641_GL0B_AuthSessionFoundation",
        "20260904125956_GL0C_SsoIdentityMfaConcurrency", "20260904140909_GL1B_DteIdempotency", "20260904154619_GL1D_DteFiscalConcurrency",
        "20260904173458_GL1E_LoteAttemptConcurrency", "20260905002545_GL1G_BillingProviderOperations", "20260905034928_GL1H_CheckoutIntentFoundation",
        "20260905043506_GL1H_WompiPaymentInbox", "20260905150044_GL1H_AtomicPaymentApplication", "20260905214616_CERT1_CampaignQuotaFoundation", Last];
    internal static void Require(bool value, string code) { if (!value) throw new UpgradeRejected(code); }
    internal static bool ApplyMode(string[] args)
    {
        if (args.Length == 0 || args.SequenceEqual(new[] { "--preview" })) return false;
        if (args.SequenceEqual(new[] { "--apply-active-schema-79-to-91" })) return true;
        throw new UpgradeRejected("ARGUMENTS_REJECTED");
    }
    internal static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    internal static void Baseline(string[] applied, string[] expected)
        => Require(expected.Length == 91 && expected[^1] == Last && expected.Skip(79).SequenceEqual(Delta)
            && applied.Length == 79 && applied.SequenceEqual(expected.Take(79)), "EXACT_79_TO_91_DELTA_REQUIRED");
    internal static async Task<JsonElement> Windows(string repo, string mode = "Check", string? id = null)
    {
        Require(OperatingSystem.IsWindows(), "WINDOWS_REQUIRED");
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        // ProcessStartInfo does not perform pwsh's native PowerShell compatibility adjustment.
        // Let Windows PowerShell reconstruct its own module path instead of inheriting PowerShell 7 modules.
        start.Environment.Remove("PSModulePath");
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(repo, "tools/ClientCertificationSchemaUpgrade/WindowsGuard.ps1"), "-Mode", mode }) start.ArgumentList.Add(argument);
        if (id is not null) { start.ArgumentList.Add("-RunId"); start.ArgumentList.Add(id); }
        using var process = Process.Start(start) ?? throw new UpgradeRejected("WINDOWS_GUARD_START_FAILED");
        var text = process.StandardOutput.ReadToEndAsync(); var errorText = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); var errors = await errorText;
        if (process.ExitCode != 0)
        {
            var rawId = System.Text.RegularExpressions.Regex.Match(errors, "FullyQualifiedErrorId\\s*:\\s*([^\\r\\n]+)").Groups[1].Value;
            var safeId = new string(rawId.Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-').Take(100).ToArray());
            if (safeId.Length == 0) safeId = "UNCLASSIFIED";
            var diagnostic = Path.Combine(repo, "tmp/client-schema-upgrade/windows-guard-failures", Guid.NewGuid().ToString("N") + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(diagnostic)!);
            await File.WriteAllTextAsync(diagnostic, JsonSerializer.Serialize(new { AtUtc = DateTimeOffset.UtcNow, Mode = mode,
                process.ExitCode, FullyQualifiedErrorId = safeId, RawStderrSuppressed = true, ChildPowerShellModulePathReset = true }));
            throw new UpgradeRejected("WINDOWS_GUARD_FAILED_" + safeId);
        }
        using var json = JsonDocument.Parse(await text); return json.RootElement.Clone();
    }
    internal static async Task<Dictionary<string, object?>> Evidence(string repo)
    {
        var paths = new Dictionary<string, string> {
            ["Candidate"] = Candidate,
            ["UnitTrx"] = "tmp/client-certification-release-2026-09-05/tests/client-release_net10.0_20260905162701.trx",
            ["IntegrationTrx"] = "tmp/client-certification-release-2026-09-05/tests/client-release_net10.0_20260905162638.trx",
            ["Clone"] = "tmp/client-certification-release-2026-09-05/rehearsal-results.json",
            ["Pilot"] = "tmp/client-certification-runner/20260905T222553Z-5ab79d92cce2462cb9fa44e696994bf4.json" };
        Require(Hash(Path.Combine(repo, Candidate)) == CandidateSha, "CANDIDATE_MANIFEST_CHANGED");
        using var candidate = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, Candidate)));
        var c = candidate.RootElement;
        Require(c.GetProperty("PublicationComplete").GetBoolean() && !c.GetProperty("HostsStarted").GetBoolean()
            && !c.GetProperty("EnvironmentConfigurationIncluded").GetBoolean() && c.GetProperty("Roots").GetArrayLength() == 3
            && c.GetProperty("Files").GetArrayLength() == 647, "CANDIDATE_NOT_VERIFIED");
        var candidateRoot = Path.GetDirectoryName(Path.Combine(repo, Candidate))!;
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in c.GetProperty("Files").EnumerateArray())
        {
            var root = entry.GetProperty("Root").GetString()!; var relative = entry.GetProperty("Path").GetString()!;
            Require(root is "api" or "web" or "worker" && !Path.IsPathRooted(relative), "CANDIDATE_PATH_REJECTED");
            var full = Path.GetFullPath(Path.Combine(candidateRoot, root, relative));
            Require(full.StartsWith(Path.Combine(candidateRoot, root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && known.Add(full) && File.Exists(full) && new FileInfo(full).Length == entry.GetProperty("Bytes").GetInt64()
                && Hash(full) == entry.GetProperty("Sha256").GetString(), "CANDIDATE_ARTIFACT_CHANGED");
        }
        var physical = new[] { "api", "web", "worker" }.SelectMany(root => Directory.EnumerateFiles(Path.Combine(candidateRoot, root), "*", SearchOption.AllDirectories))
            .Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Require(known.SetEquals(physical), "CANDIDATE_EXTRA_OR_MISSING_ARTIFACTS");
        Require(Hash(typeof(NeoSTP.Infrastructure.Persistence.NeoStpDbContext).Assembly.Location)
            == Hash(Path.Combine(candidateRoot, "api", "NeoSTP.Infrastructure.dll")), "MIGRATOR_ASSEMBLY_DIFFERS_FROM_REVIEWED_CANDIDATE");
        foreach (var (name, count) in new[] { ("UnitTrx", 2304), ("IntegrationTrx", 9) })
        {
            var xml = XDocument.Load(Path.Combine(repo, paths[name]));
            var summary = xml.Descendants().Single(x => x.Name.LocalName == "ResultSummary");
            var counters = summary.Elements().Single(x => x.Name.LocalName == "Counters");
            Require((string?)summary.Attribute("outcome") == "Completed" && (int?)counters.Attribute("total") == count
                && (int?)counters.Attribute("executed") == count && (int?)counters.Attribute("passed") == count
                && (int?)counters.Attribute("failed") == 0 && (int?)counters.Attribute("error") == 0, "FINAL_TEST_EVIDENCE_REJECTED");
        }
        using var clone = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, paths["Clone"])));
        Require(clone.RootElement.GetProperty("Passed").GetInt32() == 22 && clone.RootElement.GetProperty("Failed").GetInt32() == 0
            && clone.RootElement.GetProperty("AppliedMigrations").EnumerateArray().Select(x => x.GetString()).TakeLast(12).SequenceEqual(Delta), "CLONE_REHEARSAL_REJECTED");
        using var pilot = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, paths["Pilot"])));
        var p = pilot.RootElement;
        Require(p.GetProperty("Passed").GetBoolean() && p.GetProperty("RollbackVerified").GetBoolean()
            && p.GetProperty("Mode").GetString() == "REHEARSE_CLONE_ROLLBACK" && p.GetProperty("Exact91MigrationsVerified").GetBoolean()
            && p.GetProperty("HaciendaReceptionAttempts").GetInt32() == 0 && p.GetProperty("HaciendaAuthenticationAttempts").GetInt32() == 0
            && p.GetProperty("Preview").GetArrayLength() == 4 && p.GetProperty("Preview").EnumerateArray().All(x => x.GetProperty("SchemaPassed").GetBoolean()), "PILOT_REHEARSAL_REJECTED");
        return paths.ToDictionary(x => x.Key, x => (object?)new { Path = x.Value, Sha256 = Hash(Path.Combine(repo, x.Value)) });
    }
}
