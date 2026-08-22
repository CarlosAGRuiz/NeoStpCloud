using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class DteGeneratorTests
{
    private readonly DteGeneratorService _gen = new(Options.Create(new TerritorialOptions()));
    private readonly DteCalculator _calc = new();

    private static DteDocumento NewDoc(string tipo)
    {
        var d = new DteDocumento
        {
            EmpresaId = 1,
            TipoDteCodigo = tipo,
            AmbienteCodigo = "PRUEBAS",
            NumeroControl = $"DTE-{tipo}-00010001-000000000000001",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            FechaEmision = new DateTime(2026, 1, 15),
            HoraEmision = new TimeSpan(10, 30, 0),
            CondicionOperacionCodigo = "1",
            Empresa = new Empresa
            {
                Id = 1,
                Nit = "06140101001234",
                Nrc = "12345",
                RazonSocial = "Empresa Demo S.A. de C.V.",
                Departamento = "06",
                Municipio = "14",
                CodigoActividad = "47190",
                ActividadEconomica = "Comercio",
            },
            ReceptorNombre = "Consumidor Final",
            ReceptorTipoDocumento = "36",
            ReceptorNumeroDocumento = "01234567-8",
            ReceptorNrc = tipo == TipoDteCodigos.ComprobanteCreditoFiscal ? "98765" : null,
        };
        d.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1,
            Codigo = "ITEM1",
            Descripcion = "Producto demo",
            UnidadMedidaCodigo = "59",
            Cantidad = 2m,
            PrecioUnitario = tipo == TipoDteCodigos.FacturaConsumidorFinal ? 11.30m : 10m,
        });
        return d;
    }

    // ── LK.2: territorial configurable (sin literales "23"/"03") ──

    [Fact]
    public void Generar_Donacion_UsaMunicipioYDistritoDeConfig()
    {
        var gen = new DteGeneratorService(Options.Create(new TerritorialOptions
        {
            MunicipioDivision2024Default = "07",
            DistritoDefault = "09",
        }));
        var d = NewDoc(TipoDteCodigos.ComprobanteDonacion);
        d.Empresa!.Distrito = null; // sin distrito registrado → usa el default de config
        _calc.Recalcular(d);

        var json = JsonDocument.Parse(gen.Generar(d).Value!);
        var dir = json.RootElement.GetProperty("emisor").GetProperty("direccion");
        dir.GetProperty("municipio").GetString().Should().Be("07");
        dir.GetProperty("distrito").GetString().Should().Be("09");
        dir.GetProperty("municipio").GetString().Should().NotBe("23"); // ya no hay literal
    }

    [Fact]
    public void Generar_Donacion_RespetaDistritoDelEmisor()
    {
        var gen = new DteGeneratorService(Options.Create(new TerritorialOptions()));
        var d = NewDoc(TipoDteCodigos.ComprobanteDonacion);
        d.Empresa!.Distrito = "05";
        _calc.Recalcular(d);

        var json = JsonDocument.Parse(gen.Generar(d).Value!);
        json.RootElement.GetProperty("emisor").GetProperty("direccion").GetProperty("distrito").GetString().Should().Be("05");
    }

    [Fact]
    public void Generar_Factura_ProduceJsonValido()
    {
        var d = NewDoc(TipoDteCodigos.FacturaConsumidorFinal);
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeTrue();
        var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("01");
        json.RootElement.GetProperty("identificacion").GetProperty("ambiente").GetString().Should().Be("00");
        json.RootElement.GetProperty("resumen").GetProperty("totalGravada").GetDouble().Should().Be(22.60);
        json.RootElement.GetProperty("resumen").GetProperty("totalPagar").GetDouble().Should().Be(22.60);
    }

    [Fact]
    public void Generar_CCF_IncluyeTributosYReceptorNrc()
    {
        var d = NewDoc(TipoDteCodigos.ComprobanteCreditoFiscal);
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeTrue();
        var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("03");
        json.RootElement.GetProperty("receptor").GetProperty("nrc").GetString().Should().Be("98765");
        var tributos = json.RootElement.GetProperty("resumen").GetProperty("tributos");
        tributos.GetArrayLength().Should().Be(1);
        tributos[0].GetProperty("codigo").GetString().Should().Be("20");
        tributos[0].GetProperty("valor").GetDouble().Should().Be(2.60);
    }

    [Fact]
    public void Generar_NotaCredito_IncluyeDocumentoRelacionado()
    {
        var d = NewDoc(TipoDteCodigos.NotaCredito);
        d.NumeroDocumentoRelacionado = "DTE-03-00010001-000000000000099";
        d.TipoDteRelacionado = "03";
        d.TipoGeneracionRelacionado = "2";
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeTrue();
        var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("05");
        var rel = json.RootElement.GetProperty("documentoRelacionado");
        rel.GetArrayLength().Should().Be(1);
        rel[0].GetProperty("numeroDocumento").GetString().Should().Be("DTE-03-00010001-000000000000099");
    }

    [Fact]
    public void Generar_SujetoExcluido_TieneNodoSujetoExcluido()
    {
        var d = NewDoc(TipoDteCodigos.FacturaSujetoExcluido);
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeTrue();
        var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("14");
        json.RootElement.TryGetProperty("sujetoExcluido", out var se).Should().BeTrue();
        se.GetProperty("nombre").GetString().Should().Be("Consumidor Final");
        json.RootElement.GetProperty("resumen").GetProperty("totalPagar").GetDouble().Should().Be(20.00);
    }

    [Fact]
    public void Generar_NotaDebito_TipoDte06_ConRelacionadoYReceptorNrc()
    {
        var d = NewDoc(TipoDteCodigos.NotaDebito);
        d.ReceptorNrc = "98765"; // ND es documento entre contribuyentes (usa NRC del receptor)
        d.NumeroDocumentoRelacionado = "DTE-03-00010001-000000000000099";
        d.TipoDteRelacionado = "03";
        d.TipoGeneracionRelacionado = "2";
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeTrue();
        var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("06");
        json.RootElement.GetProperty("receptor").GetProperty("nrc").GetString().Should().Be("98765");
        json.RootElement.GetProperty("documentoRelacionado").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Generar_NotaRemision_TipoDte04_ConReceptor()
    {
        var d = NewDoc(TipoDteCodigos.NotaRemision);
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeTrue();
        var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("04");
        json.RootElement.GetProperty("receptor").GetProperty("nombre").GetString().Should().Be("Consumidor Final");
    }

    [Fact]
    public void Generar_FacturaExportacion_TipoDte11_ConPaisYTributoExportacion()
    {
        var d = NewDoc(TipoDteCodigos.FacturaExportacion);
        d.ReceptorPaisCodigo = "9539"; // CAT-020: Estados Unidos
        d.ReceptorPaisNombre = "ESTADOS UNIDOS";
        d.ReceptorTipoPersona = 2;
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeTrue();
        var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("11");
        json.RootElement.GetProperty("receptor").GetProperty("codPais").GetString().Should().Be("9539");
        json.RootElement.GetProperty("receptor").GetProperty("nombrePais").GetString().Should().Be("ESTADOS UNIDOS");
        var cuerpo = json.RootElement.GetProperty("cuerpoDocumento");
        cuerpo[0].GetProperty("tributos")[0].GetString().Should().Be("C3"); // IVA exportación 0%
    }

    // ── 08 Comprobante de Liquidación / 09 Documento Contable de Liquidación ──
    // Los esquemas fe-cl-v2 y fe-dcl-v2 cierran cada bloque con "additionalProperties": false
    // y declaran todos sus campos como requeridos: mandar uno de más o de menos es rechazo.
    // Por eso los tests comparan el conjunto exacto de propiedades, no solo algunos valores.

    private static IEnumerable<string> Propiedades(JsonElement e)
        => e.EnumerateObject().Select(p => p.Name);

    private static DteDocumento NewLiquidacion(string tipo)
    {
        var d = NewDoc(tipo);
        d.ReceptorNombre = "Mandante S.A. de C.V.";
        d.ReceptorNumeroDocumento = "06140101001299";
        d.ReceptorNrc = "98765";
        d.ReceptorCodigoActividad = "47190";
        d.ReceptorActividadEconomica = "Venta al por menor";
        d.ReceptorDepartamentoCodigo = "06";
        d.ReceptorMunicipioCodigo = "14";
        d.ReceptorDireccion = "Calle Principal 123";
        d.Detalles.Clear();
        d.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1,
            Codigo = "76f19422-085d-45b7-a998-4374a3a8ead7", // minúsculas a propósito
            Descripcion = "Venta por cuenta del mandante",
            UnidadMedidaCodigo = "99",
            Cantidad = 2m,
            PrecioUnitario = 10m,
            DocRelacionadoTipoDte = "01",
            DocRelacionadoFecha = new DateTime(2026, 1, 10),
        });
        return d;
    }

    [Fact]
    public void Generar_VentaPorCuentaDeTercero_ViajaEnElBloqueVentaTercero()
    {
        // Insumo del 08: Hacienda cruza el DTE liquidado y exige que se haya emitido por
        // cuenta del mandante. Sin este bloque rechaza la liquidación sin decir por qué.
        var d = NewDoc(TipoDteCodigos.ComprobanteCreditoFiscal);
        d.VentaTerceroNit = "02101309221018";
        d.VentaTerceroNombre = "Mandante S.A. de C.V.";
        _calc.Recalcular(d);

        var vt = JsonDocument.Parse(_gen.Generar(d).Value!).RootElement.GetProperty("ventaTercero");
        vt.GetProperty("nit").GetString().Should().Be("02101309221018");
        vt.GetProperty("nombre").GetString().Should().Be("Mandante S.A. de C.V.");
    }

    [Fact]
    public void Generar_SinVentaTercero_OmiteElBloque()
    {
        var d = NewDoc(TipoDteCodigos.ComprobanteCreditoFiscal);
        _calc.Recalcular(d);

        JsonDocument.Parse(_gen.Generar(d).Value!).RootElement
            .GetProperty("ventaTercero").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Generar_ComprobanteLiquidacion_UsaLosCamposDelEsquemaFeClV2()
    {
        var d = NewLiquidacion(TipoDteCodigos.ComprobanteLiquidacion);
        _calc.Recalcular(d);

        var result = _gen.Generar(d);
        result.IsSuccess.Should().BeTrue();
        var root = JsonDocument.Parse(result.Value!).RootElement;

        Propiedades(root).Should().BeEquivalentTo(
            "identificacion", "emisor", "receptor", "cuerpoDocumento", "resumen", "apendice");

        var ident = root.GetProperty("identificacion");
        Propiedades(ident).Should().BeEquivalentTo(
            "version", "ambiente", "tipoDte", "numeroControl", "codigoGeneracion",
            "tipoModelo", "tipoOperacion", "fecEmi", "horEmi", "tipoMoneda", "fusion");
        ident.GetProperty("version").GetInt32().Should().Be(2);
        ident.GetProperty("tipoDte").GetString().Should().Be("08");

        Propiedades(root.GetProperty("emisor")).Should().BeEquivalentTo(
            "nit", "nrc", "nombre", "codActividad", "descActividad", "nombreComercial",
            "direccion", "telefono", "correo", "codEstable", "codPuntoVenta");

        Propiedades(root.GetProperty("receptor")).Should().BeEquivalentTo(
            "tipoDocumento", "numDocumento", "codDomiciliado", "nrc", "nombre", "codActividad",
            "descActividad", "nombreComercial", "direccion", "telefono", "correo");

        Propiedades(root.GetProperty("cuerpoDocumento")[0]).Should().BeEquivalentTo(
            "numItem", "tipoDte", "tipoGeneracion", "numeroDocumento", "fechaEmision",
            "ventaNoSuj", "ventaExenta", "ventaGravada", "exportaciones", "tributos",
            "ivaItem", "observaciones");

        Propiedades(root.GetProperty("resumen")).Should().BeEquivalentTo(
            "totalNoSuj", "totalExenta", "totalGravada", "exportacion", "subTotalVentas",
            "tributos", "montoTotalOperacion", "ivaPerci", "total", "totalLetras",
            "condicionOperacion", "observaciones");
    }

    [Fact]
    public void Generar_ComprobanteLiquidacion_ReferenciaElDocumentoLiquidadoConIvaSeparado()
    {
        var d = NewLiquidacion(TipoDteCodigos.ComprobanteLiquidacion);
        _calc.Recalcular(d);

        var root = JsonDocument.Parse(_gen.Generar(d).Value!).RootElement;
        var linea = root.GetProperty("cuerpoDocumento")[0];

        linea.GetProperty("tipoGeneracion").GetInt32().Should().Be(2); // UUID → electrónico
        linea.GetProperty("numeroDocumento").GetString().Should().Be("76F19422-085D-45B7-A998-4374A3A8EAD7");
        linea.GetProperty("fechaEmision").GetString().Should().Be("2026-01-10");
        linea.GetProperty("tipoDte").GetString().Should().Be("01");

        // El 08 lleva IVA separado como el CCF: precio sin IVA + tributo 20 desglosado.
        linea.GetProperty("ventaGravada").GetDouble().Should().Be(20.00);
        linea.GetProperty("ivaItem").GetDouble().Should().Be(2.60);
        linea.GetProperty("tributos")[0].GetString().Should().Be("20");

        var resumen = root.GetProperty("resumen");
        resumen.GetProperty("tributos")[0].GetProperty("valor").GetDouble().Should().Be(2.60);
        resumen.GetProperty("montoTotalOperacion").GetDouble().Should().Be(22.60);
        resumen.GetProperty("total").GetDouble().Should().Be(22.60);
    }

    [Fact]
    public void Generar_ComprobanteLiquidacion_IgnoraContingencia()
    {
        // fe-cl-v2 declara tipoModelo y tipoOperacion como const 1: el CL no admite
        // contingencia ni modelo diferido aunque el documento venga marcado así.
        var d = NewLiquidacion(TipoDteCodigos.ComprobanteLiquidacion);
        d.ModeloFacturacion = 2;
        d.TipoTransmision = 2;
        d.TipoContingenciaCodigo = "1";
        _calc.Recalcular(d);

        var ident = JsonDocument.Parse(_gen.Generar(d).Value!).RootElement.GetProperty("identificacion");
        ident.GetProperty("tipoModelo").GetInt32().Should().Be(1);
        ident.GetProperty("tipoOperacion").GetInt32().Should().Be(1);
        ident.TryGetProperty("tipoContingencia", out _).Should().BeFalse();
    }

    [Fact]
    public void Generar_DocumentoContableLiquidacion_UsaLosCamposDelEsquemaFeDclV2()
    {
        var d = NewLiquidacion(TipoDteCodigos.DocumentoContableLiquidacion);
        d.LiquidacionPeriodoInicio = new DateTime(2026, 1, 1);
        d.LiquidacionPeriodoFin = new DateTime(2026, 1, 31);
        _calc.Recalcular(d);

        var result = _gen.Generar(d);
        result.IsSuccess.Should().BeTrue();
        var root = JsonDocument.Parse(result.Value!).RootElement;

        Propiedades(root).Should().BeEquivalentTo(
            "identificacion", "emisor", "receptor", "cuerpoDocumento", "extension", "apendice");

        var ident = root.GetProperty("identificacion");
        Propiedades(ident).Should().BeEquivalentTo(
            "version", "ambiente", "tipoDte", "numeroControl", "codigoGeneracion",
            "tipoModelo", "tipoOperacion", "fecEmi", "horEmi", "tipoMoneda"); // sin fusion
        ident.GetProperty("version").GetInt32().Should().Be(2);
        ident.GetProperty("tipoDte").GetString().Should().Be("09");

        Propiedades(root.GetProperty("emisor")).Should().BeEquivalentTo(
            "nit", "nrc", "nombre", "codActividad", "descActividad", "nombreComercial",
            "telefono", "correo", "direccion", "codEstable", "codPuntoVenta");

        Propiedades(root.GetProperty("receptor")).Should().BeEquivalentTo(
            "nit", "nrc", "nombre", "codActividad", "descActividad", "nombreComercial",
            "tipoEstablecimiento", "direccion", "telefono", "correo");

        // El cuerpo del DCL es un objeto único (el corte del período), no un arreglo.
        var cuerpo = root.GetProperty("cuerpoDocumento");
        cuerpo.ValueKind.Should().Be(JsonValueKind.Object);
        Propiedades(cuerpo).Should().BeEquivalentTo(
            "periodoLiquidacionFechaInicio", "periodoLiquidacionFechaFin", "codLiquidacion",
            "cantidadDoc", "valorOperaciones", "montoSinPercepcion", "descripSinPercepcion",
            "subTotal", "iva", "montoSujetoPercepcion", "ivaPercibido", "comision",
            "porcentComision", "ivaComision", "liquidoApagar", "totalLetras", "observaciones");

        Propiedades(root.GetProperty("extension")).Should().BeEquivalentTo(
            "nombEntrega", "docuEntrega", "codEmpleado");
    }

    [Fact]
    public void Generar_DocumentoContableLiquidacion_DerivaElCorteDelPeriodo()
    {
        var d = NewLiquidacion(TipoDteCodigos.DocumentoContableLiquidacion);
        d.LiquidacionPeriodoInicio = new DateTime(2026, 1, 1);
        d.LiquidacionPeriodoFin = new DateTime(2026, 1, 31);
        _calc.Recalcular(d);

        var cuerpo = JsonDocument.Parse(_gen.Generar(d).Value!).RootElement.GetProperty("cuerpoDocumento");

        cuerpo.GetProperty("periodoLiquidacionFechaInicio").GetString().Should().Be("2026-01-01");
        cuerpo.GetProperty("periodoLiquidacionFechaFin").GetString().Should().Be("2026-01-31");
        cuerpo.GetProperty("valorOperaciones").GetDouble().Should().Be(20.00); // bruto con IVA
        cuerpo.GetProperty("subTotal").GetDouble().Should().Be(20.00);
        cuerpo.GetProperty("montoSujetoPercepcion").GetDouble().Should().Be(17.70); // 20 / 1.13
        cuerpo.GetProperty("iva").GetDouble().Should().Be(2.30);
        cuerpo.GetProperty("ivaPercibido").GetDouble().Should().Be(0.35);           // 2% de 17.70
        cuerpo.GetProperty("porcentComision").GetDouble().Should().Be(5.00);        // default
        cuerpo.GetProperty("comision").GetDouble().Should().Be(0.89);
        cuerpo.GetProperty("ivaComision").GetDouble().Should().Be(0.12);
        cuerpo.GetProperty("liquidoApagar").GetDouble().Should().Be(18.64);
        cuerpo.GetProperty("cantidadDoc").GetInt32().Should().Be(1);
    }

    [Fact]
    public void Generar_DocumentoContableLiquidacion_ExtensionCaeAlEmisorSinResponsable()
    {
        // extension.nombEntrega y docuEntrega son obligatorios y no admiten null en fe-dcl-v2.
        var d = NewLiquidacion(TipoDteCodigos.DocumentoContableLiquidacion);
        _calc.Recalcular(d);

        var ext = JsonDocument.Parse(_gen.Generar(d).Value!).RootElement.GetProperty("extension");
        ext.GetProperty("nombEntrega").GetString().Should().Be("Empresa Demo S.A. de C.V.");
        ext.GetProperty("docuEntrega").GetString().Should().Be("06140101001234");
        ext.GetProperty("codEmpleado").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Generar_SinDetalles_FallaConValidacion()
    {
        var d = NewDoc(TipoDteCodigos.FacturaConsumidorFinal);
        d.Detalles.Clear();
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
    }
}
