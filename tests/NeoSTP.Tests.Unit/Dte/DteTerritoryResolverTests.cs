using FluentAssertions;
using NeoSTP.Application.Lookups;
using NeoSTP.Infrastructure.Dte;

namespace NeoSTP.Tests.Unit.Dte;

public class DteTerritoryResolverTests
{
    private static readonly LookupItem[] Departments = [new("LA_LIBERTAD", "La Libertad", null, "{\"codigoMH\":\"05\"}"), new("SAN_SALVADOR", "San Salvador", null, "{\"codigoMH\":\"06\"}")];
    private static readonly LookupItem[] Municipalities = [new("LA_LIBERTAD_CENTRO", "La Libertad Centro", "LA_LIBERTAD", "{\"codigoMH\":\"24\"}"), new("SAN_SALVADOR_SUR", "San Salvador Sur", "SAN_SALVADOR", "{\"codigoMH\":\"24\"}")];
    private static readonly LookupItem[] Districts = [new("SAN_JUAN_OPICO", "San Juan Opico", "LA_LIBERTAD_CENTRO", "{\"codigoMH\":\"15\"}")];

    [Theory]
    [InlineData("La Libertad", "La Libertad Centro", "San Juan Opico")]
    [InlineData("05", "24", "15")]
    [InlineData("LA-LIBERTAD", "LA_LIBERTAD_CENTRO", "SAN_JUAN_OPICO")]
    public void Resolves_codes_by_parent_instead_of_first_global_numeric_match(string department, string municipality, string district)
    {
        var result = DteTerritoryResolver.Resolve(department, municipality, district, Departments, Municipalities, Districts, true);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new DteTerritory("05", "24", "15"));
    }
    [Fact]
    public void Legacy_document_does_not_replace_current_municipality_with_district_code()
    {
        var result = DteTerritoryResolver.Resolve("05", "24", "15", Departments, Municipalities, Districts, false);
        result.Value!.Municipality.Should().Be("24");
    }
    [Theory]
    [InlineData("06", "LA_LIBERTAD_CENTRO", "15", "MUNICIPIO")]
    [InlineData("06", "24", "15", "DISTRITO")]
    [InlineData("05", "24", null, "DISTRITO")]
    [InlineData("MISSING", "24", "15", "DEPARTAMENTO")]
    public void Rejects_missing_or_cross_parent_territory(string department, string municipality, string? district, string field)
    {
        DteTerritoryResolver.Resolve(department, municipality, district, Departments, Municipalities, Districts, true)
            .ErrorCode.Should().Be("DTE_TERRITORIO_" + field);
    }
    [Fact]
    public void Duplicate_catalog_match_is_rejected()
    {
        var duplicates = Municipalities.Concat([new LookupItem("SECOND", "Second", "LA_LIBERTAD", "{\"codigoMH\":\"24\"}")]).ToArray();
        DteTerritoryResolver.Resolve("05", "24", "15", Departments, duplicates, Districts, true).IsFailure.Should().BeTrue();
    }
    [Fact]
    public void Legacy_numeric_municipality_without_district_is_not_reinterpreted_from_new_catalog()
    {
        var result = DteTerritoryResolver.Resolve("05", "02", null, Departments, Municipalities, Districts, false);
        result.Value.Should().Be(new DteTerritory("05", "02", null));
        DteTerritoryResolver.Resolve("05", "02", null, Departments, Municipalities, Districts, true).IsFailure.Should().BeTrue();
    }
    [Theory]
    [InlineData("invalid-json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("123")]
    public void Invalid_metadata_never_falls_back_to_an_unrelated_code(string metadata)
    {
        LookupItem[] districts = [new("SAN_JUAN_OPICO", "San Juan Opico", "LA_LIBERTAD_CENTRO", metadata)];
        DteTerritoryResolver.Resolve("05", "24", "San Juan Opico", Departments, Municipalities, districts, true).IsFailure.Should().BeTrue();
    }
}
