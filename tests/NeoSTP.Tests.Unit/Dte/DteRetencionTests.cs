using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// DTE 07 — Comprobante de Retención (fe-cr-v1): reglas de numDocumento, cálculo de
/// retención (22→1%, C4→13%) y forma del JSON (cuerpoDocumento + resumen).
/// </summary>
public class DteRetencionTests
{
    private const string CodGen = "76F19422-085D-45B7-A998-4374A3A8EAD7";

    private readonly DteGeneratorService _gen = new(Options.Create(new TerritorialOptions()));
    private readonly DteCalculator _calc = new();

    // ── Reglas de formato del documento relacionado ──────────────────────────

    [Theory]
    [InlineData(CodGen, true)]
    [InlineData("76f19422-085d-45b7-a998-4374a3a8ead7", true)] // minúsculas: válido (se normaliza)
    [InlineData("DTE-07-M001P001-000000000000616", false)]     // número de control NO es código de generación
    [InlineData("1101", false)]
    [InlineData("", false)]
    public void EsCodigoGeneracion_DistingueUuidDeOtros(string numero, bool esperado)
        => DteRetencion.EsCodigoGeneracion(numero).Should().Be(esperado);

    [Theory]
    [InlineData("1101", true)]
    [InlineData("F001A23", true)]
    [InlineData("DTE-03-M001P001-000000000000616", false)] // guiones y >20 chars: inválido como físico
    [InlineData("", false)]
    public void EsNumeroFisicoValido_AlfanumericoMax20(string numero, bool esperado)
        => DteRetencion.EsNumeroFisicoValido(numero).Should().Be(esperado);

    // ── Cálculo ───────────────────────────────────────────────────────────────

    private static DteDocumento NewCr(params (string numero, decimal monto, string codigo)[] lineas)
    {
        var d = new DteDocumento
        {
            EmpresaId = 1,
            TipoDteCodigo = TipoDteCodigos.ComprobanteRetencion,
            AmbienteCodigo = "PRUEBAS",
            NumeroControl = "DTE-07-M001P001-000000000000001",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            FechaEmision = new DateTime(2026, 6, 1),
            HoraEmision = new TimeSpan(10, 0, 0),
            CondicionOperacionCodigo = "1",
            Empresa = new Empresa
            {
                Id = 1, Nit = "06140101001234", Nrc = "12345", RazonSocial = "Agente de Retención S.A.",
                Departamento = "06", Municipio = "14", CodigoActividad = "62010", ActividadEconomica = "Informática",
            },
            ReceptorNombre = "Proveedor Retenido S.A.",
            ReceptorTipoDocumento = "36",
            ReceptorNumeroDocumento = "06142803901121",
            ReceptorNrc = "98765",
        };
        var n = 1;
        foreach (var (numero, monto, codigo) in lineas)
            d.Detalles.Add(new DteDocumentoDetalle
            {
                NumeroLinea = n++,
                Codigo = numero,
                Descripcion = $"Retención sobre {numero}",
                Cantidad = 1m,
                PrecioUnitario = monto,
                DocRelacionadoTipoDte = "03",
                DocRelacionadoFecha = new DateTime(2026, 5, 20),
                RetencionCodigoMH = codigo,
            });
        return d;
    }

    [Fact]
    public void Recalcular_Retencion1Porciento()
    {
        var d = NewCr((CodGen, 1000m, DteRetencion.CodigoIva1));

        _calc.Recalcular(d);

        d.TotalGravada.Should().Be(1000m);     // total sujeto a retención
        d.IvaRetenido.Should().Be(10m);        // 1%
        d.TotalPagar.Should().Be(10m);         // el valor del CR es el IVA retenido
        d.TotalLetras.Should().Contain("DIEZ");
    }

    [Fact]
    public void Recalcular_Retencion13Porciento_YMixta()
    {
        var d = NewCr((CodGen, 100m, DteRetencion.CodigoIva13), ("1101", 200m, DteRetencion.CodigoIva1));

        _calc.Recalcular(d);

        d.TotalGravada.Should().Be(300m);
        d.IvaRetenido.Should().Be(15m);        // 13 + 2
        d.Detalles.First().IvaItem.Should().Be(13m);
        d.Detalles.Last().IvaItem.Should().Be(2m);
        d.MontoTotalOperacion.Should().Be(300m);
    }

    // ── JSON (fe-cr-v1) ───────────────────────────────────────────────────────

    [Fact]
    public void Generar_Retencion_EmiteCuerpoYResumenCorrectos()
    {
        var d = NewCr((CodGen.ToLowerInvariant(), 1000m, DteRetencion.CodigoIva1));
        _calc.Recalcular(d);

        var json = JsonDocument.Parse(_gen.Generar(d).Value!);
        var root = json.RootElement;

        root.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("07");
        root.GetProperty("identificacion").GetProperty("version").GetInt32().Should().Be(1);

        var linea = root.GetProperty("cuerpoDocumento")[0];
        linea.GetProperty("tipoDte").GetString().Should().Be("03");
        linea.GetProperty("tipoGeneracion").GetInt32().Should().Be(2);             // electrónico (UUID)
        linea.GetProperty("numDocumento").GetString().Should().Be(CodGen);         // normalizado a MAYÚSCULAS
        linea.GetProperty("fechaEmision").GetString().Should().Be("2026-05-20");
        linea.GetProperty("montoSujetoGrav").GetDouble().Should().Be(1000d);
        linea.GetProperty("codigoRetencionMH").GetString().Should().Be("22");
        linea.GetProperty("ivaRetenido").GetDouble().Should().Be(10d);

        var resumen = root.GetProperty("resumen");
        resumen.GetProperty("totalSujetoRetencion").GetDouble().Should().Be(1000d);
        resumen.GetProperty("totalIVAretenido").GetDouble().Should().Be(10d);
        resumen.GetProperty("totalIVAretenidoLetras").GetString().Should().Contain("DIEZ");

        var receptor = root.GetProperty("receptor");
        receptor.GetProperty("nrc").GetString().Should().Be("98765");
        receptor.GetProperty("numDocumento").GetString().Should().Be("06142803901121");
    }

    [Fact]
    public void Generar_DocumentoFisico_TipoGeneracion1()
    {
        var d = NewCr(("1101", 500m, DteRetencion.CodigoIva1));
        _calc.Recalcular(d);

        var json = JsonDocument.Parse(_gen.Generar(d).Value!);
        var linea = json.RootElement.GetProperty("cuerpoDocumento")[0];

        linea.GetProperty("tipoGeneracion").GetInt32().Should().Be(1); // físico
        linea.GetProperty("numDocumento").GetString().Should().Be("1101");
    }
}
