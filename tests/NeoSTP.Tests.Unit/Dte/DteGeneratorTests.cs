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
        _calc.Recalcular(d);
        var result = _gen.Generar(d);

        result.IsSuccess.Should().BeTrue();
        var json = JsonDocument.Parse(result.Value!);
        json.RootElement.GetProperty("identificacion").GetProperty("tipoDte").GetString().Should().Be("11");
        json.RootElement.GetProperty("receptor").GetProperty("codPais").GetString().Should().Be("9539");
        var cuerpo = json.RootElement.GetProperty("cuerpoDocumento");
        cuerpo[0].GetProperty("tributos")[0].GetString().Should().Be("C3"); // IVA exportación 0%
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
