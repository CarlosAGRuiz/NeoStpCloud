using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ClientCertificationPreview;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

if (args.SequenceEqual(new[] { "--self-test" })) return await PreviewSelfTests.Run();
if (!PreviewEngine.IdentityArgumentsValid(args))
{
    Console.WriteLine("{\"Passed\":false,\"ReasonCode\":\"IDENTITY_ARGUMENT_REJECTED\",\"SqlOpened\":false,\"ReadyForTransmission\":false}");
    return 1;
}
var repo = Path.GetFullPath(Environment.CurrentDirectory);
var output = Path.Combine(repo, "tmp/client-certification-2026-09-05/preview-territory-fixed-sanitized.json");
var evidence = new Dictionary<string, object?> { ["GeneratedAtUtc"] = DateTime.UtcNow, ["EmpresaId"] = 23,
    ["Passed"] = false, ["ReadyForTransmission"] = false, ["SqlWritesIssued"] = false, ["ExternalRequestsIssued"] = false,
    ["SigningPerformed"] = false, ["SecretsOrCertificateSelected"] = false, ["PayloadsExported"] = false,
    ["SyntheticPartyContentAndDocumentIdentifiers"] = true, ["OfficialScenarioCoverageClaimed"] = false,
    ["ConfigurationSource"] = "Published API JSON: base, Development, Local; process-specific overrides not inspected",
    ["RunningProcessConfigurationVerified"] = false, ["UsesApplicationTerritoryResolver"] = true };
evidence["GenerationUsesCurrentSourceNotDeployedBinary"] = true;
evidence["SyntheticRecipientErrorsAreNotRealClientFindings"] = true;
try
{
    var configurationValues = new Dictionary<string, string?>();
    string? connectionString = null;
    foreach (var filename in new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Local.json" })
    {
        var path = Path.Combine(repo, "out/local-autostart/api", filename);
        if (!File.Exists(path)) continue;
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        if (json.RootElement.TryGetProperty("ConnectionStrings", out var connections) && connections.TryGetProperty("NeoStpDb", out var connection))
            connectionString = connection.GetString();
        if (!json.RootElement.TryGetProperty("Dte", out var dte)) continue;
        if (dte.TryGetProperty("EsquemaNuevo", out var toggle)) configurationValues["Dte:EsquemaNuevo"] = toggle.ToString();
        if (!dte.TryGetProperty("Territorial", out var territorial)) continue;
        foreach (var key in new[] { "MunicipioDivision2024Default", "DistritoDefault" })
            if (territorial.TryGetProperty(key, out var value)) configurationValues["Dte:Territorial:" + key] = value.GetString();
    }
    var config = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
    evidence["EsquemaNuevoFromPublishedJson"] = config.GetValue<bool>("Dte:EsquemaNuevo");
    evidence["SelectedNonSecretGeneratorConfigurationSha256"] = Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configurationValues.OrderBy(x => x.Key)))));
    var sources = new[] { "src/NeoSTP.Infrastructure/Dte/DteGeneratorService.cs", "src/NeoSTP.Infrastructure/Dte/DteTerritoryResolver.cs", "src/NeoSTP.Application/Dte/TerritorialOptions.cs",
        "tools/ClientCertificationPreview/Program.cs", "tools/ClientCertificationPreview/PreviewEngine.cs", "tools/ClientCertificationPreview/PreviewSelfTests.cs",
        "tools/ClientCertificationPreview/ClientCertificationPreview.csproj" };
    evidence["SourceSha256"] = sources.ToDictionary(x => x, x => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo, x)))));
    var builder = new SqlConnectionStringBuilder(connectionString) { ApplicationIntent = ApplicationIntent.ReadOnly, Pooling = false };
    if (builder.InitialCatalog != "NeoSTP_Cloud" || !new[] { ".", "(local)", "localhost", Environment.MachineName }.Contains(builder.DataSource, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("DATABASE_TARGET_REJECTED");
    await using var sql = new SqlConnection(builder.ConnectionString);
    connectionString = null;
    await sql.OpenAsync(); evidence["SqlOpened"] = true;
    await using (var command = sql.CreateCommand())
    {
        command.CommandText = "SELECT DB_NAME(),CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)),CAST(SERVERPROPERTY('ProductMajorVersion') AS int)";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetString(0) != "NeoSTP_Cloud" || !string.Equals(reader.GetString(1), Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            || reader.GetInt32(2) != 16) throw new InvalidOperationException("DATABASE_IDENTITY_REJECTED");
    }
    CompanyMetadata metadata;
    await using (var command = sql.CreateCommand())
    {
        command.CommandText = """
            SELECT CASE WHEN REPLACE(REPLACE(e.Nit,'-',''),' ','')=@nit THEN 1 ELSE 0 END IdentityMatches,
              c.AmbienteCodigo,e.CodigoActividad,e.Departamento,e.Municipio,e.Distrito,
              c.TipoEstablecimientoCodigo,c.CodigoEstablecimientoMh,c.CodigoPuntoVentaMh
            FROM dbo.Core_Empresas e JOIN dbo.Dte_Configuracion c ON c.EmpresaId=e.Id WHERE e.Id=23
            """;
        command.Parameters.AddWithValue("@nit", args[3]);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetInt32(0) != 1 || reader.GetString(1) != "PRUEBAS")
            throw new InvalidOperationException("COMPANY_OR_ENVIRONMENT_REJECTED");
        string? Text(int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        metadata = new(Text(2), Text(3), Text(4), Text(5), Text(6), Text(7), Text(8));
        if (await reader.ReadAsync()) throw new InvalidOperationException("COMPANY_AMBIGUOUS");
    }
    evidence["IdentityAndPruebasVerified"] = true;
    var all = new List<(int CatalogId, int? CompanyId, bool Active, CatalogItem? Item, string Catalog)>();
    await using (var command = sql.CreateCommand())
    {
        command.CommandText = """
            SELECT c.Id,c.EmpresaId,c.Activo,c.Codigo,i.Codigo,i.Valor,i.ParentCodigo,JSON_VALUE(i.MetadataJson,'$.codigoMH') MhCode
            FROM dbo.Core_Catalogos c LEFT JOIN dbo.Core_CatalogoItems i ON i.CatalogoId=c.Id AND i.Activo=1
            WHERE (c.EmpresaId IS NULL OR c.EmpresaId=23)
              AND c.Codigo IN ('DEPARTAMENTO_ES','MUNICIPIO_ES','DISTRITO_ES','TIPO_ESTABLECIMIENTO')
            ORDER BY c.Id,i.Orden,i.Codigo
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(3);
            CatalogItem? item = reader.IsDBNull(4) ? null : new(code, reader.GetString(4), reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7));
            all.Add((reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetBoolean(2), item, code));
        }
    }
    var catalog = new List<CatalogItem>();
    foreach (var group in all.GroupBy(x => x.Catalog))
    {
        var preferred = group.Any(x => x.CompanyId == 23) ? group.Where(x => x.CompanyId == 23).ToArray() : group.ToArray();
        if (preferred.Select(x => x.CatalogId).Distinct().Count() != 1 || preferred.Any(x => !x.Active))
            throw new InvalidOperationException("CATALOG_SELECTION_AMBIGUOUS_OR_INACTIVE");
        catalog.AddRange(preferred.Where(x => x.Item is not null).Select(x => x.Item!));
    }
    await sql.CloseAsync(); // Generation cannot access SQL or any application service.
    var schemaRoot = Path.Combine(repo, "tools/CertHarness/schemas/svfe-json-schemas");
    var results = new List<PreviewResult>();
    foreach (var type in PreviewEngine.Types) results.Add(await PreviewEngine.Run(type, metadata, catalog, config, schemaRoot));
    evidence["StoredMetadataPreview"] = results;
    var proposal = PreviewEngine.Proposed(metadata, catalog);
    evidence["ProposedTerritoryCatalogValidated"] = proposal is not null;
    evidence["ProposalAppliedToDatabase"] = false;
    if (proposal is null) evidence["ProposedTerritoryBlock"] = "CENTRO_OPICO_CODES_AND_PARENT_CHAIN_NOT_FULLY_VERIFIED";
    else
    {
        var proposedResults = new List<PreviewResult>();
        foreach (var type in PreviewEngine.Types) proposedResults.Add(await PreviewEngine.Run(type, proposal, catalog, config, schemaRoot));
        evidence["LocalProposedMetadataPreview"] = proposedResults;
    }
    evidence["GeneratedAllFourTypes"] = results.All(x => x.Generated);
    evidence["AllFourEffectiveSchemasAvailable"] = results.All(x => x.SchemaAvailable);
    evidence["NJsonSchemaVersion"] = typeof(NJsonSchema.JsonSchema).Assembly.GetName().Version?.ToString();
    evidence["Passed"] = results.All(x => x.Generated);
}
catch (Exception exception)
{
    evidence["FailureType"] = exception.GetType().Name;
    evidence["Passed"] = false;
}
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var serialized = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(output, serialized);
Console.WriteLine(serialized);
return evidence["Passed"] is true ? 0 : 1;
