using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ClientCertificationPreview;

public static class PreviewSelfTests
{
    public static async Task<int> Run()
    {
        var checks = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        try
        {
            Check(!PreviewEngine.IdentityArgumentsValid(["--empresa", "2", "--expected-nit", "06232705261148"]), "Wrong tenant rejected before IO");
            Check(!PreviewEngine.IdentityArgumentsValid(["--empresa", "23", "--expected-nit", ""]), "Missing identity rejected before IO");
            var catalog = new List<CatalogItem> { new("DEPARTAMENTO_ES", "LA_LIBERTAD", "La Libertad", null, "05"),
                new("MUNICIPIO_ES", "LA_LIBERTAD_ESTE", "La Libertad Este", "LA_LIBERTAD", "26"),
                new("MUNICIPIO_ES", "LA_LIBERTAD_CENTRO", "La Libertad Centro", "LA_LIBERTAD", "24"),
                new("TIPO_ESTABLECIMIENTO", "CASA_MATRIZ", "Casa matriz", null, "02") };
            var metadata = new CompanyMetadata("62010", "La Libertad", "La Libertad Este", null, "CASA_MATRIZ", "M001", "P001");
            Check(PreviewEngine.Map(catalog, "DEPARTAMENTO_ES", "LA-LIBERTAD") == "05", "Catalog labels normalized without assuming labels invalid");
            Check(PreviewEngine.Proposed(metadata, catalog) is null, "Missing district blocks proposal");
            catalog.Add(new("DISTRITO_ES", "SAN_JUAN_OPICO", "San Juan Opico", "WRONG_PARENT", "01"));
            Check(PreviewEngine.Proposed(metadata, catalog) is null, "Wrong district parent blocks proposal");
            catalog.RemoveAt(catalog.Count - 1);
            catalog.Add(new("DISTRITO_ES", "SAN_JUAN_OPICO", "San Juan Opico", "LA_LIBERTAD_CENTRO", "15"));
            Check(PreviewEngine.Proposed(metadata, catalog)?.Municipality == "LA_LIBERTAD_CENTRO", "Complete synthetic catalog permits memory-only proposal");
            var refusedExternal = false;
            try { PreviewEngine.AssertLocalReferences("{\"$ref\":\"https://example.invalid/schema\"}"); } catch (InvalidOperationException) { refusedExternal = true; }
            Check(refusedExternal, "Remote schema reference rejected before parser");
            PreviewEngine.AssertLocalReferences("{\"$ref\":\"#/definitions/local\"}");
            Check(true, "Local schema reference permitted");
            var schemaRoot = Path.Combine(Environment.CurrentDirectory, "tools/CertHarness/schemas/svfe-json-schemas");
            var currentFex = await PreviewEngine.Run("11", metadata, catalog, new ConfigurationBuilder().Build(), schemaRoot);
            Check(!currentFex.Generated && currentFex.Issues.Any(x => x.Code == "DTE_TERRITORIO_DISTRITO"), "Current incomplete territory blocks FEX without a default");
            var resolvedMetadata = PreviewEngine.Proposed(metadata, catalog)!;
            foreach (var (enabled, expected) in new[] { (false, new[] { 1, 3, 3, 1 }), (true, new[] { 2, 4, 3, 2 }) })
            {
                var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Dte:EsquemaNuevo"] = enabled.ToString() }).Build();
                for (var i = 0; i < PreviewEngine.Types.Length; i++)
                {
                    var result = await PreviewEngine.Run(PreviewEngine.Types[i], resolvedMetadata, catalog, config, schemaRoot);
                    Check(result.Generated && result.Version == expected[i], $"Actual generator effective version {PreviewEngine.Types[i]} toggle {enabled}");
                    if (PreviewEngine.Types[i] == "11")
                        Check(result.OutputMunicipalityMatchesStored && !result.UsesDistrictFallback && result.OutputMunicipalityHasDepartmentParent,
                            $"FEX preserves resolved synthetic territory with toggle {enabled}");
                }
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Passed = true, CheckCount = checks.Count, SqlOpened = false, ExternalRequestsIssued = false, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { Passed = false, CompletedCheckCount = checks.Count, SqlOpened = false, ExternalRequestsIssued = false, Checks = checks }));
            return 1;
        }
    }
}
