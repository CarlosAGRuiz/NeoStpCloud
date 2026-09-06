using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Common;

namespace ClientCertificationRunner;

public static class RunnerSelfTests
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks++; Console.WriteLine("PASS: " + name); }
        bool Rejected(Action action) { try { action(); return false; } catch (RunnerRejected) { return true; } }
        Check(!RunnerArguments.Parse([]).Pilot, "Default mode cannot send");
        Check(!RunnerArguments.Parse(["--preview"]).Pilot, "Explicit preview cannot send");
        Check(RunnerArguments.Parse(["--rehearse-clone"]).RehearseClone && !RunnerArguments.Parse(["--rehearse-clone"]).Pilot, "Clone rehearsal cannot send");
        foreach (var args in new[] { new[] { "--pilot", "01" }, new[] { "--pilot", "05", "--campaign", Guid.NewGuid().ToString(), "--execute-test-pilot" },
            new[] { "--pilot", "01", "--campaign", Guid.Empty.ToString(), "--execute-test-pilot" },
            new[] { "--pilot", "01", "--campaign", Guid.NewGuid().ToString(), "--execute-test-pilot" }, new[] { "--rehearse-clone", "--database", "NeoSTP_Cloud" } })
            Check(Rejected(() => RunnerArguments.Parse(args)), "Unsafe/ambiguous operator arguments rejected");
        Check(RunnerPolicy.ExactCsv("01,03,11,14") && !RunnerPolicy.ExactCsv(null) && !RunnerPolicy.ExactCsv("01,03,11,14,05"), "Exactly four authorized client types required");
        Check(RunnerPolicy.AllowedEndpoint(HttpMethod.Post, new(RunnerPolicy.BaseUrl + "/fesv/recepciondte")), "Only exact test reception endpoint accepted");
        foreach (var url in new[] { "https://api.dtes.mh.gob.sv/fesv/recepciondte", "http://apitest.dtes.mh.gob.sv/fesv/recepciondte", "https://apitest.dtes.mh.gob.sv.evil.invalid/fesv/recepciondte",
            RunnerPolicy.BaseUrl + "/fesv/recepciondte?x=1", RunnerPolicy.BaseUrl + "/other" })
            Check(!RunnerPolicy.AllowedEndpoint(HttpMethod.Post, new(url)), "Production/alternate endpoint rejected");
        Check(!RunnerPolicy.AllowedEndpoint(HttpMethod.Get, new(RunnerPolicy.BaseUrl + "/fesv/recepciondte")), "Unexpected HTTP verb rejected");
        var now = DateTimeOffset.UtcNow;
        CertificationCampaign Campaign() => new() { PublicId = RunnerPolicy.CampaignId, EmpresaId = 23, ExpectedNit = RunnerPolicy.Nit, Status = "ACTIVE", StartsAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddHours(1), TotalBudget = 4, MatrixReference = RunnerPolicy.Matrix,
            TypeBudgets = RunnerPolicy.Types.Select(t => new CertificationCampaignTypeBudget { TipoDteCodigo = t, Budget = 1 }).ToList() };
        var good = Campaign(); RunnerPolicy.Campaign(good, good.PublicId, now); Check(true, "Finite four-type pilot campaign accepted");
        var imitation = Campaign(); imitation.PublicId = Guid.NewGuid(); Check(Rejected(() => RunnerPolicy.Campaign(imitation, imitation.PublicId, now)), "Imitation pilot campaign GUID rejected");
        foreach (var defect in new[] { "tenant", "production", "expired", "revoked", "budget", "matrix" })
        {
            var bad = Campaign();
            switch (defect) { case "tenant": bad.EmpresaId = 2; break; case "production": bad.AmbienteCodigo = "PRODUCCION"; break;
                case "expired": bad.ExpiresAtUtc = now; break; case "revoked": bad.Status = "REVOKED"; break; case "budget": bad.TotalBudget = 279; break;
                case "matrix": bad.MatrixReference = "GENERIC"; break; }
            Check(Rejected(() => RunnerPolicy.Campaign(bad, bad.PublicId, now)), "Campaign " + defect + " rejected");
        }
        var doc = PilotFixture.Create("01", "M001P001", DateTime.UtcNow); doc.IdempotencyScope = "CERT";
        Check(RunnerPolicy.MayStartTransmission(doc), "Fresh campaign draft can reach final preflight");
        doc.EnviadoAt = DateTime.UtcNow; Check(!RunnerPolicy.MayStartTransmission(doc), "Prior attempt never automatically resent");
        doc.EnviadoAt = null; doc.EstadoCodigo = "PROCESADO"; Check(!RunnerPolicy.MayStartTransmission(doc), "Processed document never resent");
        doc.EstadoCodigo = "BORRADOR"; doc.SelloRecibido = "synthetic"; Check(!RunnerPolicy.MayStartTransmission(doc), "Existing seal never resent");
        Check(Rejected(() => PilotFixture.Create("05", "M001P001", DateTime.UtcNow)), "Unsupported fixture rejected before persistence");
        foreach (var (type, expectedVersion) in new[] { ("01", 2), ("03", 4), ("11", 3), ("14", 2) })
            Check(PilotFixture.Create(type, "M001P001", DateTime.UtcNow).VersionDte == expectedVersion, "Pilot " + type + " stores the effective current version");
        var mismatched = PilotFixture.Create("01", "M001P001", DateTime.UtcNow); mismatched.VersionDte = 1;
        var checkedGenerator = new SchemaCheckedGenerator(new SyntheticVersionTwoGenerator(), new Dictionary<(string, int), NJsonSchema.JsonSchema> { [("01", 2)] = new() });
        Check(checkedGenerator.Generar(mismatched, new DteConfiguracion { EmpresaId = 23, AmbienteCodigo = "PRUEBAS" }).IsSuccess && mismatched.VersionDte == 1,
            "Checked generator permits old persisted version so the application can synchronize after generation");
        Check(mismatched.ReceptorTipoDocumento is null && mismatched.ReceptorNumeroDocumento is null, "CF synthetic fixture does not invent a recipient DUI");
        var excluded = PilotFixture.Create("14", "M001P001", DateTime.UtcNow);
        Check(excluded.ReceptorTipoDocumento == "37" && excluded.ReceptorNumeroDocumento == "CERT-SE-PILOT-001",
            "Excluded-subject pilot uses explicitly synthetic passport instead of invalid DUI");
        Check(PilotFixture.Version == "CLIENT23-PILOT-FIXTURE-V2"
            && PilotFixture.Create("11", "M001P001", DateTime.UtcNow).ReceptorNumeroDocumento == "SYNTHETIC-PILOT-001"
            && PilotFixture.Create("03", "M001P001", DateTime.UtcNow).ReceptorNumeroDocumento == "06140000000000",
            "Type14 fixture change preserves other pilot recipient fields and global fingerprint version");
        var reviewedArgs = new[] { "--retry-reviewed-1016", "--campaign", RunnerPolicy.CampaignId.ToString(), "--expected-rejection", ReviewedRetry1016.ResponseHash, "--execute-test-pilot" };
        Check(RunnerArguments.Parse(reviewedArgs).ReviewedRetry && RunnerArguments.Parse(reviewedArgs).Type == "01", "Reviewed retry targets only the fixed 1016 CF attempt");
        var wrongHashArgs = (string[])reviewedArgs.Clone(); wrongHashArgs[4] = new string('0', 64);
        Check(Rejected(() => RunnerArguments.Parse(wrongHashArgs)), "Different rejection hash cannot authorize reviewed retry");
        var wrongCampaignArgs = (string[])reviewedArgs.Clone(); wrongCampaignArgs[2] = Guid.NewGuid().ToString();
        Check(Rejected(() => RunnerArguments.Parse(wrongCampaignArgs)), "Different campaign cannot authorize reviewed retry");
        Check(Rejected(() => RunnerArguments.Parse(reviewedArgs[..5])), "Reviewed retry needs explicit execution flag");
        Check(!ReviewedRetry1016.ConfirmedReviewedResponse(null) && !ReviewedRetry1016.ConfirmedReviewedResponse("{}"), "Missing or altered original response is rejected before repair");
        var retryDoc = PilotFixture.Create("01", "M001P001", DateTime.UtcNow); retryDoc.Id = 1016; retryDoc.EstadoCodigo = "ERROR"; retryDoc.EnviadoAt = DateTime.UtcNow; retryDoc.IdempotencyScope = "CERT";
        Check(ReviewedRetry1016.MayRegenerateReviewed(retryDoc), "Reviewed ERROR document can reach pipeline guard");
        retryDoc.Id = 1017; Check(!ReviewedRetry1016.MayRegenerateReviewed(retryDoc), "Another document cannot use reviewed retry");
        retryDoc.Id = 1016; retryDoc.SelloRecibido = "SYNTHETIC"; Check(!ReviewedRetry1016.MayRegenerateReviewed(retryDoc), "Sealed reviewed document cannot be retried");
        Check(NeoReceiverSnapshot.Expected.Hash() == NeoReceiverSnapshot.ExpectedHash, "NEO public receiver snapshot matches the reviewed UTF8 fingerprint");
        var reviewed03Args = new[] { "--retry-reviewed-1017", "--campaign", RunnerPolicy.CampaignId.ToString(), "--expected-rejection",
            ReviewedRetry1017.ResponseHash, "--receiver-snapshot", NeoReceiverSnapshot.ExpectedHash, "--execute-test-pilot" };
        Check(RunnerArguments.Parse(reviewed03Args).ReviewedRetry03 && !RunnerArguments.Parse(reviewed03Args).ReviewedRetry
            && RunnerArguments.Parse(reviewed03Args).Type == "03", "Reviewed 1017 path is segregated from the accepted 1016 retry");
        var wrong03Response = (string[])reviewed03Args.Clone(); wrong03Response[4] = ReviewedRetry1016.ResponseHash;
        Check(Rejected(() => RunnerArguments.Parse(wrong03Response)), "1016 rejection cannot authorize 1017 repair");
        var wrong03Snapshot = (string[])reviewed03Args.Clone(); wrong03Snapshot[6] = new string('0', 64);
        Check(Rejected(() => RunnerArguments.Parse(wrong03Snapshot)), "Unreviewed receiver fingerprint cannot authorize 1017 repair");
        Check(Rejected(() => RunnerArguments.Parse(reviewed03Args[..7])), "1017 repair requires explicit execution flag");
        Check(!ReviewedRetry1017.ConfirmedReviewedResponse(null) && !ReviewedRetry1017.ConfirmedReviewedResponse("{}"), "Changed 1017 response fails the exact hash guard");
        var retry03Doc = PilotFixture.Create("03", "M001P001", DateTime.UtcNow); retry03Doc.Id = 1017;
        NeoReceiverSnapshot.Expected.ApplyToReviewedDocument(retry03Doc);
        Check(retry03Doc.ReceptorNumeroDocumento == "06231111251090" && retry03Doc.ReceptorNrc == "3755868"
            && retry03Doc.ReceptorDepartamentoCodigo == "06" && retry03Doc.ReceptorMunicipioCodigo == "23" && retry03Doc.ReceptorDistritoCodigo == "03",
            "1017 receiver receives the complete reviewed NEO fiscal snapshot");
        retry03Doc.EstadoCodigo = "ERROR"; retry03Doc.EnviadoAt = DateTime.UtcNow; retry03Doc.IdempotencyScope = "CERT";
        Check(ReviewedRetry1017.MayRegenerateReviewed(retry03Doc), "Reviewed 1017 receiver can reach final pipeline guard");
        retry03Doc.Id = 1016;
        Check(Rejected(() => NeoReceiverSnapshot.Expected.ApplyToReviewedDocument(retry03Doc)) && !ReviewedRetry1017.MayRegenerateReviewed(retry03Doc),
            "NEO snapshot cannot mutate another document");
        retry03Doc.Id = 1017;
        Check(Rejected(() => (NeoReceiverSnapshot.Expected with { Nit = "06140000000000" }).ApplyToReviewedDocument(retry03Doc)),
            "Altered receiver snapshot is rejected before document mutation");
        const string confirmedRejection = """{"estado":"ERROR","codigoMsg":"096","codigoHttp":400,"clasificaMsg":"ERROR","descripcionMsg":"DOCUMENTO NO CUMPLE CON NORMATIVA DE CUMPLIMIENTO","observaciones":["Campo #/receptor/numDocumento no cumple el formato requerido"]}""";
        var rejection = DteDiagnosticoGuia.Crear("ERROR", null, DateTime.UtcNow, confirmedRejection);
        Check(!rejection.RequiereConsultaHacienda && rejection.SiguientePaso == "CORREGIR_DATOS", "Confirmed HTTP400/MH096 validation rejection is not an ambiguous reception");
        Check(DteDiagnosticoGuia.Crear("ERROR", null, DateTime.UtcNow, """{"estado":"ERROR","codigoMsg":"096","codigoHttp":500,"clasificaMsg":"ERROR_SERVIDOR"}""").RequiereConsultaHacienda,
            "The same numeric MH code with server failure still requires reconciliation");
        Check(DteDiagnosticoGuia.Crear("ERROR", null, DateTime.UtcNow, null).RequiereConsultaHacienda, "Missing response after transmission remains uncertain");
        using var key = RSA.Create(2048); using var other = RSA.Create(2048);
        byte[] Xml(RSA publicKey, string root = "CertificadoMH") => Encoding.UTF8.GetBytes(new XElement(root,
            new XElement("privateKey", new XElement("encodied", Convert.ToBase64String(key.ExportPkcs8PrivateKey()))),
            new XElement("publicKey", new XElement("encodied", Convert.ToBase64String(publicKey.ExportSubjectPublicKeyInfo())))).ToString());
        var certificate = Xml(key);
        try
        {
            var config = new DteConfiguracion { CertificadoBlob = certificate, PasswordCertificadoCifrado = null };
            Check(CredentialPreflight.CertificatePairValid(config.CertificadoBlob), "HaciendaCert XML accepts matching RSA key pair without PFX password");
            var wrongPair = Xml(other); try { Check(!CredentialPreflight.CertificatePairValid(wrongPair), "Mismatched private/public RSA keys rejected before reservation"); }
            finally { CryptographicOperations.ZeroMemory(wrongPair); }
            var wrongRoot = Xml(key, "UntrustedRoot"); try { Check(!CredentialPreflight.CertificatePairValid(wrongRoot), "Non-CertificadoMH XML root rejected"); }
            finally { CryptographicOperations.ZeroMemory(wrongRoot); }
            Check(!CredentialPreflight.CertificatePairValid(Encoding.UTF8.GetBytes("<!DOCTYPE CertificadoMH [<!ENTITY x SYSTEM 'file:///never-read'>]><CertificadoMH>&x;</CertificadoMH>")), "DTD/external entity input rejected locally");
            Check(!CredentialPreflight.CertificatePairValid(null), "Missing certificate rejected without creating a draft");
        }
        finally { CryptographicOperations.ZeroMemory(certificate); }
        var protector = new SyntheticProtector();
        Check(CredentialPreflight.HaciendaPasswordCanBeDecrypted(protector, "valid-synthetic"), "Decryptable Hacienda password recognized without authentication");
        Check(!CredentialPreflight.HaciendaPasswordCanBeDecrypted(protector, "wrong-purpose"), "Undecryptable Hacienda password rejected before reservation");
        Check(!CredentialPreflight.HaciendaPasswordCanBeDecrypted(protector, "empty-synthetic"), "Empty decrypted password rejected");
        Console.WriteLine($"{checks} local policy checks passed; SQL and network were not used."); return 0;
    }
    private sealed class SyntheticProtector : ISecretProtector
    {
        public string Unprotect(string value) => value switch { "valid-synthetic" => "SYNTHETIC-ONLY", "empty-synthetic" => " ", _ => throw new CryptographicException() };
        public string Protect(string value) => throw new InvalidOperationException("Not used in read-only preflight.");
        public string? ProtectOrNull(string? value) => throw new InvalidOperationException("Not used in read-only preflight.");
        public string? UnprotectOrNull(string? value) => value is null ? null : Unprotect(value);
    }
    private sealed class SyntheticVersionTwoGenerator : IDteGeneratorService
    {
        public Result<string> Generar(DteDocumento documento, DteConfiguracion? config = null)
            => Result<string>.Ok("""{"identificacion":{"version":2,"ambiente":"00"},"emisor":{"nit":"06232705261148"}}""");
    }
}
