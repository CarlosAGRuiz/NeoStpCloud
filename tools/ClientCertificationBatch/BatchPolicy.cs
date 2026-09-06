using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClientCertificationRunner;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;

namespace ClientCertificationBatch;

public sealed class BatchRejected(string code) : Exception(code) { public string Code { get; } = code; }
public sealed record BatchCase(string Type, int Ordinal, int Quantity, decimal Price)
{
    public string Key => $"batch275-v1-{Type}-{Ordinal:D3}";
    public string Reference => $"CLIENT23-BATCH275-V1:{Type}:{Ordinal:D3}";
}
public static class BatchPolicy
{
    public const int Tenant = 23;
    public const string Nit = "06232705261148";
    public const string Version = "CLIENT23-BATCH275-V1";
    public const string Actor = "client23-certification-batch";
    public const string PlanAction = "CERT_BATCH275_PLAN";
    public const string AttemptAction = "CERT_BATCH275_ATTEMPT";
    public const string StopAction = "CERT_BATCH275_STOP";
    public static readonly Guid Campaign = Guid.Parse("7bc8a3fb-08d6-4c8d-974a-01cde7fd702a");
    public static readonly (string Type, int Count)[] Budgets = [("01",88),("03",74),("11",89),("14",24)];
    public static void Require(bool condition, string code) { if (!condition) throw new BatchRejected(code); }
    public static string Mode(string[] args) => args.Length == 0 ? "--preview" : args.Length == 1 &&
        args[0] is "--preview" or "--prepare-campaign" or "--run-next" or "--self-test" ? args[0] : throw new BatchRejected("BATCH_ARGUMENTS_REJECTED");
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string FileHash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    public static IReadOnlyList<BatchCase> Cases() => Budgets.SelectMany(b => Enumerable.Range(1,b.Count)
        .Select(i => new BatchCase(b.Type,i,1+(i-1)%5,1+(i-1)/5%10))).ToArray();
    public static string Normalize(string? value) => (value ?? "").Replace("-", "").Replace(" ", "");
    public static bool Accepted(DteDocumento doc) => doc.EmpresaId == Tenant && doc.AmbienteCodigo == "PRUEBAS"
        && doc.EstadoCodigo == "PROCESADO" && !string.IsNullOrWhiteSpace(doc.SelloRecibido)
        && doc.EnviadoAt is not null && doc.VersionDte == PilotFixture.ExpectedVersion(doc.TipoDteCodigo);
    public static bool ExactCampaign(CertificationCampaign c) => c.PublicId == Campaign && c.EmpresaId == Tenant
        && c.ExpectedNit == Nit && c.AmbienteCodigo == "PRUEBAS" && c.MatrixReference == Version && c.TotalBudget == 275
        && c.StartsAtUtc.Offset == TimeSpan.Zero && c.ExpiresAtUtc.Offset == TimeSpan.Zero
        && c.ExpiresAtUtc == c.StartsAtUtc.AddHours(48) && c.TypeBudgets.Count == 4
        && Budgets.All(b => c.TypeBudgets.Count(t => t.TipoDteCodigo == b.Type && t.Budget == b.Count) == 1);
    public static string PayloadHash(string planHash, BatchCase item) => Hash($"{planHash}|{item.Key}|{item.Quantity}|{item.Price.ToString("F2",CultureInfo.InvariantCulture)}");
    public static string KeyHash(BatchCase item) => Hash($"{Campaign:N}:{item.Key}");
    public static string RequestHash(string planHash, BatchCase item) => Hash($"{PayloadHash(planHash,item)}|{item.Type}|{item.Reference}");
    public static bool ClaimMatches(CertificationCampaignConsumption c, BatchCase item, string planHash) =>
        c.EmpresaId == Tenant && c.TipoDteCodigo == item.Type && c.ScenarioReference == item.Reference
        && c.IdempotencyKeyHash == KeyHash(item) && c.RequestHash == RequestHash(planHash,item)
        && c.Document is { } doc && doc.EmpresaId == Tenant && doc.AmbienteCodigo == "PRUEBAS" && doc.TipoDteCodigo == item.Type
        && doc.IdempotencyScope == "CERT" && doc.IdempotencyKeyHash == c.IdempotencyKeyHash
        && doc.IdempotencyRequestHash == c.RequestHash && doc.CreatedAt == c.CreatedAt;
}
