using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Persistence;

namespace ClientCertificationBatch;

public sealed record BatchReservation(int DocumentId,int ConsumptionId,long AttemptId,string CaseKey,string PlanHash);
public sealed record BatchStatus(bool Prepared,bool Stopped,int Accepted,int Reserved,string? NextCase,string? Blocker);
public static class BatchCampaign
{
    private static string Entity => BatchPolicy.Campaign.ToString("D");
    internal static async Task LockAsync(NeoStpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @r int;
            EXEC @r=sys.sp_getapplock @Resource=N'NeoSTP:DTE-LIMIT:23',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @r<0 THROW 50001,'Batch certification lock unavailable.',1;
            """);
    }
    private static Auditoria Audit(string action,string result,string? data=null,string? key=null)=>new(){
        EmpresaId=23,Username=BatchPolicy.Actor,Modulo="DTE",Accion=action,Entidad="CERTIFICATION_BATCH",EntidadId=Entity,
        Resultado=result,Detalle=key,DatosDespues=data,CreatedAt=DateTime.UtcNow};
    public static async Task<BatchStatus> InspectAsync(NeoStpDbContext db,BatchSnapshot snapshot)
    {
        var campaign=await db.CertificationCampaigns.AsNoTracking().Include(x=>x.TypeBudgets)
            .SingleOrDefaultAsync(x=>x.PublicId==BatchPolicy.Campaign && x.EmpresaId==23);
        if(campaign is null) return new(false,false,0,0,BatchPolicy.Cases()[0].Key,null);
        BatchPolicy.Require(BatchPolicy.ExactCampaign(campaign),"BATCH_CAMPAIGN_TERMS_CHANGED");
        var plan=await db.Auditoria.AsNoTracking().SingleOrDefaultAsync(x=>x.EmpresaId==23 && x.Accion==BatchPolicy.PlanAction && x.EntidadId==Entity);
        BatchPolicy.Require(plan is not null && plan.Resultado=="PREPARED" && plan.DatosAntes==snapshot.PlanHash,"BATCH_PLAN_CHANGED");
        var stop=await db.Auditoria.AsNoTracking().AnyAsync(x=>x.EmpresaId==23 && x.Accion==BatchPolicy.StopAction && x.EntidadId==Entity);
        var claims=await db.CertificationCampaignConsumptions.AsNoTracking().Where(x=>x.CampaignId==campaign.Id)
            .Include(x=>x.Document).ToListAsync();
        var attempts=await db.Auditoria.AsNoTracking().Where(x=>x.EmpresaId==23 && x.Accion==BatchPolicy.AttemptAction && x.EntidadId==Entity).ToListAsync();
        string? blocker=null; string? next=null; var accepted=0;
        if(claims.Count!=attempts.Count || claims.Count>275) blocker="BATCH_LEDGER_ATTEMPT_MISMATCH";
        var known=BatchPolicy.Cases();
        if(claims.Any(x=>!known.Any(k=>k.Reference==x.ScenarioReference)) || attempts.Any(x=>!known.Any(k=>k.Key==x.Detalle))) blocker="BATCH_UNEXPECTED_CASE";
        foreach(var item in known)
        {
            var matching=claims.Where(x=>x.ScenarioReference==item.Reference).ToArray();
            var marks=attempts.Where(x=>x.Detalle==item.Key).ToArray();
            if(matching.Length==0 && marks.Length==0){next??=item.Key;continue;}
            if(matching.Length!=1 || marks.Length!=1 || next is not null){blocker??="BATCH_CASE_SEQUENCE_CONFLICT";continue;}
            var claim=matching[0]; var marker=marks[0];
            if(!BatchPolicy.ClaimMatches(claim,item,snapshot.PlanHash) || marker.DatosAntes!=snapshot.PlanHash){blocker??="BATCH_CASE_IDENTITY_CONFLICT";continue;}
            if(marker.Resultado!="ACCEPTED" || !BatchPolicy.Accepted(claim.Document)){blocker??="BATCH_PREVIOUS_ATTEMPT_REQUIRES_REVIEW";continue;}
            var receipt=JsonSerializer.Deserialize<BatchReservation>(marker.DatosDespues ?? "{}");
            if(receipt is null || receipt.DocumentId!=claim.DteDocumentoId || receipt.ConsumptionId!=claim.Id || receipt.AttemptId!=marker.Id
                || receipt.CaseKey!=item.Key || receipt.PlanHash!=snapshot.PlanHash){blocker??="BATCH_ACCEPTANCE_MARKER_CONFLICT";continue;}
            accepted++;
        }
        if(campaign.Status!="ACTIVE" || DateTimeOffset.UtcNow<campaign.StartsAtUtc || DateTimeOffset.UtcNow>=campaign.ExpiresAtUtc) blocker??="BATCH_CAMPAIGN_INACTIVE";
        return new(true,stop,accepted,claims.Count,next,blocker);
    }
    public static async Task<bool> PrepareAsync(BatchEnvironment environment,string expectedPlan)
    {
        await using var db=environment.Db(); await environment.ValidateSqlAsync(db);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await LockAsync(db);
        var snapshot=await BatchSnapshot.ReadAsync(environment,db);
        BatchPolicy.Require(snapshot.PlanHash==expectedPlan,"BATCH_METADATA_CHANGED_BEFORE_PREPARATION");
        var current=await InspectAsync(db,snapshot);
        if(current.Prepared)
        {
            BatchPolicy.Require(!current.Stopped && current.Blocker is null,"BATCH_EXISTING_CAMPAIGN_REQUIRES_REVIEW");
            await tx.RollbackAsync(); return false;
        }
        BatchPolicy.Require(!await db.CertificationCampaigns.AnyAsync(x=>x.EmpresaId==23 && x.MatrixReference==BatchPolicy.Version)
            && !await db.Auditoria.AnyAsync(x=>x.EmpresaId==23 && x.EntidadId==Entity && (x.Accion==BatchPolicy.PlanAction || x.Accion==BatchPolicy.StopAction || x.Accion==BatchPolicy.AttemptAction)),"BATCH_PREPARATION_CONFLICT");
        var now=DateTimeOffset.UtcNow;
        db.CertificationCampaigns.Add(new CertificationCampaign{PublicId=BatchPolicy.Campaign,EmpresaId=23,ExpectedNit=BatchPolicy.Nit,
            AmbienteCodigo="PRUEBAS",Status="ACTIVE",StartsAtUtc=now,ExpiresAtUtc=now.AddHours(48),TotalBudget=275,MatrixReference=BatchPolicy.Version,
            CreatedAt=now.UtcDateTime,CreatedBy=BatchPolicy.Actor,TypeBudgets=BatchPolicy.Budgets.Select(x=>new CertificationCampaignTypeBudget{TipoDteCodigo=x.Type,Budget=x.Count}).ToArray()});
        var plan=Audit(BatchPolicy.PlanAction,"PREPARED",JsonSerializer.Serialize(snapshot.Evidence)); plan.DatosAntes=snapshot.PlanHash;
        db.Auditoria.Add(plan); await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
    }
    public static async Task<BatchReservation?> ReserveAsync(BatchEnvironment environment,string expectedPlan)
    {
        await using var db=environment.Db(); await environment.ValidateSqlAsync(db);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await LockAsync(db);
        var snapshot=await BatchSnapshot.ReadAsync(environment,db);
        BatchPolicy.Require(snapshot.PlanHash==expectedPlan,"BATCH_METADATA_CHANGED_BEFORE_RESERVATION");
        var status=await InspectAsync(db,snapshot);
        BatchPolicy.Require(status.Prepared,"BATCH_PREPARE_REQUIRED");
        if(status.Stopped || status.Blocker is not null)
        {
            if(!status.Stopped){db.Auditoria.Add(Audit(BatchPolicy.StopAction,"STOPPED",key:status.Blocker));await db.SaveChangesAsync();await tx.CommitAsync();}
            throw new BatchRejected(status.Blocker ?? "BATCH_DURABLY_STOPPED");
        }
        if(status.NextCase is null){BatchPolicy.Require(status.Accepted==275,"BATCH_TOTAL_MISMATCH");await tx.RollbackAsync();return null;}
        var item=BatchPolicy.Cases().Single(x=>x.Key==status.NextCase);
        snapshot.ValidateCase(item);
        var doc=snapshot.Create(item);
        var monthStart=new DateTime(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1,0,0,0,DateTimeKind.Utc);
        var before=await CertificationCampaignAccess.CountCommercialDocumentsAsync(db,23,monthStart,default);
        var result=await new CertificationCampaignQuotaService(db).StageConsumptionAsync(new(BatchPolicy.Campaign,23,BatchPolicy.Nit,
            item.Type,item.Key,BatchPolicy.PayloadHash(expectedPlan,item),item.Reference,BatchPolicy.Actor),doc);
        BatchPolicy.Require(result.IsSuccess && !result.Value!.Replayed,result.ErrorCode ?? "BATCH_UNEXPECTED_RESERVATION_REPLAY");
        var number=await DteCorrelativoAllocator.NextAsync(db,23,item.Type,default);
        doc.NumeroControl=$"DTE-{item.Type}-{snapshot.Block}-{number:D15}";
        var attempt=Audit(BatchPolicy.AttemptAction,"IN_PROGRESS",key:item.Key); attempt.DatosAntes=expectedPlan;
        db.Auditoria.Add(attempt); await db.SaveChangesAsync();
        var reservation=new BatchReservation(doc.Id,result.Value!.Consumption.Id,attempt.Id,item.Key,expectedPlan);
        attempt.DatosDespues=JsonSerializer.Serialize(reservation);
        var access=await CertificationCampaignAccess.ValidateAsync(db,23,doc,default);
        BatchPolicy.Require(access.IsSuccess,access.ErrorCode ?? "BATCH_NEW_LEDGER_REJECTED");
        BatchPolicy.Require(await CertificationCampaignAccess.CountCommercialDocumentsAsync(db,23,monthStart,default)==before,"BATCH_COMMERCIAL_QUOTA_CHANGED");
        await db.SaveChangesAsync(); await tx.CommitAsync(); return reservation;
    }
    public static async Task PreflightAsync(BatchEnvironment environment,BatchReservation reservation)
    {
        await using var db=environment.Db(); await environment.ValidateSqlAsync(db);
        var snapshot=await BatchSnapshot.ReadAsync(environment,db);
        BatchPolicy.Require(snapshot.PlanHash==reservation.PlanHash,"BATCH_METADATA_CHANGED_BEFORE_HTTP");
        BatchPolicy.Require(!await db.Auditoria.AnyAsync(x=>x.EmpresaId==23 && x.Accion==BatchPolicy.StopAction && x.EntidadId==Entity),"BATCH_DURABLY_STOPPED");
        var marker=await db.Auditoria.AsNoTracking().SingleAsync(x=>x.Id==reservation.AttemptId && x.EmpresaId==23 && x.Accion==BatchPolicy.AttemptAction && x.EntidadId==Entity);
        BatchPolicy.Require(marker.Resultado=="IN_PROGRESS" && marker.DatosAntes==reservation.PlanHash
            && marker.DatosDespues==JsonSerializer.Serialize(reservation),"BATCH_ATTEMPT_MARKER_CHANGED");
        var claim=await db.CertificationCampaignConsumptions.AsNoTracking().Include(x=>x.Document)
            .SingleAsync(x=>x.Id==reservation.ConsumptionId && x.EmpresaId==23 && x.DteDocumentoId==reservation.DocumentId
                && x.TypeBudget.Campaign.PublicId==BatchPolicy.Campaign && x.TypeBudget.Campaign.EmpresaId==23);
        BatchPolicy.Require(BatchPolicy.ClaimMatches(claim,BatchPolicy.Cases().Single(x=>x.Key==reservation.CaseKey),reservation.PlanHash),"BATCH_PENDING_IDENTITY_CHANGED");
        var access=await CertificationCampaignAccess.ValidateAsync(db,23,claim.Document,default);
        BatchPolicy.Require(access.IsSuccess,access.ErrorCode ?? "BATCH_PENDING_ACCESS_REJECTED");
    }
    public static async Task FinishAsync(BatchEnvironment environment,BatchReservation reservation)
    {
        await using var db=environment.Db(); await environment.ValidateSqlAsync(db);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await LockAsync(db);
        var document=await db.DteDocumentos.AsNoTracking().Include(x=>x.Json).SingleAsync(x=>x.Id==reservation.DocumentId && x.EmpresaId==23);
        var claim=await db.CertificationCampaignConsumptions.AsNoTracking().Include(x=>x.Document)
            .SingleAsync(x=>x.Id==reservation.ConsumptionId && x.DteDocumentoId==reservation.DocumentId && x.EmpresaId==23
                && x.TypeBudget.Campaign.PublicId==BatchPolicy.Campaign && x.TypeBudget.Campaign.EmpresaId==23);
        BatchPolicy.Require(BatchPolicy.ClaimMatches(claim,BatchPolicy.Cases().Single(x=>x.Key==reservation.CaseKey),reservation.PlanHash),"BATCH_FINAL_LEDGER_CHANGED");
        var marker=await db.Auditoria.SingleAsync(x=>x.Id==reservation.AttemptId && x.EmpresaId==23 && x.Accion==BatchPolicy.AttemptAction && x.EntidadId==Entity);
        BatchPolicy.Require(marker.Resultado=="IN_PROGRESS" && marker.DatosDespues==JsonSerializer.Serialize(reservation),"BATCH_COMPLETION_CONFLICT");
        var stopped=await db.Auditoria.AnyAsync(x=>x.EmpresaId==23 && x.Accion==BatchPolicy.StopAction && x.EntidadId==Entity);
        var access=await CertificationCampaignAccess.ValidateAsync(db,23,document,default);
        var accepted=BatchPolicy.Accepted(document) && access.IsSuccess;
        if(accepted)
        {
            using var response=JsonDocument.Parse(document.Json?.RespuestaHacienda ?? "{}");
            accepted=response.RootElement.TryGetProperty("estado",out var state) && state.GetString()=="PROCESADO"
                && response.RootElement.TryGetProperty("selloRecibido",out var seal) && seal.GetString()==document.SelloRecibido;
        }
        marker.Resultado=accepted ? "ACCEPTED" : "STOPPED";
        if(!accepted && !stopped) db.Auditoria.Add(Audit(BatchPolicy.StopAction,"STOPPED",key:"BATCH_REJECTED_OR_UNCERTAIN_RESULT"));
        await db.SaveChangesAsync(); await tx.CommitAsync();
        BatchPolicy.Require(accepted && !stopped,"BATCH_RESULT_REQUIRES_REVIEW");
    }
    public static async Task StopAsync(BatchEnvironment environment,string code)
    {
        await using var db=environment.Db(); await environment.ValidateSqlAsync(db);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await LockAsync(db);
        if(await db.CertificationCampaigns.AnyAsync(x=>x.PublicId==BatchPolicy.Campaign && x.EmpresaId==23)
            && !await db.Auditoria.AnyAsync(x=>x.EmpresaId==23 && x.Accion==BatchPolicy.StopAction && x.EntidadId==Entity))
        {db.Auditoria.Add(Audit(BatchPolicy.StopAction,"STOPPED",key:code));await db.SaveChangesAsync();}
        await tx.CommitAsync();
    }
}
