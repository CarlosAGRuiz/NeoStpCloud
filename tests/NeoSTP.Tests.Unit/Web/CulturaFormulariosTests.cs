using System.Globalization;
using FluentAssertions;
using Xunit;

namespace NeoSTP.Tests.Unit.Web;

/// <summary>
/// Regresión de un bug que corrompía importes en silencio: la web corría con la cultura
/// "es" genérica (coma decimal, euro), pero los &lt;input type="number"&gt; del navegador
/// SIEMPRE envían el punto decimal según el estándar HTML. El binder leía "3.25" como 325,
/// multiplicando por 100 cada precio capturado en un formulario.
///
/// El Salvador usa punto decimal y dólar: es-SV es la cultura correcta y además arregla
/// el binding. Estos tests fijan esa expectativa.
/// </summary>
public class CulturaFormulariosTests
{
    private static readonly CultureInfo Salvador = CultureInfo.GetCultureInfo("es-SV");

    [Theory]
    [InlineData("3.25", 3.25)]
    [InlineData("7.85", 7.85)]
    [InlineData("0.13", 0.13)]
    [InlineData("1234.56", 1234.56)]
    public void PrecioDelNavegador_SeLeeIgualQueSeEnvia(string enviado, decimal esperado)
    {
        decimal.Parse(enviado, NumberStyles.Number, Salvador).Should().Be(esperado);
    }

    [Fact]
    public void CulturaGenericaEs_HabriaCorrompidoElPrecio()
    {
        // Documenta el bug: con "es", 3.25 se interpretaba como tres mil doscientos cincuenta
        // (el punto pasa por separador de miles). De ahí venían las facturas 100x infladas.
        var generica = CultureInfo.GetCultureInfo("es");

        var corrupto = decimal.Parse("3.25", NumberStyles.Number, generica);

        corrupto.Should().Be(325m);
        corrupto.Should().NotBe(3.25m);
    }

    [Fact]
    public void Salvador_UsaPuntoDecimalYDolar()
    {
        Salvador.NumberFormat.NumberDecimalSeparator.Should().Be(".");
        Salvador.NumberFormat.NumberGroupSeparator.Should().Be(",");
        Salvador.NumberFormat.CurrencySymbol.Should().Be("$");
    }

    [Fact]
    public void Salvador_FormateaMontosComoEsperaElUsuarioSalvadoreno()
    {
        4185.11m.ToString("N2", Salvador).Should().Be("4,185.11");
    }

    [Fact]
    public void FechaIsoDelNavegador_SeLeeCorrecta()
    {
        // <input type="date"> envía siempre ISO yyyy-MM-dd, sin importar la cultura.
        DateTime.TryParse("2026-07-26", Salvador, DateTimeStyles.None, out var fecha)
            .Should().BeTrue();
        fecha.Should().Be(new DateTime(2026, 7, 26));
    }

    [Fact]
    public void Salvador_HeredaLosRecursosDeEspanol()
    {
        // Los .resx están nombrados en "es"; es-SV debe caer a ellos por herencia
        // para no perder las traducciones al cambiar de cultura.
        Salvador.Parent.TwoLetterISOLanguageName.Should().Be("es");
    }
}
