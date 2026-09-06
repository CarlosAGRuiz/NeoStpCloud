using System.Text.Json;
using ClientCertificationRunner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace ClientCertificationBatch;

public static class BatchSelfTests
{
    public static async Task<int> RunAsync()
    {
        var passed=0;
        void Check(bool value,string name){if(!value) throw new InvalidOperationException("SELF_TEST_FAILED: "+name);passed++;Console.WriteLine("PASS: "+name);}
        bool Rejected(Action action){try{action();return false;}catch(BatchRejected){return true;}}
        Check(BatchPolicy.Mode([])=="--preview","Default is SELECT-only preview");
        foreach(var args in new[]{new[]{"--run-next","--repeat"},new[]{"--pilot"},new[]{"--prepare-campaign","--tenant","2"},new[]{"--resume"},new[]{"--run-next","--count","275"}})
            Check(Rejected(()=>BatchPolicy.Mode(args)),"Unbounded/foreign/resume arguments rejected");
        var cases=BatchPolicy.Cases();
        Check(cases.Count==275 && cases.Select(x=>x.Key).Distinct().Count()==275 && cases.Select(x=>x.Reference).Distinct().Count()==275,"Exactly275 distinct deterministic cases");
        Check(cases.All(x=>x.Quantity is >=1 and <=5 && x.Price is >=1 and <=10),"Amounts stay in reviewed low-price bounds");
        foreach(var budget in BatchPolicy.Budgets) Check(cases.Count(x=>x.Type==budget.Type)==budget.Count,"Finite budget "+budget.Type);
        Check(BatchPolicy.Campaign!=RunnerPolicy.CampaignId,"New campaign never extends four-document pilot");
        var now=DateTimeOffset.UtcNow;
        var campaign=new CertificationCampaign{PublicId=BatchPolicy.Campaign,EmpresaId=23,ExpectedNit=BatchPolicy.Nit,AmbienteCodigo="PRUEBAS",
            MatrixReference=BatchPolicy.Version,TotalBudget=275,StartsAtUtc=now,ExpiresAtUtc=now.AddHours(48),
            TypeBudgets=BatchPolicy.Budgets.Select(x=>new CertificationCampaignTypeBudget{TipoDteCodigo=x.Type,Budget=x.Count}).ToArray()};
        Check(BatchPolicy.ExactCampaign(campaign),"Exact finite campaign accepted");
        campaign.TotalBudget++;Check(!BatchPolicy.ExactCampaign(campaign),"Budget expansion rejected");campaign.TotalBudget--;
        campaign.ExpiresAtUtc=campaign.ExpiresAtUtc.AddSeconds(1);Check(!BatchPolicy.ExactCampaign(campaign),"Silent period extension rejected");
        var item=cases[0];const string plan="SYNTHETIC_PLAN";
        var doc=PilotFixture.Create(item.Type,"M001P001",DateTime.UtcNow);doc.Id=5000;doc.IdempotencyScope="CERT";
        doc.IdempotencyKeyHash=BatchPolicy.KeyHash(item);doc.IdempotencyRequestHash=BatchPolicy.RequestHash(plan,item);
        var claim=new CertificationCampaignConsumption{Id=1,EmpresaId=23,DteDocumentoId=doc.Id,Document=doc,TipoDteCodigo=item.Type,
            ScenarioReference=item.Reference,IdempotencyKeyHash=doc.IdempotencyKeyHash,RequestHash=doc.IdempotencyRequestHash,CreatedAt=doc.CreatedAt};
        Check(BatchPolicy.ClaimMatches(claim,item,plan),"Exact case/document/ledger hashes match");
        doc.EmpresaId=2;Check(!BatchPolicy.ClaimMatches(claim,item,plan),"Cross-tenant document rejected");doc.EmpresaId=23;
        doc.AmbienteCodigo="PRODUCCION";Check(!BatchPolicy.ClaimMatches(claim,item,plan),"Production document rejected");doc.AmbienteCodigo="PRUEBAS";
        claim.RequestHash=new string('0',64);Check(!BatchPolicy.ClaimMatches(claim,item,plan),"Changed reservation hash rejected");claim.RequestHash=doc.IdempotencyRequestHash;
        Check(!BatchPolicy.ClaimMatches(claim,cases[1],plan),"Another ordinal cannot borrow reservation");
        Check(!BatchPolicy.ClaimMatches(claim,item,plan+"CHANGED"),"Changed plan cannot reuse existing claim");
        Check(!BatchPolicy.Accepted(doc),"Unsent draft is never accepted");
        doc.EstadoCodigo="PROCESADO";doc.EnviadoAt=DateTime.UtcNow;Check(!BatchPolicy.Accepted(doc),"Processed without seal is not accepted");
        doc.SelloRecibido="SYNTHETIC";Check(BatchPolicy.Accepted(doc),"Processed with receipt marker is locally accepted");
        doc.EstadoCodigo="ERROR";Check(!BatchPolicy.Accepted(doc),"Prior error cannot become completion by retaining a seal");
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["Dte:EsquemaNuevo"]="false",["Dte:TenantSchemas:23:Nit"]=BatchPolicy.Nit,
            ["Dte:TenantSchemas:23:Ambiente"]="PRUEBAS",["Dte:TenantSchemas:23:Profile"]="MH_20260825"}).Build();
        var schemas=await SchemaCheckedGenerator.Load(Path.Combine(Environment.CurrentDirectory,"tools/CertHarness/schemas/svfe-json-schemas"));
        var generator=new SchemaCheckedGenerator(new DteGeneratorService(Options.Create(new TerritorialOptions()),cfg),schemas);
        var pilots=new Dictionary<string,BatchPilot>();
        foreach(var type in BatchPolicy.Budgets.Select(x=>x.Type))
        {
            var fixture=PilotFixture.Create(type,"M001P001",DateTime.UtcNow);
            if(type=="03") {fixture.ReceptorNumeroDocumento="06140101001011";fixture.ReceptorNrc="1234567";}
            pilots.Add(type,new(1,type,BatchReceiver.From(fixture),"SYNTHETIC","SYNTHETIC"));
        }
        var company=new Empresa{Id=23,Nit=BatchPolicy.Nit,Nrc="1234567",RazonSocial="EMISOR SINTETICO",NombreComercial="PRUEBA",
            CodigoActividad="62010",ActividadEconomica="PROGRAMACION INFORMATICA",Departamento="05",Municipio="24",Distrito="15",
            Direccion="DIRECCION SINTETICA",Correo="test@example.invalid",Telefono="22000000"};
        var fiscal=new DteConfiguracion{EmpresaId=23,AmbienteCodigo="PRUEBAS",TipoEstablecimientoCodigo="02",CodigoEstablecimientoMh="M001",CodigoPuntoVentaMh="P001"};
        var env=new BatchEnvironment(Environment.CurrentDirectory,"SYNTHETIC",cfg,new DbContextOptionsBuilder<NeoStpDbContext>().Options,"SYNTHETIC","SYNTHETIC",new Dictionary<string,string>());
        var snapshot=new BatchSnapshot(env,company,fiscal,"M001P001",pilots,generator,plan,new{});
        foreach(var next in cases) snapshot.ValidateCase(next);
        Check(true,"All275 varied fixtures validate their exact local schemas without SQL/signature/HTTP");
        var generated=cases.Select(x=>snapshot.Create(x)).ToArray();
        Check(generated.All(x=>x.Id==0 && x.Json is null && x.Empresa is null && x.ClienteId is null && x.IdempotencyScope is null
            && x.EnviadoAt is null && x.Detalles.All(d=>d.Id==0 && d.DocumentoId==0 && d.ProductoId is null)),"All fixtures are detached fresh aggregates without business references");
        Check(generated.Select(x=>x.CodigoGeneracion).Distinct().Count()==275,"Every fresh draft has a unique generation code");
        var r1=BatchPolicy.Hash(JsonSerializer.Serialize(pilots["11"].Receiver));
        var r2=BatchPolicy.Hash(JsonSerializer.Serialize(pilots["11"].Receiver with {Name="CHANGED"}));
        Check(r1!=r2,"Receiver field mutation changes frozen receiver digest");
        var preflights=0;using var transport=new PilotTransport(_=>{preflights++;throw new BatchRejected("SYNTHETIC_PREFLIGHT_BLOCK");});
        using var http=transport.CreateClient("HaciendaReception");
        try{await http.PostAsync(RunnerPolicy.BaseUrl+"/fesv/recepciondte",new StringContent("{}"));Check(false,"No network expected");}
        catch(BatchRejected ex){Check(ex.Code=="SYNTHETIC_PREFLIGHT_BLOCK" && preflights==1 && transport.ReceptionAttempts==0,"Preflight rejection prevents physical HTTP before attempt counter");}
        Check(Rejected(()=>BatchEnvironment.SafePath(Environment.CurrentDirectory,"../outside")),"Path traversal rejected");
        Console.WriteLine(JsonSerializer.Serialize(new{Passed=passed,Failed=0,SchemaCases=275,SqlConnections=0,ProviderRequests=0}));
        return 0;
    }
}
