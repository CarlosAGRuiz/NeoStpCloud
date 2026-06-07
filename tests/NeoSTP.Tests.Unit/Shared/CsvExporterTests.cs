using System.Text;
using FluentAssertions;
using NeoSTP.Shared;
using Xunit;

namespace NeoSTP.Tests.Unit.Shared;

/// <summary>M7.3 — utilidad CSV reutilizable: escape RFC 4180 + BOM.</summary>
public class CsvExporterTests
{
    [Fact]
    public void Headers_Y_Filas_SeRenderizan()
    {
        var csv = new CsvExporter("A", "B").AddRow("1", "2").AddRow("3", "4").ToCsv();
        csv.Should().Be("A,B\r\n1,2\r\n3,4\r\n");
    }

    [Theory]
    [InlineData("hola", "hola")]                       // sin caracteres especiales
    [InlineData("a,b", "\"a,b\"")]                     // coma => entre comillas
    [InlineData("di\"jo", "\"di\"\"jo\"")]             // comilla => duplicada
    [InlineData("línea1\nlínea2", "\"línea1\nlínea2\"")] // salto => entre comillas
    [InlineData("", "")]                                // vacío
    public void Field_EscapaSegunRfc4180(string input, string esperado)
        => CsvExporter.Field(input).Should().Be(esperado);

    [Fact]
    public void AddRow_ConTiposMixtos_UsaToString_YNullVacio()
    {
        var csv = new CsvExporter("n", "x", "y").AddRow(42, null, 7).ToCsv();
        csv.Should().Be("n,x,y\r\n42,,7\r\n"); // 42, celda vacía (null), 7
    }

    [Fact]
    public void ToBytes_IncluyeBomUtf8()
    {
        var bytes = new CsvExporter("A").ToBytes();
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF); // BOM UTF-8
    }
}
