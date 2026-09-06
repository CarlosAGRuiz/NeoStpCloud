using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClientCertificationPreview;
using ClientCertificationRunner;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Infrastructure.Persistence;

if (args.SequenceEqual(new[] { "--self-test" })) return RunnerSelfTests.Run();
var report = new Dictionary<string, object?> { ["AtUtc"] = DateTimeOffset.UtcNow, ["EmpresaId"] = 23, ["Passed"] = false,
    ["OfficialCasesCompleted"] = 0, ["SqlWritesIssued"] = false, ["HaciendaReceptionAttempts"] = 0, ["PayloadsExported"] = false };
var repo = Path.GetFullPath(Environment.CurrentDirectory);
var output = Path.Combine(repo, "tmp/client-certification-runner", DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N") + ".json");
PilotTransport? transport = null;
try
{
    var options = RunnerArguments.Parse(args);
    report["Mode"] = options.ReviewedRetry03 ? "REVIEWED_RETRY_1017_ONLY" : options.ReviewedRetry ? "REVIEWED_RETRY_1016_ONLY" : options.RehearseClone ? "REHEARSE_CLONE_ROLLBACK" : options.Pilot ? "PILOT" : "PREVIEW";
    report["CampaignPublicId"] = options.Campaign;
    var configuration = new ConfigurationBuilder().SetBasePath(Path.Combine(repo, "out/local-autostart/api"))
        .AddJsonFile("appsettings.json", optional: false).AddJsonFile("appsettings.Development.json", optional: true)
        .AddJsonFile("appsettings.Local.json", optional: false)
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Dte:EsquemaNuevo"] = "true" }).Build();
    report["NewSchemaOverride"] = true;
    report["PublishedConfigurationModified"] = false;
    report["GeneratorOverrideSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Dte:EsquemaNuevo=true")));
    var sqlBuilder = new SqlConnectionStringBuilder(configuration.GetConnectionString("NeoStpDb"));
    RunnerPolicy.Require(sqlBuilder.InitialCatalog == "NeoSTP_Cloud" && sqlBuilder.AttachDBFilename.Length == 0
        && new[] { ".", "(local)", "localhost", "127.0.0.1", Environment.MachineName }.Contains(sqlBuilder.DataSource, StringComparer.OrdinalIgnoreCase), "SQL_TARGET_REJECTED");
    var target = "NeoSTP_Cloud";
    if (options.RehearseClone)
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, "tmp/client-certification-release-2026-09-05/clone-manifest.json")));
        var m = manifest.RootElement; var id = m.GetProperty("RunId").GetString();
        target = m.GetProperty("TargetDatabase").GetString()!;
        RunnerPolicy.Require(System.Text.RegularExpressions.Regex.IsMatch(id ?? "", "\\A[a-f0-9]{32}\\z") && target == "NeoProductionAudit_" + id
            && m.GetProperty("SourceDatabase").GetString() == "NeoSTP_Cloud" && m.GetProperty("Restored").GetBoolean()
            && m.GetProperty("Verified").GetBoolean() && m.GetProperty("BackupCompleted").GetBoolean(), "CLONE_MANIFEST_REJECTED");
        sqlBuilder.InitialCatalog = target;
        report["TargetClone"] = target;
    }
    var dbOptions = new DbContextOptionsBuilder<NeoStpDbContext>().UseSqlServer(sqlBuilder.ConnectionString).Options;
    NeoStpDbContext Db() => new(dbOptions);
    await using var read = Db();
    await read.Database.OpenConnectionAsync();
    await using (var command = read.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "SELECT DB_NAME(),CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),CONVERT(int,SERVERPROPERTY('ProductMajorVersion'))";
        await using var row = await command.ExecuteReaderAsync();
        RunnerPolicy.Require(await row.ReadAsync() && row.GetString(0) == target
            && row.GetString(1).Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) && row.GetInt32(2) == 16, "SQL_IDENTITY_REJECTED");
    }
    var expectedMigrations = read.Database.GetMigrations().ToArray();
    var applied = (await read.Database.GetAppliedMigrationsAsync()).ToArray();
    RunnerPolicy.Require(expectedMigrations.Length == 91 && applied.SequenceEqual(expectedMigrations), "EXACT_91_MIGRATIONS_REQUIRED");
    report["Exact91MigrationsVerified"] = true;
    async Task ValidateIdentity(NeoStpDbContext db, CancellationToken ct = default)
    {
        var identity = await db.Empresas.AsNoTracking().Where(x => x.Id == 23).Select(x => new { x.Nit, x.EstadoCodigo }).SingleOrDefaultAsync(ct);
        var fiscal = await db.DteConfiguracion.AsNoTracking().Where(x => x.EmpresaId == 23)
            .Select(x => new { x.AmbienteCodigo, x.TiposDteAutorizadosCsv }).SingleOrDefaultAsync(ct);
        RunnerPolicy.Require(identity?.Nit.Replace("-", "").Replace(" ", "") == RunnerPolicy.Nit && identity.EstadoCodigo == "ACTIVA"
            && fiscal?.AmbienteCodigo == "PRUEBAS" && RunnerPolicy.ExactCsv(fiscal.TiposDteAutorizadosCsv), "CLIENT_FISCAL_AUTHORITY_REJECTED");
    }
    await ValidateIdentity(read);
    var company = await read.Empresas.AsNoTracking().Where(x => x.Id == 23).Select(x => new Empresa {
        Id = x.Id, Nit = x.Nit, Nrc = x.Nrc, RazonSocial = x.RazonSocial, NombreComercial = x.NombreComercial,
        CodigoActividad = x.CodigoActividad, ActividadEconomica = x.ActividadEconomica, Departamento = x.Departamento,
        Municipio = x.Municipio, Distrito = x.Distrito, Direccion = x.Direccion, Correo = x.Correo, Telefono = x.Telefono }).SingleAsync();
    company.Nit = company.Nit.Replace("-", "").Replace(" ", "");
    company.Nrc = company.Nrc?.Replace("-", "").Replace(" ", "");
    var fiscalConfig = await read.DteConfiguracion.AsNoTracking().Where(x => x.EmpresaId == 23).Select(x => new DteConfiguracion {
        EmpresaId = 23, AmbienteCodigo = x.AmbienteCodigo, TipoEstablecimientoCodigo = x.TipoEstablecimientoCodigo,
        CodigoEstablecimientoMh = x.CodigoEstablecimientoMh, CodigoPuntoVentaMh = x.CodigoPuntoVentaMh,
        TiposDteAutorizadosCsv = x.TiposDteAutorizadosCsv }).SingleAsync();
    var catalogs = new List<CatalogItem>();
    await using (var command = read.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = """
            SELECT c.Codigo,c.EmpresaId,c.Activo,i.Codigo,i.Valor,i.ParentCodigo,JSON_VALUE(i.MetadataJson,'$.codigoMH')
            FROM Core_Catalogos c JOIN Core_CatalogoItems i ON i.CatalogoId=c.Id AND i.Activo=1
            WHERE c.Codigo IN ('DEPARTAMENTO_ES','MUNICIPIO_ES','DISTRITO_ES','TIPO_ESTABLECIMIENTO')
              AND (c.EmpresaId=23 OR (c.EmpresaId IS NULL AND NOT EXISTS (SELECT 1 FROM Core_Catalogos t WHERE t.Codigo=c.Codigo AND t.EmpresaId=23)))
            """;
        await using var rows = await command.ExecuteReaderAsync();
        while (await rows.ReadAsync())
        {
            RunnerPolicy.Require(rows.GetBoolean(2), "CATALOG_INACTIVE");
            catalogs.Add(new(rows.GetString(0), rows.GetString(3), rows.GetString(4), rows.IsDBNull(5) ? null : rows.GetString(5), rows.IsDBNull(6) ? null : rows.GetString(6)));
        }
    }
    LookupItem[] Items(string name) => catalogs.Where(x => x.Catalog == name).Select(x => new LookupItem(x.Code, x.Label, x.Parent, JsonSerializer.Serialize(new { codigoMH = x.MhCode }))).ToArray();
    var verifiedTerritory = DteTerritoryResolver.Resolve(company.Departamento, company.Municipio, company.Distrito,
        Items("DEPARTAMENTO_ES"), Items("MUNICIPIO_ES"), Items("DISTRITO_ES"), true);
    RunnerPolicy.Require(verifiedTerritory.IsSuccess && verifiedTerritory.Value!.Department == "05"
        && verifiedTerritory.Value.Municipality == "24" && verifiedTerritory.Value.District == "15", "CLIENT_CENTRO_OPICO_TERRITORY_REQUIRED");
    var block = RunnerPolicy.Establishment(fiscalConfig);
    fiscalConfig.TipoEstablecimientoCodigo = PreviewEngine.Map(catalogs, "TIPO_ESTABLECIMIENTO", fiscalConfig.TipoEstablecimientoCodigo);
    var schemas = await SchemaCheckedGenerator.Load(Path.Combine(repo, "tools/CertHarness/schemas/svfe-json-schemas"));
    var territoryOptions = new TerritorialOptions(); configuration.GetSection(TerritorialOptions.SectionName).Bind(territoryOptions);
    var checkedGenerator = new SchemaCheckedGenerator(new DteGeneratorService(Options.Create(territoryOptions), configuration), schemas);
    var previews = new List<object>();
    var allPreviewsPassed = true;
    var fixtureHashes = new Dictionary<string, string>();
    foreach (var type in RunnerPolicy.Types)
    {
        var doc = PilotFixture.Create(type, block, DateTime.UtcNow.AddHours(-6));
        var territory = DteTerritoryResolver.Resolve(company.Departamento, company.Municipio, company.Distrito,
            Items("DEPARTAMENTO_ES"), Items("MUNICIPIO_ES"), Items("DISTRITO_ES"), DteGeneratorService.RequiereTerritorio2024(type, configuration.GetValue<bool>("Dte:EsquemaNuevo")));
        RunnerPolicy.Require(territory.IsSuccess, "TERRITORY_VERSION_REJECTED");
        company.Departamento = territory.Value!.Department; company.Municipio = territory.Value.Municipality; company.Distrito = territory.Value.District;
        doc.Empresa = company;
        doc.ReceptorDepartamentoCodigo = territory.Value.Department; doc.ReceptorMunicipioCodigo = territory.Value.Municipality; doc.ReceptorDistritoCodigo = territory.Value.District;
        new DteCalculator().Recalcular(doc);
        var generated = checkedGenerator.Generar(doc, fiscalConfig);
        previews.Add(new { Type = type, SchemaPassed = generated.IsSuccess, Code = generated.ErrorCode, Issues = checkedGenerator.LastIssues });
        allPreviewsPassed &= generated.IsSuccess;
        // Stable request identity excludes volatile date/correlative. Includes template and fiscal metadata.
        fixtureHashes[type] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            PilotFixture.Version, Type = type, VersionDte = doc.VersionDte, doc.ReceptorTipoDocumento, doc.ReceptorNumeroDocumento,
            company.Nit, company.Nrc, company.RazonSocial, company.NombreComercial,
            company.CodigoActividad, company.ActividadEconomica, company.Departamento, company.Municipio, company.Distrito,
            company.Direccion, company.Correo, company.Telefono, fiscalConfig.TipoEstablecimientoCodigo,
            fiscalConfig.CodigoEstablecimientoMh, fiscalConfig.CodigoPuntoVentaMh,
            NewSchema = configuration.GetValue<bool>("Dte:EsquemaNuevo") }))));
        if (options.Pilot && options.Type == type) RunnerPolicy.Require(generated.IsSuccess, generated.ErrorCode ?? "PILOT_FIXTURE_REJECTED");
        // Restore the stored hierarchy for the next version's resolver.
        company.Departamento = verifiedTerritory.Value!.Department; company.Municipio = verifiedTerritory.Value.Municipality; company.Distrito = verifiedTerritory.Value.District;
    }
    report["Preview"] = previews;
    if (options.Campaign is Guid campaignId)
    {
        var campaign = await read.CertificationCampaigns.AsNoTracking().Include(x => x.TypeBudgets).SingleOrDefaultAsync(x => x.PublicId == campaignId && x.EmpresaId == 23);
        RunnerPolicy.Campaign(campaign, campaignId, DateTimeOffset.UtcNow); report["PilotCampaignVerified"] = true;
    }
    if (options.RehearseClone)
    {
        var documentsBefore = await read.DteDocumentos.CountAsync();
        var consumedBefore = await read.CertificationCampaignConsumptions.CountAsync();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var commercialBefore = await CertificationCampaignAccess.CountCommercialDocumentsAsync(read, 23, monthStart, default);
        var countersBefore = await read.DteCorrelativos.AsNoTracking().OrderBy(x => x.EmpresaId).ThenBy(x => x.TipoDteCodigo).Select(x => new { x.EmpresaId, x.TipoDteCodigo, x.UltimoCorrelativo, x.ActualizadoAt }).ToArrayAsync();
        var stagedResults = new List<object>();
        await using (var write = Db())
        {
            await using var tx = await write.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            foreach (var type in RunnerPolicy.Types)
            {
                var doc = PilotFixture.Create(type, block, DateTime.UtcNow.AddHours(-6));
                var request = new CertificationConsumptionRequest(options.Campaign!.Value, 23, RunnerPolicy.Nit, type,
                    RunnerPolicy.Key(type), fixtureHashes[type], RunnerPolicy.Matrix + ":" + type, RunnerPolicy.Actor);
                var result = await new CertificationCampaignQuotaService(write).StageConsumptionAsync(request, doc);
                RunnerPolicy.Require(result.IsSuccess && !result.Value!.Replayed, result.ErrorCode ?? "CLONE_PILOT_MUST_BE_FRESH");
                report["SqlWritesIssued"] = true;
                var number = await DteCorrelativoAllocator.NextAsync(write, 23, type, default);
                doc.NumeroControl = $"DTE-{type}-{block}-{number:D15}";
                await write.SaveChangesAsync(); report["SqlWritesIssued"] = true;
                var access = await CertificationCampaignAccess.ValidateAsync(write, 23, doc, default);
                RunnerPolicy.Require(access.IsSuccess, access.ErrorCode ?? "CLONE_CAMPAIGN_ACCESS_REJECTED");
                // Pure generation on a separate new fixture; no aggregate navigation is attached to EF.
                var proof = PilotFixture.Create(type, block, DateTime.UtcNow.AddHours(-6));
                proof.NumeroControl = doc.NumeroControl; proof.CodigoGeneracion = doc.CodigoGeneracion; proof.Empresa = company;
                new DteCalculator().Recalcular(proof);
                var generated = checkedGenerator.Generar(proof, fiscalConfig);
                RunnerPolicy.Require(generated.IsSuccess, generated.ErrorCode ?? "CLONE_GENERATION_FAILED");
                stagedResults.Add(new { Type = type, PersistedWithinTransaction = doc.Id > 0, SchemaPassed = true });
            }
            RunnerPolicy.Require(await write.DteDocumentos.CountAsync() == documentsBefore + 4
                && await write.CertificationCampaignConsumptions.CountAsync() == consumedBefore + 4, "CLONE_ATOMIC_STAGING_COUNT_REJECTED");
            RunnerPolicy.Require(await CertificationCampaignAccess.CountCommercialDocumentsAsync(write, 23, monthStart, default) == commercialBefore,
                "CLONE_COMMERCIAL_QUOTA_CHANGED");
            var pilot = await write.CertificationCampaigns.AsNoTracking().SingleAsync(x => x.PublicId == options.Campaign);
            var first = await write.DteDocumentos.AsNoTracking().SingleAsync(x => x.IdempotencyScope == "CERT" && x.EmpresaId == 23 && x.TipoDteCodigo == "01"
                && write.CertificationCampaignConsumptions.Any(c => c.CampaignId == pilot.Id && c.DteDocumentoId == x.Id));
            await write.CertificationCampaigns.Where(x => x.Id == pilot.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, DateTimeOffset.UtcNow));
            RunnerPolicy.Require((await CertificationCampaignAccess.ValidateAsync(write, 23, first, default)).ErrorCode == "CERT_CAMPAIGN_INACTIVE", "CLONE_EXPIRED_ACCESS_NOT_BLOCKED");
            await write.CertificationCampaigns.Where(x => x.Id == pilot.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, pilot.ExpiresAtUtc).SetProperty(x => x.Status, "REVOKED"));
            RunnerPolicy.Require((await CertificationCampaignAccess.ValidateAsync(write, 23, first, default)).ErrorCode == "CERT_CAMPAIGN_INACTIVE", "CLONE_REVOKED_ACCESS_NOT_BLOCKED");
            report["CloneCommercialQuotaUnchanged"] = true; report["CloneExpiryAndRevocationBlockAccess"] = true;
            await tx.RollbackAsync();
        }
        var countersAfter = await read.DteCorrelativos.AsNoTracking().OrderBy(x => x.EmpresaId).ThenBy(x => x.TipoDteCodigo).Select(x => new { x.EmpresaId, x.TipoDteCodigo, x.UltimoCorrelativo, x.ActualizadoAt }).ToArrayAsync();
        RunnerPolicy.Require(await read.DteDocumentos.CountAsync() == documentsBefore && await read.CertificationCampaignConsumptions.CountAsync() == consumedBefore
            && JsonSerializer.Serialize(countersBefore) == JsonSerializer.Serialize(countersAfter), "CLONE_ROLLBACK_NOT_VERIFIED");
        report["CloneDraftsRolledBack"] = stagedResults; report["RollbackVerified"] = true;
        report["Passed"] = true; report["NoSigningOrTransmission"] = true;
    }
    else if (!options.Pilot) { report["Passed"] = allPreviewsPassed; report["NoSigningOrTransmission"] = true; }
    else
    {
        RunnerPolicy.Require(configuration["Hacienda:Client"] == "Http" && configuration["Dte:Signer"] == "HaciendaCert", "REAL_TEST_PROVIDERS_REQUIRED");
        var mh = new HaciendaOptions(); configuration.GetSection("Hacienda").Bind(mh);
        RunnerPolicy.Require(mh.PruebasBaseUrl.TrimEnd('/') == RunnerPolicy.BaseUrl, "TEST_ENDPOINT_REQUIRED");
        configuration["Hacienda:PruebasBaseUrl"] = RunnerPolicy.BaseUrl;
        report["TestBaseUrlCanonicalizedInMemory"] = true;
        var credentials = await read.DteConfiguracion.AsNoTracking().Where(x => x.EmpresaId == 23).Select(x => new {
            UserMatches = x.UsuarioMh != null && x.UsuarioMh.Replace("-", "").Replace(" ", "") == RunnerPolicy.Nit,
            PasswordPresent = x.PasswordMhCifrado != null && x.PasswordMhCifrado != "",
            CertificatePresent = x.CertificadoBlob != null && x.CertificadoBlob.Length > 0, x.CertificadoVence }).SingleAsync();
        RunnerPolicy.Require(credentials.UserMatches && credentials.PasswordPresent && credentials.CertificatePresent
            && credentials.CertificadoVence > DateTime.UtcNow, "CLIENT_TEST_CREDENTIAL_METADATA_REQUIRED");
        var material = await read.DteConfiguracion.AsNoTracking().Where(x => x.EmpresaId == 23)
            .Select(x => new { x.CertificadoBlob, x.PasswordMhCifrado }).SingleAsync();
        try
        {
            RunnerPolicy.Require(CredentialPreflight.CertificatePairValid(material.CertificadoBlob), "CLIENT_CERTIFICADO_MH_KEYPAIR_INVALID");
            var keyServices = new ServiceCollection(); keyServices.AddLogging(x => x.ClearProviders());
            keyServices.AddNeoStpDataProtection(configuration); keyServices.AddDataProtection().DisableAutomaticKeyGeneration();
            await using var keys = keyServices.BuildServiceProvider();
            var protector = new DataProtectionSecretProtector(keys.GetRequiredService<IDataProtectionProvider>());
            RunnerPolicy.Require(CredentialPreflight.HaciendaPasswordCanBeDecrypted(protector, material.PasswordMhCifrado), "CLIENT_MH_PASSWORD_UNPROTECT_FAILED");
            report["CertificateMhXmlKeyPairVerifiedLocally"] = true;
            report["CertificatePasswordRequired"] = false;
            report["HaciendaPasswordDecryptableBeforeReservation"] = true;
            report["PreflightFiscalSignatureOrAuthenticationPerformed"] = false;
        }
        finally { if (material.CertificadoBlob is not null) CryptographicOperations.ZeroMemory(material.CertificadoBlob); }
        var selectedType = options.Type!; var selectedCampaign = options.Campaign!.Value;
        int? reservedId = null;
        async Task Preflight(CancellationToken ct)
        {
            await using var check = Db(); await ValidateIdentity(check, ct);
            var active = await check.CertificationCampaigns.AsNoTracking().Include(x => x.TypeBudgets).SingleOrDefaultAsync(x => x.PublicId == selectedCampaign && x.EmpresaId == 23, ct);
            RunnerPolicy.Campaign(active, selectedCampaign, DateTimeOffset.UtcNow);
            if (reservedId is int dteId)
            {
                var dte = await check.DteDocumentos.AsNoTracking().SingleAsync(x => x.Id == dteId && x.EmpresaId == 23, ct);
                var access = await CertificationCampaignAccess.ValidateAsync(check, 23, dte, ct);
                RunnerPolicy.Require(access.IsSuccess, access.ErrorCode ?? "PILOT_ACCESS_REJECTED");
            }
        }
        await Preflight(default);
        int documentId; bool replayed;
        ReviewedRetry1016.Preparation? retryPreparation = null;
        ReviewedRetry1017.Preparation? retry03Preparation = null;
        if (options.ReviewedRetry03)
        {
            await using var reviewed = Db();
            var prepared = await ReviewedRetry1017.PrepareAsync(reviewed, checkedGenerator, fiscalConfig, company,
                Items("DEPARTAMENTO_ES"), Items("MUNICIPIO_ES"), Items("DISTRITO_ES"));
            retry03Preparation = prepared;
            documentId = 1017; replayed = prepared.Replayed;
            report["ReviewedRetry03"] = prepared;
            report["ReceiverSnapshotSha256"] = NeoReceiverSnapshot.ExpectedHash;
            report["SqlWritesIssued"] = !replayed;
        }
        else if (options.ReviewedRetry)
        {
            await using var reviewed = Db();
            var prepared = await ReviewedRetry1016.PrepareAsync(reviewed);
            retryPreparation = prepared;
            documentId = 1016; replayed = prepared.Replayed;
            report["ReviewedRetry"] = prepared;
            report["SqlWritesIssued"] = !replayed;
        }
        else await using (var write = Db())
        {
            await using var tx = await write.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var doc = PilotFixture.Create(selectedType, block, DateTime.UtcNow.AddHours(-6));
            var request = new CertificationConsumptionRequest(selectedCampaign, 23, RunnerPolicy.Nit, selectedType,
                RunnerPolicy.Key(selectedType), fixtureHashes[selectedType], RunnerPolicy.Matrix + ":" + selectedType, RunnerPolicy.Actor);
            var staged = await new CertificationCampaignQuotaService(write).StageConsumptionAsync(request, doc);
            RunnerPolicy.Require(staged.IsSuccess, staged.ErrorCode ?? "PILOT_RESERVATION_REJECTED");
            replayed = staged.Value!.Replayed;
            if (!replayed)
            {
                report["SqlWritesIssued"] = true;
                var number = await DteCorrelativoAllocator.NextAsync(write, 23, selectedType, default);
                doc.NumeroControl = $"DTE-{selectedType}-{block}-{number:D15}";
                await write.SaveChangesAsync(); report["SqlWritesIssued"] = true;
            }
            documentId = staged.Value.Consumption.DteDocumentoId;
            await tx.CommitAsync();
        }
        report["DteId"] = documentId; report["ReplayedReadOnly"] = replayed;
        reservedId = documentId;
        if (!replayed)
        {
            await Preflight(default);
            var stored = await read.DteDocumentos.AsNoTracking().SingleAsync(x => x.Id == documentId && x.EmpresaId == 23);
            RunnerPolicy.Require(options.ReviewedRetry03 ? ReviewedRetry1017.MayRegenerateReviewed(stored)
                : options.ReviewedRetry ? ReviewedRetry1016.MayRegenerateReviewed(stored) : RunnerPolicy.MayStartTransmission(stored), "EXISTING_ATTEMPT_REQUIRES_RECONCILIATION");
            transport = new PilotTransport(Preflight);
            var services = new ServiceCollection(); services.AddLogging(x => x.ClearProviders()); services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);
            services.Configure<HaciendaOptions>(configuration.GetSection("Hacienda"));
            services.RemoveAll<IDistributedCache>(); services.AddDistributedMemoryCache();
            services.AddDataProtection().DisableAutomaticKeyGeneration();
            services.AddSingleton<IHttpClientFactory>(transport);
            services.AddScoped<ITenantEmailSender, BlockedTenantEmail>(); services.AddScoped<IConnectWebhookDispatcher, BlockedWebhooks>();
            services.AddScoped<IDteGeneratorService>(_ => checkedGenerator);
            await using var provider = services.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IDteDocumentosService>();
            var sent = await service.EnviarAsync(23, documentId, RunnerPolicy.Actor);
            report["PipelineSuccess"] = sent.IsSuccess; report["PipelineCode"] = sent.ErrorCode;
        }
        await using var finalDb = Db();
        var final = await finalDb.DteDocumentos.AsNoTracking().Where(x => x.Id == documentId && x.EmpresaId == 23)
            .Select(x => new { x.Id, x.EstadoCodigo, x.EnviadoAt, x.VersionDte, x.CodigoGeneracion, x.NumeroControl, HasSeal = x.SelloRecibido != null && x.SelloRecibido != "" }).SingleAsync();
        if (retryPreparation is not null)
        {
            var preserved = await finalDb.CertificationCampaignConsumptions.AsNoTracking().SingleAsync(x => x.EmpresaId == 23 && x.DteDocumentoId == documentId);
            RunnerPolicy.Require(final.CodigoGeneracion == retryPreparation.CodigoGeneracion && final.NumeroControl == retryPreparation.NumeroControl
                && preserved.Id == retryPreparation.ConsumptionId && preserved.RequestHash == retryPreparation.OriginalRequestHash
                && preserved.IdempotencyKeyHash == retryPreparation.OriginalIdempotencyKeyHash, "REVIEWED_RETRY_IDENTITY_OR_LEDGER_CHANGED");
            report["ReviewedRetryOriginalIdentityAndLedgerPreserved"] = true;
        }
        report["Document"] = final;
        if (retry03Preparation is not null)
        {
            var preserved = await finalDb.CertificationCampaignConsumptions.AsNoTracking().SingleAsync(x => x.EmpresaId == 23 && x.DteDocumentoId == documentId);
            RunnerPolicy.Require(final.CodigoGeneracion == retry03Preparation.CodigoGeneracion && final.NumeroControl == retry03Preparation.NumeroControl
                && preserved.Id == retry03Preparation.ConsumptionId && preserved.RequestHash == retry03Preparation.OriginalRequestHash
                && preserved.IdempotencyKeyHash == retry03Preparation.OriginalIdempotencyKeyHash, "REVIEWED_RETRY_1017_IDENTITY_OR_LEDGER_CHANGED");
            await NeoReceiverSnapshot.ReadAndVerifyAsync(finalDb, default);
            report["ReviewedRetryOriginalIdentityAndLedgerPreserved"] = true;
            report["NeoPublicFiscalSnapshotUnchanged"] = true;
        }
        var response = await finalDb.DteDocumentoJson.AsNoTracking().Where(x => x.DocumentoId == documentId).Select(x => x.RespuestaHacienda).SingleOrDefaultAsync();
        var diagnosis = DteDiagnosticoGuia.Crear(final.EstadoCodigo, final.HasSeal ? "PRESENT" : null, final.EnviadoAt, response);
        report["NeedsReconciliation"] = diagnosis.RequiereConsultaHacienda;
        report["DiagnosticCode"] = diagnosis.Codigo;
        report["SuggestedNextStep"] = diagnosis.SiguientePaso;
        report["Passed"] = final.EstadoCodigo == "PROCESADO" && final.HasSeal;
        // Local acceptance does not establish portal counter/matrix completion.
    }
}
catch (Exception error)
{
    report["ErrorType"] = error.GetType().Name; report["Code"] = error is RunnerRejected rejected ? rejected.Code : "RUNNER_FAILURE_REVIEW_REQUIRED";
    report["Passed"] = false;
}
finally
{
    report["HaciendaReceptionAttempts"] = transport?.ReceptionAttempts ?? 0;
    report["HaciendaAuthenticationAttempts"] = transport?.AuthenticationAttempts ?? 0;
    transport?.Dispose();
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Evidence: " + output);
}
return report["Passed"] is true ? 0 : 1;
