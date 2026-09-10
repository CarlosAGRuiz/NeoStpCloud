using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;

namespace NeoSTP.Tests.Unit.Dte;

public sealed class DteGeneratorTerritorialTests
{
    [Theory]
    [InlineData("01", false, 1)]
    [InlineData("03", false, 3)]
    [InlineData("14", false, 1)]
    [InlineData("11", false, 3)]
    [InlineData("01", true, 2)]
    [InlineData("03", true, 4)]
    [InlineData("14", true, 2)]
    [InlineData("11", true, 3)]
    public void Keeps_effective_version_and_resolved_territory_without_defaults_or_mutation(string type, bool enabled, int version)
    {
        var document = Document(type, enabled);
        var before = JsonSerializer.Serialize(new { document.Empresa.Departamento, document.Empresa.Municipio, document.Empresa.Distrito,
            document.ReceptorDepartamentoCodigo, document.ReceptorMunicipioCodigo, document.ReceptorDistritoCodigo });

        var result = Generator(enabled).Generar(document);

        result.IsSuccess.Should().BeTrue();
        using var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("version").GetInt32().Should().Be(version);
        var address = json.RootElement.GetProperty("emisor").GetProperty("direccion");
        address.GetProperty("departamento").GetString().Should().Be("05");
        address.GetProperty("municipio").GetString().Should().Be(document.Empresa.Municipio);
        var modern = enabled || type == "11";
        address.TryGetProperty("distrito", out var district).Should().Be(modern);
        if (modern) district.GetString().Should().Be("15");
        if (type != "11")
        {
            var receiver = json.RootElement.GetProperty(type == "14" && !enabled ? "sujetoExcluido" : "receptor").GetProperty("direccion");
            receiver.GetProperty("municipio").GetString().Should().Be(document.ReceptorMunicipioCodigo);
            receiver.TryGetProperty("distrito", out var receiverDistrict).Should().Be(enabled);
            if (enabled) receiverDistrict.GetString().Should().Be("14");
        }
        JsonSerializer.Serialize(new { document.Empresa.Departamento, document.Empresa.Municipio, document.Empresa.Distrito,
            document.ReceptorDepartamentoCodigo, document.ReceptorMunicipioCodigo, document.ReceptorDistritoCodigo }).Should().Be(before);
    }

    [Theory]
    [InlineData("01", false)]
    [InlineData("03", false)]
    [InlineData("14", false)]
    [InlineData("11", false)]
    [InlineData("01", true)]
    [InlineData("03", true)]
    [InlineData("14", true)]
    [InlineData("11", true)]
    public void Rejects_missing_or_unresolved_emitter_codes_without_substituting_configuration(string type, bool enabled)
    {
        foreach (var invalid in new string?[] { null, "", " ", "La Libertad", "5", "005", "０５" })
        {
            var document = Document(type, enabled);
            document.Empresa.Departamento = invalid;
            var result = Generator(enabled).Generar(document);
            result.ErrorCode.Should().Be("DTE_TERRITORIO_EMISOR_INVALIDO");
            result.Value.Should().BeNull();
            document = Document(type, enabled);
            document.Empresa.Municipio = invalid;
            Generator(enabled).Generar(document).ErrorCode.Should().Be("DTE_TERRITORIO_EMISOR_INVALIDO");
        }
    }

    [Theory]
    [InlineData("11", false)]
    [InlineData("11", true)]
    [InlineData("01", true)]
    [InlineData("03", true)]
    [InlineData("14", true)]
    public void Modern_emitter_requires_its_own_district(string type, bool enabled)
    {
        var document = Document(type, enabled);
        document.Empresa.Distrito = null;
        Generator(enabled).Generar(document).ErrorCode.Should().Be("DTE_TERRITORIO_EMISOR_INVALIDO");
    }

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("14")]
    public void Legacy_output_does_not_require_or_copy_district(string type)
    {
        var document = Document(type, false);
        document.Empresa.Distrito = null;
        document.ReceptorDistritoCodigo = null;
        Generator(false).Generar(document).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("01", false)]
    [InlineData("03", false)]
    [InlineData("14", false)]
    [InlineData("01", true)]
    [InlineData("03", true)]
    [InlineData("14", true)]
    public void Present_receiver_address_cannot_be_partial_or_unresolved(string type, bool enabled)
    {
        var document = Document(type, enabled);
        document.ReceptorMunicipioCodigo = "SAN_SALVADOR";
        Generator(enabled).Generar(document).ErrorCode.Should().Be("DTE_TERRITORIO_RECEPTOR_INVALIDO");
        document.ReceptorMunicipioCodigo = null;
        Generator(enabled).Generar(document).ErrorCode.Should().Be("DTE_TERRITORIO_RECEPTOR_INVALIDO");
    }

    [Theory]
    [InlineData("01", true, true)]
    [InlineData("03", true, false)]
    [InlineData("14", true, false)]
    [InlineData("01", false, true)]
    [InlineData("03", false, true)]
    [InlineData("14", false, true)]
    public void Keeps_optional_receiver_behavior_but_requires_new_contributor_territory(string type, bool enabled, bool accepted)
    {
        var document = Document(type, enabled);
        document.ReceptorDepartamentoCodigo = null;
        document.ReceptorMunicipioCodigo = null;
        document.ReceptorDistritoCodigo = null;
        Generator(enabled).Generar(document).IsSuccess.Should().Be(accepted);
    }

    [Theory]
    [InlineData("04")]
    [InlineData("07")]
    [InlineData("15")]
    public void Other_document_types_keep_their_existing_territorial_configuration(string type)
    {
        var document = Document(type, true);
        document.Empresa.Distrito = null;
        var result = Generator(true).Generar(document);
        result.IsSuccess.Should().BeTrue();
        using var json = JsonDocument.Parse(result.Value!);
        var address = json.RootElement.GetProperty("emisor").GetProperty("direccion");
        address.GetProperty("municipio").GetString().Should().Be("99");
        address.GetProperty("distrito").GetString().Should().Be("98");
    }

    private static DteGeneratorService Generator(bool enabled) => new(Options.Create(new TerritorialOptions
    {
        MunicipioDivision2024Default = "99", DistritoDefault = "98",
    }), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Dte:EsquemaNuevo"] = enabled.ToString() }).Build());

    private static DteDocumento Document(string type, bool enabled) => new()
    {
        EmpresaId = 71, TipoDteCodigo = type, AmbienteCodigo = "PRUEBAS", NumeroControl = "DTE-SYNTHETIC-001",
        CodigoGeneracion = Guid.NewGuid().ToString("D"), ReceptorNombre = "Synthetic receiver", ReceptorNumeroDocumento = "00000000000000",
        ReceptorDepartamentoCodigo = "06", ReceptorMunicipioCodigo = "23", ReceptorDistritoCodigo = "14",
        ReceptorDireccion = "Synthetic address", ReceptorPaisCodigo = "US", ReceptorPaisNombre = "United States", ReceptorTipoPersona = 2,
        Empresa = new Empresa { Id = 71, Nit = "00000000000071", RazonSocial = "Synthetic emitter", Departamento = "05",
            Municipio = "24", Distrito = "15", Direccion = "Synthetic address" },
        Detalles = [new DteDocumentoDetalle { NumeroLinea = 1, Codigo = "SYNTHETIC", Descripcion = "Synthetic service", Cantidad = 1, PrecioUnitario = 100 }],
    };
}
