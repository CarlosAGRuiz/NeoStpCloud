using System.Text.Json;
using ClientCertificationPreview;
using NJsonSchema;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;

namespace ClientCertificationRunner;

public sealed class SchemaCheckedGenerator(IDteGeneratorService inner, IReadOnlyDictionary<(string, int), JsonSchema> schemas) : IDteGeneratorService
{
    public IReadOnlyList<Issue> LastIssues { get; private set; } = [];
    public Result<string> Generar(DteDocumento documento, DteConfiguracion? configuracion = null)
    {
        LastIssues = [];
        if (documento.EmpresaId != 23 || documento.AmbienteCodigo != "PRUEBAS" || configuracion?.AmbienteCodigo != "PRUEBAS")
            return Result<string>.Fail("Pilot context rejected.", "PILOT_CONTEXT_REJECTED");
        var generated = inner.Generar(documento, configuracion);
        if (generated.IsFailure) return generated;
        using var json = JsonDocument.Parse(generated.Value!);
        var identity = json.RootElement.GetProperty("identificacion");
        if (identity.GetProperty("ambiente").GetString() != "00" || json.RootElement.GetProperty("emisor").GetProperty("nit").GetString() != RunnerPolicy.Nit)
            return Result<string>.Fail("Pilot JSON identity rejected.", "PILOT_JSON_IDENTITY_REJECTED");
        var version = identity.GetProperty("version").GetInt32();
        if (version != PilotFixture.ExpectedVersion(documento.TipoDteCodigo))
        {
            LastIssues = [new Issue("identificacion.version", "CURRENT_PILOT_JSON_VERSION_REQUIRED")];
            return Result<string>.Fail("Generated JSON does not use the reviewed current version.", "PILOT_JSON_VERSION_UNSUPPORTED");
        }
        if (!schemas.TryGetValue((documento.TipoDteCodigo, version), out var schema))
            return Result<string>.Fail("Effective schema is unavailable.", "PILOT_SCHEMA_UNAVAILABLE");
        var errors = schema.Validate(generated.Value!);
        if (errors.Count != 0)
        {
            LastIssues = errors.Select(x => new Issue(x.Path ?? "$", "SCHEMA_" + x.Kind)).Distinct().Take(30).ToArray();
            return Result<string>.Fail("Synthetic fixture does not satisfy local schema.", "PILOT_SCHEMA_REJECTED");
        }
        return generated;
    }
    public static async Task<Dictionary<(string, int), JsonSchema>> Load(string root)
    {
        var schemas = new Dictionary<(string, int), JsonSchema>();
        foreach (var (type, version, name) in new[] { ("01", 1, "v1/fe-f-v1.json"), ("01", 2, "v2/fe-f-v2.json"),
            ("03", 3, "v3/fe-ccf-v3.json"), ("03", 4, "v4/fe-ccf-v4.json"), ("11", 3, "v3/fe-fex-v3.json"),
            ("14", 1, "v1/fe-fse-v1.json"), ("14", 2, "v2/fe-fse-v2.json") })
        {
            var path = Path.Combine(root, name); if (!File.Exists(path)) continue;
            var content = await File.ReadAllTextAsync(path); PreviewEngine.AssertLocalReferences(content);
            schemas[(type, version)] = await JsonSchema.FromJsonAsync(content);
        }
        return schemas;
    }
}
