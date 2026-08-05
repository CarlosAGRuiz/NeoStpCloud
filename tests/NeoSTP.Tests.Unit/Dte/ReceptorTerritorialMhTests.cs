using FluentAssertions;
using NeoSTP.Application.Lookups;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// Guarda el arreglo del receptor territorial: el cliente guarda códigos internos del catálogo
/// ("SAN_SALVADOR") pero Hacienda exige el código MH numérico ("06"). Sin la traducción, MH
/// rechaza el DTE ("departamento no cumple el formato requerido") — verificado en apitest real.
/// </summary>
public class ReceptorTerritorialMhTests
{
    private static IReadOnlyList<LookupItem> Departamentos() =>
    [
        new LookupItem("SAN_SALVADOR", "San Salvador", null, "{\"codigoMH\":\"06\"}"),
        new LookupItem("LA_LIBERTAD", "La Libertad", null, "{\"codigoMH\":\"05\"}"),
        new LookupItem("SIN_META", "Sin metadata", null, null),
    ];

    private static IReadOnlyList<LookupItem> Municipios() =>
    [
        new LookupItem("SAN_SALVADOR_CENTRO", "San Salvador Centro", "SAN_SALVADOR", "{\"codigoMH\":\"23\",\"departamentoMH\":\"06\"}"),
    ];

    [Fact]
    public void TraduceDepartamentoInternoACodigoMh()
        => DteDocumentosService.ResolverCodigoMhEnItems(Departamentos(), "SAN_SALVADOR").Should().Be("06");

    [Fact]
    public void TraduceMunicipioInternoACodigoMh()
        => DteDocumentosService.ResolverCodigoMhEnItems(Municipios(), "SAN_SALVADOR_CENTRO").Should().Be("23");

    [Fact]
    public void EsInsensibleAMayusculas()
        => DteDocumentosService.ResolverCodigoMhEnItems(Departamentos(), "san_salvador").Should().Be("06");

    [Fact]
    public void CodigoQueNoEstaEnCatalogo_SeConservaTalCual()
    {
        // Un código MH ya numérico (o desconocido) no existe como Value interno: se deja pasar.
        DteDocumentosService.ResolverCodigoMhEnItems(Departamentos(), "06").Should().Be("06");
    }

    [Fact]
    public void ItemSinMetadata_SeConservaElCodigoOriginal()
        => DteDocumentosService.ResolverCodigoMhEnItems(Departamentos(), "SIN_META").Should().Be("SIN_META");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NuloOVacio_PasaSinCambios(string? entrada)
        => DteDocumentosService.ResolverCodigoMhEnItems(Departamentos(), entrada).Should().Be(entrada);

    [Fact]
    public void ExtraerCodigoMh_MetadataValido_DevuelveCodigo()
        => DteDocumentosService.ExtraerCodigoMh("{\"codigoMH\":\"06\"}").Should().Be("06");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"otro\":\"x\"}")]
    [InlineData("no-es-json")]
    public void ExtraerCodigoMh_SinCodigoOInvalido_DevuelveNull(string? metadata)
        => DteDocumentosService.ExtraerCodigoMh(metadata).Should().BeNull();
}
