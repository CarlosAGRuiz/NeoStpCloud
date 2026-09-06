using System.Text.RegularExpressions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;

namespace ClientCertificationRunner;

public sealed record RunnerArguments(bool Pilot, string? Type, Guid? Campaign, bool RehearseClone = false, bool ReviewedRetry = false, bool ReviewedRetry03 = false)
{
    public static RunnerArguments Parse(string[] args)
    {
        if (args.Length == 0 || args.SequenceEqual(new[] { "--preview" })) return new(false, null, null);
        if (args.SequenceEqual(new[] { "--retry-reviewed-1016", "--campaign", RunnerPolicy.CampaignId.ToString("D"),
            "--expected-rejection", ReviewedRetry1016.ResponseHash, "--execute-test-pilot" })) return new(true, "01", RunnerPolicy.CampaignId, ReviewedRetry: true);
        if (args.SequenceEqual(new[] { "--retry-reviewed-1017", "--campaign", RunnerPolicy.CampaignId.ToString("D"),
            "--expected-rejection", ReviewedRetry1017.ResponseHash, "--receiver-snapshot", NeoReceiverSnapshot.ExpectedHash,
            "--execute-test-pilot" })) return new(true, "03", RunnerPolicy.CampaignId, ReviewedRetry03: true);
        if (args.SequenceEqual(new[] { "--rehearse-clone" })) return new(false, null, RunnerPolicy.CampaignId, true);
        if (args.Length == 3 && args[0] == "--preview" && args[1] == "--campaign" && Guid.TryParse(args[2], out var preview) && preview == RunnerPolicy.CampaignId)
            return new(false, null, preview);
        if (args.Length == 5 && args[0] == "--pilot" && RunnerPolicy.Types.Contains(args[1]) && args[2] == "--campaign"
            && Guid.TryParse(args[3], out var pilot) && pilot == RunnerPolicy.CampaignId && args[4] == "--execute-test-pilot")
            return new(true, args[1], pilot);
        throw new RunnerRejected("ARGUMENTS_REJECTED");
    }
}
public sealed class RunnerRejected(string code) : Exception(code) { public string Code { get; } = code; }
public static class RunnerPolicy
{
    public const int Tenant = 23;
    public const string Nit = "06232705261148";
    public const string Matrix = "CLIENT23-PILOT-4-V1";
    public const string Actor = "client23-certification-pilot";
    public const string BaseUrl = "https://apitest.dtes.mh.gob.sv";
    public static readonly Guid CampaignId = Guid.Parse("5f681bfb-149d-4650-9a1b-5723feeebef3");
    public static readonly string[] Types = ["01", "03", "11", "14"];
    public static void Require(bool value, string code) { if (!value) throw new RunnerRejected(code); }
    public static bool ExactCsv(string? csv) => csv == "01,03,11,14";
    public static string Key(string type) => "client23-pilot-v1-" + type;
    public static void Campaign(CertificationCampaign? campaign, Guid expected, DateTimeOffset now)
        => Require(expected == CampaignId && campaign is not null && campaign.PublicId == expected && campaign.EmpresaId == Tenant && campaign.ExpectedNit == Nit
            && campaign.AmbienteCodigo == "PRUEBAS" && campaign.Status == "ACTIVE" && campaign.StartsAtUtc <= now && now < campaign.ExpiresAtUtc
            && campaign.StartsAtUtc.Offset == TimeSpan.Zero && campaign.ExpiresAtUtc.Offset == TimeSpan.Zero
            && campaign.TotalBudget == 4 && campaign.MatrixReference == Matrix && campaign.TypeBudgets.Count == 4
            && campaign.TypeBudgets.All(x => x.Budget == 1) && campaign.TypeBudgets.Select(x => x.TipoDteCodigo).Order().SequenceEqual(Types), "PILOT_CAMPAIGN_REJECTED");
    public static bool MayStartTransmission(DteDocumento document)
        => document.EmpresaId == Tenant && document.AmbienteCodigo == "PRUEBAS" && Types.Contains(document.TipoDteCodigo)
            && document.EnviadoAt is null && string.IsNullOrWhiteSpace(document.SelloRecibido)
            && document.EstadoCodigo == "BORRADOR" && document.IdempotencyScope == "CERT";
    public static bool AllowedEndpoint(HttpMethod method, Uri? uri)
        => method == HttpMethod.Post && uri is not null && uri.Scheme == "https" && uri.Host == "apitest.dtes.mh.gob.sv"
            && uri.Port == 443 && uri.UserInfo == "" && uri.Query == "" && uri.Fragment == ""
            && uri.AbsolutePath is "/seguridad/auth" or "/fesv/recepciondte";
    public static string Establishment(DteConfiguracion config)
    {
        Require(Regex.IsMatch(config.CodigoEstablecimientoMh ?? "", "\\A[MBSP][0-9]{3}\\z")
            && Regex.IsMatch(config.CodigoPuntoVentaMh ?? "", "\\AP[0-9]{3}\\z"), "ESTABLISHMENT_NOT_REVIEWED");
        return config.CodigoEstablecimientoMh + config.CodigoPuntoVentaMh;
    }
}
