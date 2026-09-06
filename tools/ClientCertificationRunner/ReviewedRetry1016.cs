using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Diagnostico;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Persistence;

namespace ClientCertificationRunner;

/// <summary>One reviewed repair of one proven rejection. Never a generic retry/resume mechanism.</summary>
public static class ReviewedRetry1016
{
    public const int DocumentId = 1016;
    public const string Marker = "CLIENT_PILOT_RETRY_1016_V1";
    public const string ResponseHash = "E0D6E7D219846E70B0983FCA0B30C0A36CD5D28B0523F550B285A8C366036316";
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
            return Text("estado") == "ERROR" && Text("codigoMsg") == "096" && Text("codigoHttp") == "400" && Text("clasificaMsg") == "ERROR";
        }
        catch (JsonException) { return false; }
    }
    public static bool MayRegenerateReviewed(DteDocumento document)
        => document.Id == DocumentId && document.EmpresaId == 23 && document.TipoDteCodigo == "01" && document.AmbienteCodigo == "PRUEBAS"
            && document.EstadoCodigo == "ERROR" && string.IsNullOrWhiteSpace(document.SelloRecibido) && document.EnviadoAt is not null
            && document.IdempotencyScope == "CERT" && document.ReceptorTipoDocumento is null && document.ReceptorNumeroDocumento is null;

    public static async Task<Preparation> PrepareAsync(NeoStpDbContext db, CancellationToken ct = default)
    {
        RunnerPolicy.Require(db.Database.IsSqlServer(), "REVIEWED_RETRY_SQL_SERVER_REQUIRED");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @lockResult int;
            EXEC @lockResult=sys.sp_getapplock @Resource=N'NeoSTP:DTE-LIMIT:23',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @lockResult<0 THROW 50001,'Reviewed pilot repair lock unavailable.',1;
            """, ct);
        var document = await db.DteDocumentos.Include(x => x.Json).SingleAsync(x => x.Id == DocumentId && x.EmpresaId == 23, ct);
        var campaign = await db.CertificationCampaigns.AsNoTracking().Include(x => x.TypeBudgets)
            .SingleOrDefaultAsync(x => x.PublicId == RunnerPolicy.CampaignId && x.EmpresaId == 23, ct);
        RunnerPolicy.Campaign(campaign, RunnerPolicy.CampaignId, DateTimeOffset.UtcNow);
        var consumption = await db.CertificationCampaignConsumptions.AsNoTracking()
            .SingleAsync(x => x.EmpresaId == 23 && x.DteDocumentoId == DocumentId && x.CampaignId == campaign!.Id && x.TipoDteCodigo == "01", ct);
        var marker = await db.Auditoria.AsNoTracking().SingleOrDefaultAsync(x => x.EmpresaId == 23 && x.Accion == Marker, ct);
        if (marker is not null)
        {
            RunnerPolicy.Require(marker.Entidad == "DTE" && marker.EntidadId == "1016", "REVIEWED_RETRY_MARKER_CONFLICT");
            await transaction.RollbackAsync(ct);
            return new(true, marker.Id, DocumentId, document.CodigoGeneracion, document.NumeroControl, consumption.Id,
                consumption.RequestHash, consumption.IdempotencyKeyHash, document.VersionDte, true);
        }
        var access = await CertificationCampaignAccess.ValidateAsync(db, 23, document, ct);
        RunnerPolicy.Require(access.IsSuccess, access.ErrorCode ?? "REVIEWED_RETRY_CAMPAIGN_REJECTED");
        RunnerPolicy.Require(document.EstadoCodigo == "ERROR" && document.AmbienteCodigo == "PRUEBAS" && document.TipoDteCodigo == "01"
            && document.EnviadoAt is not null && string.IsNullOrWhiteSpace(document.SelloRecibido) && document.VersionDte == 1
            && document.TotalPagar == 1.13m && ConfirmedReviewedResponse(document.Json?.RespuestaHacienda), "REVIEWED_REJECTION_1016_NOT_MATCHED");
        var diagnosis = DteDiagnosticoGuia.Crear(document.EstadoCodigo, document.SelloRecibido, document.EnviadoAt, document.Json!.RespuestaHacienda);
        RunnerPolicy.Require(!diagnosis.RequiereConsultaHacienda && diagnosis.SiguientePaso == "CORREGIR_DATOS", "REVIEWED_RETRY_REQUIRES_RECONCILIATION");
        using var oldJson = JsonDocument.Parse(document.Json.JsonDte);
        var oldIdentity = oldJson.RootElement.GetProperty("identificacion");
        RunnerPolicy.Require(oldIdentity.GetProperty("version").GetInt32() == 2 && oldIdentity.GetProperty("ambiente").GetString() == "00"
            && oldIdentity.GetProperty("tipoDte").GetString() == "01" && oldIdentity.GetProperty("codigoGeneracion").GetString() == document.CodigoGeneracion
            && oldIdentity.GetProperty("numeroControl").GetString() == document.NumeroControl
            && oldJson.RootElement.GetProperty("emisor").GetProperty("nit").GetString() == RunnerPolicy.Nit, "REVIEWED_RETRY_ORIGINAL_JSON_IDENTITY_REJECTED");
        if (!await db.DteErrorOcurrencias.AnyAsync(x => x.EmpresaId == 23 && x.DteDocumentoId == DocumentId && x.RespuestaMhJson == document.Json.RespuestaHacienda, ct))
            db.DteErrorOcurrencias.Add(new DteErrorOcurrencia { EmpresaId = 23, DteDocumentoId = DocumentId, CodigoError = "096",
                Mensaje = "Respuesta original preservada antes del único reintento revisado 1016.", Fuente = DteErrorFuente.Certificacion,
                RespuestaMhJson = document.Json.RespuestaHacienda, JsonEnviado = document.Json.JsonFirmado ?? document.Json.JsonDte,
                OcurrioAt = document.Json.RespuestaAt ?? document.EnviadoAt!.Value, CreatedBy = RunnerPolicy.Actor });
        var attempt = new Auditoria { EmpresaId = 23, Username = RunnerPolicy.Actor, Modulo = "DTE", Accion = Marker, Entidad = "DTE", EntidadId = "1016",
            Resultado = "RESERVADO", Detalle = "Un único reintento revisado; si se interrumpe no se reanuda automáticamente. Sin nueva reserva ni cuota.",
            DatosAntes = JsonSerializer.Serialize(new { DocumentId, document.CodigoGeneracion, document.NumeroControl, ResponseSha256Utf16 = ResponseHash,
                document.VersionDte, JsonVersion = 2, ConsumptionId = consumption.Id }),
            DatosDespues = JsonSerializer.Serialize(new { ExpectedGeneratedVersion = 2, ReceptorTipoDocumento = (string?)null,
                ReceptorNumeroDocumento = (string?)null, OriginalConsumptionRetained = true, OriginalIdentityRetained = true }) };
        db.Auditoria.Add(attempt);
        document.ReceptorTipoDocumento = null; document.ReceptorNumeroDocumento = null;
        document.UpdatedAt = DateTime.UtcNow; document.UpdatedBy = RunnerPolicy.Actor;
        // Version, old JSON, signature and EnviadoAt are preserved here. The fixed generation service
        // archives/regenerates and synchronizes version in its own fiscal transaction.
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(false, attempt.Id, DocumentId, document.CodigoGeneracion, document.NumeroControl, consumption.Id,
            consumption.RequestHash, consumption.IdempotencyKeyHash, document.VersionDte, true);
    }
}
