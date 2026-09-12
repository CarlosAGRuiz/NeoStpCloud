using FluentAssertions;
using NeoSTP.Application.Dte;

namespace NeoSTP.Tests.Unit.Dte;

public class DteTipoEstablecimientoTests
{
    [Theory]
    [InlineData("01", "01")]
    [InlineData("02", "02")]
    [InlineData("04", "04")]
    [InlineData("07", "07")]
    [InlineData("20", "20")]
    [InlineData("SUCURSAL", "01")]
    [InlineData("AGENCIA", "01")]
    [InlineData("CASA_MATRIZ", "02")]
    [InlineData("BODEGA", "04")]
    [InlineData("PATIO", "07")]
    [InlineData("OTRO", "20")]
    public void NormalizesOfficialAndLegacyCodes(string input, string expected)
    {
        DteTiposEstablecimiento.TryNormalize(input, out var result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("OFICINA")]
    [InlineData("99")]
    [InlineData("CASA")]
    public void RejectsValuesOutsideCat009(string input)
    {
        DteTiposEstablecimiento.TryNormalize(input, out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void AllowsEmptyValueForIncompleteDraftConfiguration()
    {
        DteTiposEstablecimiento.TryNormalize(" ", out var result).Should().BeTrue();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "02")]
    [InlineData("CASA_MATRIZ", "02")]
    [InlineData("SUCURSAL", "01")]
    public void ProducesOnlyOfficialCodesForEmission(string? input, string expected)
        => DteTiposEstablecimiento.ForEmission(input).Should().Be(expected);

    [Fact]
    public void RefusesToEmitUnknownEstablishmentType()
    {
        var act = () => DteTiposEstablecimiento.ForEmission("OFICINA");

        act.Should().Throw<InvalidOperationException>().WithMessage("*CAT-009*");
    }
}
