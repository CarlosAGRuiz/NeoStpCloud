using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Diagnostico;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Persistence;

namespace ClientCertificationRunner;

/// <summary>One reviewed repair of one proven rejection. Never a generic retry/resume mechanism.</summary>
public static class ReviewedRetry1017
{
    public const int DocumentId = 1017;
    public const string Marker = "CLIENT_PILOT_RETRY_1017_V1";
    public const string ResponseHash = "20AA343DED877599F64076749D6E15EC079E14A75A34139EAEDC5A9385242FE5";
    public sealed record Preparation(bool Replayed, long MarkerId, int DocumentId, string CodigoGeneracion, string NumeroControl,
        int ConsumptionId, string OriginalRequestHash, string OriginalIdempotencyKeyHash, int VersionBeforePipeline,
        bool OriginalResponsePreserved, bool NewQuotaConsumed = false);

    public static bool ConfirmedReviewedResponse(string? raw)
    {
        if (raw is null || Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(raw))) != ResponseHash) return false;
        try
        {
            using var parsed = JsonDocument.Parse(raw); var r = parsed.RootElement;
            string? Text(string name) => r.TryGetProperty(name, out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString() : null;
            return Text("estado") == "ERROR" && Text("codigoMsg") == "009" && Text("codigoHttp") == "400" && Text("clasificaMsg") == "ERROR";
        }
        catch (JsonException) { return false; }
    }
    public static bool MayRegenerateReviewed(DteDocumento document)
        => document.Id == DocumentId && document.EmpresaId == 23 && document.TipoDteCodigo == "03" && document.AmbienteCodigo == "PRUEBAS"
            && document.EstadoCodigo == "ERROR" && string.IsNullOrWhiteSpace(document.SelloRecibido) && document.EnviadoAt is not null
            && document.IdempotencyScope == "CERT" && document.ReceptorTipoDocumento == "36" && document.ReceptorNumeroDocumento == NeoReceiverSnapshot.Expected.Nit
            && document.ReceptorNrc == NeoReceiverSnapshot.Expected.Nrc;

    public static async Task<Preparation> PrepareAsync(NeoStpDbContext db, SchemaCheckedGenerator generator, DteConfiguracion fiscal,
        Empresa verifiedIssuer, IReadOnlyList<LookupItem> departments, IReadOnlyList<LookupItem> municipalities, IReadOnlyList<LookupItem> districts,
        CancellationToken ct = default)
    {
        RunnerPolicy.Require(db.Database.IsSqlServer(), "REVIEWED_RETRY_SQL_SERVER_REQUIRED");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @lockResult int;
            EXEC @lockResult=sys.sp_getapplock @Resource=N'NeoSTP:DTE-LIMIT:23',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @lockResult<0 THROW 50001,'Reviewed pilot repair lock unavailable.',1;
            """, ct);
        var document = await db.DteDocumentos.Include(x => x.Json).Include(x => x.Empresa).Include(x => x.Detalles).SingleAsync(x => x.Id == DocumentId && x.EmpresaId == 23, ct);
        var campaign = await db.CertificationCampaigns.AsNoTracking().Include(x => x.TypeBudgets)
            .SingleOrDefaultAsync(x => x.PublicId == RunnerPolicy.CampaignId && x.EmpresaId == 23, ct);
        RunnerPolicy.Campaign(campaign, RunnerPolicy.CampaignId, DateTimeOffset.UtcNow);
        var consumption = await db.CertificationCampaignConsumptions.AsNoTracking()
            .SingleAsync(x => x.EmpresaId == 23 && x.DteDocumentoId == DocumentId && x.CampaignId == campaign!.Id && x.TipoDteCodigo == "03", ct);
        var marker = await db.Auditoria.AsNoTracking().SingleOrDefaultAsync(x => x.EmpresaId == 23 && x.Accion == Marker, ct);
        if (marker is not null)
        {
            RunnerPolicy.Require(marker.Entidad == "DTE" && marker.EntidadId == "1017", "REVIEWED_RETRY_MARKER_CONFLICT");
            await transaction.RollbackAsync(ct);
            return new(true, marker.Id, DocumentId, document.CodigoGeneracion, document.NumeroControl, consumption.Id,
                consumption.RequestHash, consumption.IdempotencyKeyHash, document.VersionDte, true);
        }
        var access = await CertificationCampaignAccess.ValidateAsync(db, 23, document, ct);
        RunnerPolicy.Require(access.IsSuccess, access.ErrorCode ?? "REVIEWED_RETRY_CAMPAIGN_REJECTED");
        var neo = await NeoReceiverSnapshot.ReadAndVerifyAsync(db, ct);
        var issuerTerritory = DteTerritoryResolver.Resolve(document.Empresa.Departamento, document.Empresa.Municipio, document.Empresa.Distrito,
            departments, municipalities, districts, true);
        var receiverTerritory = DteTerritoryResolver.Resolve(neo.Departamento, neo.Municipio, neo.Distrito, departments, municipalities, districts, true);
        RunnerPolicy.Require(issuerTerritory.IsSuccess && issuerTerritory.Value!.Department == "05" && issuerTerritory.Value.Municipality == "24"
            && issuerTerritory.Value.District == "15" && receiverTerritory.IsSuccess && receiverTerritory.Value!.Department == "06"
            && receiverTerritory.Value.Municipality == "23" && receiverTerritory.Value.District == "03", "REVIEWED_RETRY_1017_TERRITORY_LOOKUP_REQUIRED");
        RunnerPolicy.Require(verifiedIssuer.Id == 23 && verifiedIssuer.Nit == RunnerPolicy.Nit && verifiedIssuer.Departamento == "05"
            && verifiedIssuer.Municipio == "24" && verifiedIssuer.Distrito == "15", "REVIEWED_RETRY_1017_VERIFIED_ISSUER_REQUIRED");
        RunnerPolicy.Require(document.EstadoCodigo == "ERROR" && document.AmbienteCodigo == "PRUEBAS" && document.TipoDteCodigo == "03"
            && document.EnviadoAt is not null && string.IsNullOrWhiteSpace(document.SelloRecibido) && document.VersionDte == 4
            && document.CodigoGeneracion == "DB6D818C-266D-4472-8870-A629FF398277" && document.NumeroControl == "DTE-03-M001P001-000000000000001"
            && document.ReceptorNumeroDocumento == "06140000000000" && document.ReceptorNrc == "7654321"
            && document.TotalPagar == 1.13m && ConfirmedReviewedResponse(document.Json?.RespuestaHacienda), "REVIEWED_REJECTION_1017_NOT_MATCHED");
        var diagnosis = DteDiagnosticoGuia.Crear(document.EstadoCodigo, document.SelloRecibido, document.EnviadoAt, document.Json!.RespuestaHacienda);
        RunnerPolicy.Require(!diagnosis.RequiereConsultaHacienda && diagnosis.SiguientePaso == "CORREGIR_DATOS", "REVIEWED_RETRY_REQUIRES_RECONCILIATION");
        using var oldJson = JsonDocument.Parse(document.Json.JsonDte);
        var oldIdentity = oldJson.RootElement.GetProperty("identificacion");
        RunnerPolicy.Require(oldIdentity.GetProperty("version").GetInt32() == 4 && oldIdentity.GetProperty("ambiente").GetString() == "00"
            && oldIdentity.GetProperty("tipoDte").GetString() == "03" && oldIdentity.GetProperty("codigoGeneracion").GetString() == document.CodigoGeneracion
            && oldIdentity.GetProperty("numeroControl").GetString() == document.NumeroControl
            && oldJson.RootElement.GetProperty("emisor").GetProperty("nit").GetString() == RunnerPolicy.Nit, "REVIEWED_RETRY_ORIGINAL_JSON_IDENTITY_REJECTED");
        if (!await db.DteErrorOcurrencias.AnyAsync(x => x.EmpresaId == 23 && x.DteDocumentoId == DocumentId && x.RespuestaMhJson == document.Json.RespuestaHacienda, ct))
            db.DteErrorOcurrencias.Add(new DteErrorOcurrencia { EmpresaId = 23, DteDocumentoId = DocumentId, CodigoError = "009",
                Mensaje = "Respuesta original preservada antes del único reintento revisado 1017.", Fuente = DteErrorFuente.Certificacion,
                RespuestaMhJson = document.Json.RespuestaHacienda, JsonEnviado = document.Json.JsonFirmado ?? document.Json.JsonDte,
                OcurrioAt = document.Json.RespuestaAt ?? document.EnviadoAt!.Value, CreatedBy = RunnerPolicy.Actor });
        var attempt = new Auditoria { EmpresaId = 23, Username = RunnerPolicy.Actor, Modulo = "DTE", Accion = Marker, Entidad = "DTE", EntidadId = "1017",
            Resultado = "RESERVADO", Detalle = "Un único reintento revisado; si se interrumpe no se reanuda automáticamente. Sin nueva reserva ni cuota.",
            DatosAntes = JsonSerializer.Serialize(new { DocumentId, document.CodigoGeneracion, document.NumeroControl, ResponseSha256Utf16 = ResponseHash,
                document.VersionDte, JsonVersion = 4, ConsumptionId = consumption.Id }),
            DatosDespues = JsonSerializer.Serialize(new { ExpectedGeneratedVersion = 4, PublicReceiverSnapshotSha256 = neo.Hash(), ReceiverCompanySource = 2,
                SourceCompanyUnchanged = true, OriginalConsumptionRetained = true, OriginalIdentityRetained = true }) };
        db.Auditoria.Add(attempt);
        neo.ApplyToReviewedDocument(document);
        var originalCompanyNavigation = document.Empresa;
        try
        {
            // The caller projection already contains resolved codes. Restore the tracked navigation
            // before EF sees any graph change; never normalize or write the real company entity.
            document.Empresa = verifiedIssuer;
            var generated = generator.Generar(document, fiscal);
            RunnerPolicy.Require(generated.IsSuccess, generated.ErrorCode ?? "REVIEWED_NEO_RECEIVER_SCHEMA_REJECTED");
        }
        finally { document.Empresa = originalCompanyNavigation; }
        document.UpdatedAt = DateTime.UtcNow; document.UpdatedBy = RunnerPolicy.Actor;
        // Version, old JSON, signature and EnviadoAt are preserved here. The fixed generation service
        // archives/regenerates and synchronizes version in its own fiscal transaction.
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(false, attempt.Id, DocumentId, document.CodigoGeneracion, document.NumeroControl, consumption.Id,
            consumption.RequestHash, consumption.IdempotencyKeyHash, document.VersionDte, true);
    }
}
