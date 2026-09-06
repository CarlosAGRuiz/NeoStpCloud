using System.Text.Json;
using ClientCertificationRunner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Persistence;

namespace ClientCertificationBatch;

// Only fiscal snapshots already used by the accepted pilots; no client/entity navigation is copied.
public sealed record BatchReceiver(string? Type,string? Number,string? Nrc,string? Name,string? Contributor,
    string? Activity,string? ActivityName,string? Department,string? Municipality,string? District,string? Address,
    string? Email,string? Phone,string? Country,string? CountryName,int? PersonType)
{
    public static BatchReceiver From(DteDocumento d)=>new(d.ReceptorTipoDocumento,d.ReceptorNumeroDocumento,d.ReceptorNrc,d.ReceptorNombre,
        d.ReceptorTipoContribuyente,d.ReceptorCodigoActividad,d.ReceptorActividadEconomica,d.ReceptorDepartamentoCodigo,d.ReceptorMunicipioCodigo,
        d.ReceptorDistritoCodigo,d.ReceptorDireccion,d.ReceptorCorreo,d.ReceptorTelefono,d.ReceptorPaisCodigo,d.ReceptorPaisNombre,d.ReceptorTipoPersona);
    public void Apply(DteDocumento d)
    {
        d.ReceptorTipoDocumento=Type; d.ReceptorNumeroDocumento=Number; d.ReceptorNrc=Nrc; d.ReceptorNombre=Name; d.ReceptorTipoContribuyente=Contributor;
        d.ReceptorCodigoActividad=Activity; d.ReceptorActividadEconomica=ActivityName; d.ReceptorDepartamentoCodigo=Department;
        d.ReceptorMunicipioCodigo=Municipality; d.ReceptorDistritoCodigo=District; d.ReceptorDireccion=Address; d.ReceptorCorreo=Email;
        d.ReceptorTelefono=Phone; d.ReceptorPaisCodigo=Country; d.ReceptorPaisNombre=CountryName; d.ReceptorTipoPersona=PersonType;
    }
}
public sealed record BatchPilot(int Id,string Type,BatchReceiver Receiver,string JsonHash,string ResponseHash);
public sealed record BatchSnapshot(BatchEnvironment Environment,Empresa Company,DteConfiguracion Fiscal,string Block,
    IReadOnlyDictionary<string,BatchPilot> Pilots,SchemaCheckedGenerator Generator,string PlanHash,object Evidence)
{
    public DteDocumento Create(BatchCase item)
    {
        var doc=PilotFixture.Create(item.Type,Block,DateTime.UtcNow.AddHours(-6));
        Pilots[item.Type].Receiver.Apply(doc);
        var line=doc.Detalles.Single();
        line.Cantidad=item.Quantity; line.PrecioUnitario=item.Price;
        line.Descripcion="SERVICIO SINTETICO DE CERTIFICACION "+item.Type+" "+item.Ordinal.ToString("D3");
        line.Codigo="CERT-BATCH-"+item.Type+"-"+item.Ordinal.ToString("D3");
        new DteCalculator().Recalcular(doc);
        return doc;
    }
    public void ValidateCase(BatchCase item)
    {
        var doc=Create(item); doc.Empresa=Company;
        var result=Generator.Generar(doc,Fiscal);
        BatchPolicy.Require(result.IsSuccess,"BATCH_SCHEMA_"+item.Key+"_REJECTED");
        BatchPolicy.Require(DteFiscalContext.TryGetGeneratedVersion(result.Value,doc,out var version) && version==doc.VersionDte,"BATCH_PREVIEW_IDENTITY_REJECTED");
    }
    public static async Task<BatchSnapshot> ReadAsync(BatchEnvironment environment,NeoStpDbContext db)
    {
        var company=await db.Empresas.AsNoTracking().Where(x=>x.Id==23).Select(x=>new Empresa{
            Id=x.Id,Nit=x.Nit,Nrc=x.Nrc,RazonSocial=x.RazonSocial,NombreComercial=x.NombreComercial,EstadoCodigo=x.EstadoCodigo,
            CodigoActividad=x.CodigoActividad,ActividadEconomica=x.ActividadEconomica,Departamento=x.Departamento,Municipio=x.Municipio,
            Distrito=x.Distrito,Direccion=x.Direccion,Telefono=x.Telefono,Correo=x.Correo}).SingleAsync();
        var fiscal=await db.DteConfiguracion.AsNoTracking().Where(x=>x.EmpresaId==23).Select(x=>new DteConfiguracion{
            EmpresaId=x.EmpresaId,AmbienteCodigo=x.AmbienteCodigo,TiposDteAutorizadosCsv=x.TiposDteAutorizadosCsv,
            TipoEstablecimientoCodigo=x.TipoEstablecimientoCodigo,CodigoEstablecimientoMh=x.CodigoEstablecimientoMh,CodigoPuntoVentaMh=x.CodigoPuntoVentaMh}).SingleAsync();
        BatchPolicy.Require(BatchPolicy.Normalize(company.Nit)==BatchPolicy.Nit && company.EstadoCodigo=="ACTIVA"
            && fiscal.AmbienteCodigo=="PRUEBAS" && fiscal.TiposDteAutorizadosCsv=="01,03,11,14","BATCH_FISCAL_AUTHORITY_REJECTED");
        company.Nit=BatchPolicy.Normalize(company.Nit); company.Nrc=BatchPolicy.Normalize(company.Nrc);
        var metadata=new List<(string Catalog,LookupItem Item)>();
        await using(var command=db.Database.GetDbConnection().CreateCommand())
        {
            if(db.Database.CurrentTransaction is {} tx) command.Transaction=tx.GetDbTransaction();
            command.CommandText="""
                SELECT c.Codigo,c.Activo,i.Codigo,i.Valor,i.ParentCodigo,i.MetadataJson
                FROM Core_Catalogos c JOIN Core_CatalogoItems i ON i.CatalogoId=c.Id AND i.Activo=1
                WHERE c.Codigo IN ('DEPARTAMENTO_ES','MUNICIPIO_ES','DISTRITO_ES','TIPO_ESTABLECIMIENTO')
                AND (c.EmpresaId=23 OR (c.EmpresaId IS NULL AND NOT EXISTS (SELECT 1 FROM Core_Catalogos t WHERE t.Codigo=c.Codigo AND t.EmpresaId=23)))
                ORDER BY c.Codigo,i.Codigo
                """;
            await using var r=await command.ExecuteReaderAsync();
            while(await r.ReadAsync())
            {
                BatchPolicy.Require(r.GetBoolean(1),"BATCH_CATALOG_INACTIVE");
                metadata.Add((r.GetString(0),new LookupItem(r.GetString(2),r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),r.IsDBNull(5)?null:r.GetString(5))));
            }
        }
        LookupItem[] Items(string name)=>metadata.Where(x=>x.Catalog==name).Select(x=>x.Item).ToArray();
        var territory=DteTerritoryResolver.Resolve(company.Departamento,company.Municipio,company.Distrito,
            Items("DEPARTAMENTO_ES"),Items("MUNICIPIO_ES"),Items("DISTRITO_ES"),true);
        BatchPolicy.Require(territory.IsSuccess && territory.Value!.Department=="05" && territory.Value.Municipality=="24" && territory.Value.District=="15","BATCH_TERRITORY_REJECTED");
        company.Departamento=territory.Value!.Department; company.Municipio=territory.Value.Municipality; company.Distrito=territory.Value.District;
        var establishment=Items("TIPO_ESTABLECIMIENTO").Where(x=>x.Value==fiscal.TipoEstablecimientoCodigo || x.Label==fiscal.TipoEstablecimientoCodigo).ToArray();
        if(establishment.Length==1)
        {
            using var meta=JsonDocument.Parse(establishment[0].Meta ?? "{}");
            if(meta.RootElement.TryGetProperty("codigoMH",out var code)) fiscal.TipoEstablecimientoCodigo=code.GetString()!;
        }
        BatchPolicy.Require(fiscal.TipoEstablecimientoCodigo is "01" or "02" or "04" or "07" or "20","BATCH_ESTABLISHMENT_CODE_REJECTED");
        var block=RunnerPolicy.Establishment(fiscal);
        var pilot=await db.CertificationCampaigns.AsNoTracking().Include(x=>x.TypeBudgets).SingleAsync(x=>x.PublicId==RunnerPolicy.CampaignId && x.EmpresaId==23);
        RunnerPolicy.Campaign(pilot,RunnerPolicy.CampaignId,DateTimeOffset.UtcNow);
        var claims=await db.CertificationCampaignConsumptions.AsNoTracking().Where(x=>x.CampaignId==pilot.Id && x.EmpresaId==23)
            .Include(x=>x.Document).ThenInclude(x=>x.Json).Include(x=>x.Document).ThenInclude(x=>x.Detalles).ToListAsync();
        BatchPolicy.Require(claims.Count==4 && claims.Select(x=>x.TipoDteCodigo).Order().SequenceEqual(BatchPolicy.Budgets.Select(x=>x.Type)),"BATCH_FOUR_PILOTS_REQUIRED");
        var neo=await NeoReceiverSnapshot.ReadAndVerifyAsync(db,default);
        var pilots=new Dictionary<string,BatchPilot>();
        foreach(var claim in claims)
        {
            var d=claim.Document;
            BatchPolicy.Require(BatchPolicy.Accepted(d) && d.Json is not null && d.Detalles.Count==1
                && d.ClienteId is null && d.DocumentoRelacionadoId is null && d.SucursalId is null && d.PuntoVentaId is null,"BATCH_ACCEPTED_PILOT_REQUIRED");
            var expectedId=d.TipoDteCodigo switch {"01"=>1016,"03"=>1017,"11"=>1018,_=>d.Id};
            BatchPolicy.Require(d.Id==expectedId,"BATCH_PILOT_ID_REJECTED");
            var access=await CertificationCampaignAccess.ValidateAsync(db,23,d,default);
            BatchPolicy.Require(access.IsSuccess,access.ErrorCode ?? "BATCH_PILOT_LEDGER_REJECTED");
            BatchPolicy.Require(DteFiscalContext.TryGetGeneratedVersion(d.Json!.JsonDte,d,out var version) && version==d.VersionDte
                && DteFiscalContext.CoincideRecepcion(new HaciendaReceptionRequest{AmbienteCodigo="PRUEBAS",Ambiente="00",Version=d.VersionDte,
                    TipoDte=d.TipoDteCodigo,CodigoGeneracion=d.CodigoGeneracion,Documento=d.Json.JsonFirmado!}),"BATCH_PILOT_JSON_MISMATCH");
            using(var response=JsonDocument.Parse(d.Json.RespuestaHacienda ?? "{}"))
                BatchPolicy.Require(response.RootElement.GetProperty("estado").GetString()=="PROCESADO"
                    && response.RootElement.GetProperty("selloRecibido").GetString()==d.SelloRecibido,"BATCH_PILOT_RECEIPT_MISMATCH");
            if(d.TipoDteCodigo=="03")
            {
                var expected=PilotFixture.Create("03",block,DateTime.UtcNow); expected.Id=1017; neo.ApplyToReviewedDocument(expected);
                BatchPolicy.Require(BatchReceiver.From(d)==BatchReceiver.From(expected),"BATCH_NEO_ACCEPTED_RECEIVER_CHANGED");
            }
            if(d.TipoDteCodigo=="01") BatchPolicy.Require(d.ReceptorTipoDocumento is null && d.ReceptorNumeroDocumento is null,"BATCH_CF_RECEIVER_REJECTED");
            if(d.TipoDteCodigo=="14") BatchPolicy.Require(d.ReceptorTipoDocumento=="37" && d.ReceptorNumeroDocumento=="CERT-SE-PILOT-001","BATCH_FSE_PASSPORT_REQUIRED");
            pilots.Add(d.TipoDteCodigo,new(d.Id,d.TipoDteCodigo,BatchReceiver.From(d),BatchPolicy.Hash(d.Json.JsonDte),BatchPolicy.Hash(d.Json.RespuestaHacienda!)));
        }
        var schemasRoot=BatchEnvironment.SafePath(environment.Repo,"tools/CertHarness/schemas/svfe-json-schemas");
        var schemas=await SchemaCheckedGenerator.Load(schemasRoot);
        var schemaHashes=new[]{"v2/fe-f-v2.json","v4/fe-ccf-v4.json","v3/fe-fex-v3.json","v2/fe-fse-v2.json"}
            .ToDictionary(x=>x,x=>BatchPolicy.FileHash(BatchEnvironment.SafePath(schemasRoot,x)));
        var generator=new SchemaCheckedGenerator(new DteGeneratorService(Options.Create(new TerritorialOptions()),environment.Configuration),schemas);
        var evidence=new {BatchPolicy.Version,Campaign=BatchPolicy.Campaign,Budgets=BatchPolicy.Budgets.Select(x=>new{x.Type,x.Count}),
            environment.ReleaseId,environment.InfrastructureHash,environment.ToolHash,environment.RuntimeHashes,SchemaHashes=schemaHashes,
            Pilots=pilots.OrderBy(x=>x.Key).Select(x=>new{x.Value.Id,x.Value.Type,x.Value.JsonHash,x.Value.ResponseHash,
                ReceiverHash=BatchPolicy.Hash(JsonSerializer.Serialize(x.Value.Receiver))}),
            FiscalHash=BatchPolicy.Hash(JsonSerializer.Serialize(new {company.Nit,company.Nrc,company.RazonSocial,company.NombreComercial,
                company.CodigoActividad,company.ActividadEconomica,company.Departamento,company.Municipio,company.Distrito,company.Direccion,company.Correo,company.Telefono,
                fiscal.TipoEstablecimientoCodigo,fiscal.CodigoEstablecimientoMh,fiscal.CodigoPuntoVentaMh,Catalogs=metadata.Select(x=>new{x.Catalog,x.Item})})),NeoSnapshotHash=neo.Hash()};
        return new(environment,company,fiscal,block,pilots,generator,BatchPolicy.Hash(JsonSerializer.Serialize(evidence)),evidence);
    }
}
