using System.Security.Cryptography;
using System.Text.Json;
using ClientCertificationBatch;
using ClientCertificationRunner;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Infrastructure.Dte;

if(args.SequenceEqual(new[]{"--self-test"})) return await BatchSelfTests.RunAsync();
var report=new Dictionary<string,object?>{["AtUtc"]=DateTimeOffset.UtcNow,["Passed"]=false,["EmpresaId"]=23,
    ["CampaignId"]=BatchPolicy.Campaign,["OfficialPortalVerified"]=false,["OfficialCasesCompleted"]=0,
    ["PlannedRemainingFromCapturedCounters"]=275,["CapturedAtUnknown"]=true,["PayloadsExported"]=false,
    ["NoFurtherReceptionScheduled"]=true,["WorkerStarted"]=false,["EmailsSent"]=0,["WebhooksSent"]=0};
BatchEnvironment? environment=null; BatchReservation? reservation=null; PilotTransport? transport=null;
string? mode=null; bool sqlValidated=false;
try
{
    mode=BatchPolicy.Mode(args); report["Mode"]=mode; report["SqlMutationsRequested"]=mode!="--preview";
    environment=BatchEnvironment.Load(Environment.CurrentDirectory);
    await using var read=environment.Db(); await environment.ValidateSqlAsync(read); sqlValidated=true;
    var snapshot=await BatchSnapshot.ReadAsync(environment,read);
    foreach(var item in BatchPolicy.Cases()) snapshot.ValidateCase(item);
    report["LocalSchemaCasesPassed"]=275; report["FourAcceptedPilotsVerified"]=true;
    report["PlanHash"]=snapshot.PlanHash; report["PlanEvidence"]=snapshot.Evidence;
    report["Before"]=await BatchCampaign.InspectAsync(read,snapshot);
    if(mode=="--preview")
    {
        report["SqlWritesIssued"]=false; report["NoSigningOrAuthentication"]=true;
        report["Passed"]=true; // Successful inventory, not authorization to transmit or a live portal verdict.
    }
    else if(mode=="--prepare-campaign")
    {
        report["CampaignCreated"]=await BatchCampaign.PrepareAsync(environment,snapshot.PlanHash);
        report["Passed"]=true; report["NoSigningOrAuthentication"]=true;
    }
    else
    {
        var configuration=environment.Configuration;
        BatchPolicy.Require(configuration["Hacienda:Client"]=="Http" && configuration["Dte:Signer"]=="HaciendaCert"
            && configuration["Hacienda:PruebasBaseUrl"]?.TrimEnd('/')==RunnerPolicy.BaseUrl,"BATCH_REAL_TEST_PROVIDERS_REQUIRED");
        // Normalize only the local transport endpoint, retaining the tenant schema policy of the active release.
        configuration["Hacienda:PruebasBaseUrl"]=RunnerPolicy.BaseUrl;
        var material=await read.DteConfiguracion.AsNoTracking().Where(x=>x.EmpresaId==23).Select(x=>new{
            x.UsuarioMh,x.CertificadoBlob,x.PasswordMhCifrado,x.CertificadoVence}).SingleAsync();
        try
        {
            BatchPolicy.Require(BatchPolicy.Normalize(material.UsuarioMh)==BatchPolicy.Nit && material.CertificadoVence>DateTime.UtcNow
                && CredentialPreflight.CertificatePairValid(material.CertificadoBlob),"BATCH_TEST_CREDENTIALS_REJECTED");
            var keyServices=new ServiceCollection(); keyServices.AddLogging(x=>x.ClearProviders());
            keyServices.AddNeoStpDataProtection(configuration); keyServices.AddDataProtection().DisableAutomaticKeyGeneration();
            await using var keys=keyServices.BuildServiceProvider();
            BatchPolicy.Require(CredentialPreflight.HaciendaPasswordCanBeDecrypted(new DataProtectionSecretProtector(keys.GetRequiredService<IDataProtectionProvider>()),
                material.PasswordMhCifrado),"BATCH_MH_PASSWORD_UNPROTECT_FAILED");
        }
        finally{if(material.CertificadoBlob is not null) CryptographicOperations.ZeroMemory(material.CertificadoBlob);}
        reservation=await BatchCampaign.ReserveAsync(environment,snapshot.PlanHash);
        if(reservation is null){report["All275LocallyAccepted"]=true;report["Passed"]=true;}
        else
        {
            report["Reservation"]=reservation; report["SqlWritesIssued"]=true;
            await BatchCampaign.PreflightAsync(environment,reservation);
            transport=new PilotTransport(_=>BatchCampaign.PreflightAsync(environment,reservation));
            var services=new ServiceCollection(); services.AddLogging(x=>x.ClearProviders()); services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);
            services.Configure<HaciendaOptions>(configuration.GetSection("Hacienda"));
            services.RemoveAll<IDistributedCache>(); services.AddDistributedMemoryCache();
            services.AddDataProtection().DisableAutomaticKeyGeneration(); services.AddSingleton<IHttpClientFactory>(transport);
            services.AddScoped<ITenantEmailSender,BlockedTenantEmail>(); services.AddScoped<IConnectWebhookDispatcher,BlockedWebhooks>();
            services.AddScoped<IDteGeneratorService>(_=>snapshot.Generator);
            await using var provider=services.BuildServiceProvider(); await using var scope=provider.CreateAsyncScope();
            var service=scope.ServiceProvider.GetRequiredService<IDteDocumentosService>();
            var result=await service.EnviarAsync(23,reservation.DocumentId,BatchPolicy.Actor);
            report["PipelineSuccess"]=result.IsSuccess; report["PipelineCode"]=result.ErrorCode;
            await BatchCampaign.FinishAsync(environment,reservation);
            report["Passed"]=true; report["OneCaseAccepted"]=true;
        }
        await using var final=environment.Db(); await environment.ValidateSqlAsync(final);
        report["After"]=await BatchCampaign.InspectAsync(final,snapshot);
    }
}
catch(Exception error)
{
    report["Passed"]=false; report["FailureType"]=error.GetType().Name;
    var code=error is BatchRejected rejected ? rejected.Code : error is RunnerRejected runner ? runner.Code : "BATCH_REVIEW_REQUIRED";
    report["Code"]=code;
    if(mode=="--run-next" && environment is not null && sqlValidated)
    {
        try{await BatchCampaign.StopAsync(environment,code);report["DurableStopChecked"]=true;}
        catch{report["DurableStopWriteFailed"]=true;report["UnfinishedReservationRemainsBlocking"]=reservation is not null;}
    }
}
finally
{
    report["HaciendaAuthenticationAttempts"]=transport?.AuthenticationAttempts ?? 0;
    report["HaciendaReceptionAttempts"]=transport?.ReceptionAttempts ?? 0; transport?.Dispose();
    var folder=Path.Combine(Environment.CurrentDirectory,"tmp/client-certification-batch");Directory.CreateDirectory(folder);
    var output=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ")+"-"+Guid.NewGuid().ToString("N")+".json");
    await File.WriteAllTextAsync(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine(JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})); Console.WriteLine("Evidence: "+output);
}
return report["Passed"] is true ? 0 : 1;
