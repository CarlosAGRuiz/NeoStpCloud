using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NJsonSchema;

namespace ClientCertificationPreview;

public sealed record CompanyMetadata(string? Activity, string? Department, string? Municipality, string? District,
    string? EstablishmentType, string? Establishment, string? PointOfSale);
public sealed record CatalogItem(string Catalog, string Code, string Label, string? Parent, string? MhCode);
public sealed record Issue(string Field, string Code);
public sealed record PreviewResult(string Type, int Version, bool Generated, bool SchemaAvailable, bool SchemaPassed,
    string? SchemaSha256, bool OutputMunicipalityMatchesStored, bool OutputMunicipalityHasDepartmentParent,
    bool UsesDistrictFallback, Issue[] Issues);

/// <summary>Only pure generation and local schema parsing. No persistence, signing, service container or transport.</summary>
public static class PreviewEngine
{
    public static readonly string[] Types = ["01", "03", "11", "14"];
    public static bool IdentityArgumentsValid(string[] args) => args.Length == 4 && args[0] == "--empresa"
        && args[1] == "23" && args[2] == "--expected-nit" && args[3] == "06232705261148";
    public static string Normalize(string? value) => string.Join(' ', new string((value ?? "").Trim().Replace('_', ' ').Replace('-', ' ')
        .Normalize(NormalizationForm.FormD).Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray())
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static CatalogItem? Find(IReadOnlyList<CatalogItem> items, string catalog, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var candidates = items.Where(x => x.Catalog == catalog && (Normalize(x.Code) == Normalize(value)
            || Normalize(x.Label) == Normalize(value) || x.MhCode == value)).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    public static string? Map(IReadOnlyList<CatalogItem> items, string catalog, string? value)
    {
        var found = Find(items, catalog, value);
        return found?.MhCode ?? (found is not null && found.Code.All(char.IsDigit) && found.Code.Length <= 4 ? found.Code : value);
    }
    public static CompanyMetadata? Proposed(CompanyMetadata source, IReadOnlyList<CatalogItem> items)
    {
        var department = Find(items, "DEPARTAMENTO_ES", "La Libertad");
        var municipality = Find(items, "MUNICIPIO_ES", "La Libertad Centro");
        var district = Find(items, "DISTRITO_ES", "San Juan Opico");
        if (department?.MhCode is null || municipality?.MhCode is null || district?.MhCode is null
            || municipality.Parent != department.Code || district.Parent != municipality.Code
            || new[] { department.MhCode, municipality.MhCode, district.MhCode }.Any(x => x.Length != 2 || !x.All(char.IsDigit))) return null;
        return source with { Department = department.Code, Municipality = municipality.Code, District = district.Code };
    }

    public static async Task<PreviewResult> Run(string type, CompanyMetadata metadata, IReadOnlyList<CatalogItem> catalog,
        IConfiguration configuration, string schemaRoot)
    {
        if (!Types.Contains(type)) throw new InvalidOperationException("PREVIEW_TYPE_NOT_ALLOWED");
        var territorial = new TerritorialOptions();
        configuration.GetSection(TerritorialOptions.SectionName).Bind(territorial);
        var generator = new DteGeneratorService(Options.Create(territorial), configuration);
        LookupItem[] Items(string name) => catalog.Where(x => x.Catalog == name)
            .Select(x => new LookupItem(x.Code, x.Label, x.Parent, JsonSerializer.Serialize(new { codigoMH = x.MhCode }))).ToArray();
        var territory = DteTerritoryResolver.Resolve(metadata.Department, metadata.Municipality, metadata.District,
            Items("DEPARTAMENTO_ES"), Items("MUNICIPIO_ES"), Items("DISTRITO_ES"),
            DteGeneratorService.RequiereTerritorio2024(type, configuration.GetValue<bool>("Dte:EsquemaNuevo")));
        if (territory.IsFailure) return new(type, 0, false, false, false, null, false, false, false,
            [new("emisor.direccion", territory.ErrorCode ?? "TERRITORY_NOT_RESOLVED")]);
        var doc = Fixture(type, metadata, catalog);
        doc.Empresa.Departamento = territory.Value!.Department;
        doc.Empresa.Municipio = territory.Value.Municipality;
        doc.Empresa.Distrito = territory.Value.District;
        var config = new DteConfiguracion { EmpresaId = 23, AmbienteCodigo = "PRUEBAS",
            TipoEstablecimientoCodigo = Map(catalog, "TIPO_ESTABLECIMIENTO", metadata.EstablishmentType),
            CodigoEstablecimientoMh = metadata.Establishment, CodigoPuntoVentaMh = metadata.PointOfSale };
        var issues = new List<Issue>();
        try
        {
            var result = generator.Generar(doc, config);
            if (result.IsFailure || result.Value is null) return new(type, 0, false, false, false, null, false, false, false,
                [new("$", result.ErrorCode ?? "GENERATOR_REJECTED")]);
            using var json = JsonDocument.Parse(result.Value);
            var root = json.RootElement;
            var version = root.GetProperty("identificacion").GetProperty("version").GetInt32();
            var address = root.GetProperty("emisor").GetProperty("direccion");
            var outputMunicipality = address.GetProperty("municipio").GetString();
            var matchesStored = outputMunicipality == doc.Empresa.Municipio;
            var department = Find(catalog, "DEPARTAMENTO_ES", doc.Empresa.Departamento);
            var outputMunicipalityItem = Find(catalog.Where(x => x.Catalog != "MUNICIPIO_ES" || x.Parent == department?.Code).ToArray(), "MUNICIPIO_ES", outputMunicipality);
            var parentMatches = department is not null && outputMunicipalityItem?.Parent == department.Code;
            var usesFallback = address.TryGetProperty("distrito", out _) && string.IsNullOrWhiteSpace(doc.Empresa.Distrito);
            if (!matchesStored) issues.Add(new("emisor.direccion.municipio", "GENERATOR_USES_CONFIGURED_DEFAULT_INSTEAD_OF_STORED_MUNICIPALITY"));
            if (!parentMatches) issues.Add(new("emisor.direccion.municipio", "OUTPUT_MUNICIPALITY_DEPARTMENT_PARENT_NOT_VERIFIED"));
            if (usesFallback) issues.Add(new("emisor.direccion.distrito", "UNVERIFIED_DISTRICT_DEFAULT_USED"));
            if (Proposed(metadata, catalog) is null) issues.Add(new("emisor.direccion.distrito", "USER_PROPOSED_TERRITORY_NOT_FULLY_CATALOG_VERIFIED"));
            var filename = (type, version) switch { ("01", 1) => "v1/fe-f-v1.json", ("01", 2) => "v2/fe-f-v2.json",
                ("03", 3) => "v3/fe-ccf-v3.json", ("03", 4) => "v4/fe-ccf-v4.json", ("11", 3) => "v3/fe-fex-v3.json",
                ("14", 1) => "v1/fe-fse-v1.json", ("14", 2) => "v2/fe-fse-v2.json", _ => "unavailable" };
            var schemaPath = Path.Combine(schemaRoot, filename);
            if (!File.Exists(schemaPath))
            {
                issues.Add(new("identificacion.version", "SCHEMA_VERSION_NOT_AVAILABLE_LOCALLY"));
                return new(type, version, true, false, false, null, matchesStored, parentMatches, usesFallback, issues.ToArray());
            }
            var schemaBytes = await File.ReadAllBytesAsync(schemaPath);
            if (schemaBytes.Length > 2 * 1024 * 1024) throw new InvalidOperationException();
            var schemaJson = Encoding.UTF8.GetString(schemaBytes);
            AssertLocalReferences(schemaJson);
            var schema = await JsonSchema.FromJsonAsync(schemaJson);
            var validation = schema.Validate(result.Value);
            issues.AddRange(validation.Select(x => new Issue(x.Path ?? "$", "SCHEMA_" + x.Kind)).Distinct().Take(100));
            return new(type, version, true, true, validation.Count == 0, Convert.ToHexString(SHA256.HashData(schemaBytes)),
                matchesStored, parentMatches, usesFallback, issues.ToArray());
        }
        catch (Exception)
        {
            return new(type, 0, false, false, false, null, false, false, false, [new("$", "PREVIEW_GENERATION_OR_LOCAL_SCHEMA_FAILURE")]);
        }
    }

    public static void AssertLocalReferences(string schema)
    {
        using var doc = JsonDocument.Parse(schema);
        void Visit(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
                foreach (var p in node.EnumerateObject())
                {
                    if (p.Name is "$ref" or "$dynamicRef" or "$recursiveRef")
                    {
                        var reference = p.Value.GetString();
                        if (reference is null || !(reference == "#" || reference.StartsWith("#/", StringComparison.Ordinal)))
                            throw new InvalidOperationException("EXTERNAL_SCHEMA_REFERENCE_FORBIDDEN");
                    }
                    Visit(p.Value);
                }
            else if (node.ValueKind == JsonValueKind.Array) foreach (var item in node.EnumerateArray()) Visit(item);
        }
        Visit(doc.RootElement);
    }

    private static DteDocumento Fixture(string type, CompanyMetadata metadata, IReadOnlyList<CatalogItem> catalog)
    {
        var net = type == "01" ? 113m : 100m;
        var tax = type is "01" or "03" ? 13m : 0m;
        var total = type == "03" ? 113m : net;
        var company = new Empresa { Id = 23, Nit = "06232705261148", Nrc = "1234567", RazonSocial = "EMISOR SINTETICO PREVIEW",
            NombreComercial = "PREVIEW LOCAL", CodigoActividad = metadata.Activity, ActividadEconomica = "ACTIVIDAD SINTETICA",
            Departamento = Map(catalog, "DEPARTAMENTO_ES", metadata.Department), Municipio = Map(catalog, "MUNICIPIO_ES", metadata.Municipality),
            Distrito = Map(catalog, "DISTRITO_ES", metadata.District), Direccion = "DIRECCION SINTETICA SIN DATOS DEL CLIENTE",
            Correo = "preview@example.invalid", Telefono = "22000000" };
        return new DteDocumento { EmpresaId = 23, Empresa = company, TipoDteCodigo = type, AmbienteCodigo = "PRUEBAS",
            NumeroControl = $"DTE-{type}-M001P001-000000000000001", CodigoGeneracion = $"00000000-0000-4000-8000-0000000000{type}",
            FechaEmision = new DateTime(2026, 9, 5), HoraEmision = new TimeSpan(12, 0, 0), CondicionOperacionCodigo = "1", FormaPagoCodigo = "01",
            ReceptorTipoDocumento = type == "11" ? "37" : type == "03" ? "36" : "13", ReceptorNumeroDocumento = type == "03" ? "06140000000000" : "00000000-0",
            ReceptorNrc = type == "03" ? "7654321" : null, ReceptorNombre = "RECEPTOR SINTETICO PREVIEW",
            ReceptorCodigoActividad = "62010", ReceptorActividadEconomica = "SERVICIOS SINTETICOS", ReceptorDepartamentoCodigo = "06",
            ReceptorMunicipioCodigo = "14", ReceptorDistritoCodigo = "01", ReceptorDireccion = "DIRECCION SINTETICA",
            ReceptorCorreo = "recipient@example.invalid", ReceptorTelefono = "22000001", ReceptorPaisCodigo = "US",
            ReceptorPaisNombre = "Estados Unidos", ReceptorTipoPersona = 2, TotalGravada = net, SubTotalVentas = net,
            SubTotal = net, IvaTotal = tax, MontoTotalOperacion = total, TotalPagar = total,
            TotalLetras = total == 113m ? "CIENTO TRECE DOLARES" : "CIEN DOLARES",
            Detalles = [new DteDocumentoDetalle { NumeroLinea = 1, TipoItem = 2, Codigo = "PREVIEW-LOCAL", Descripcion = "SERVICIO SINTETICO",
                UnidadMedidaCodigo = "59", Cantidad = 1, PrecioUnitario = net, VentaGravada = net, IvaItem = tax }] };
    }
}
