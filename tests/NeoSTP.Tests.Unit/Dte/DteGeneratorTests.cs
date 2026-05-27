using System.Text.Json;
using FluentAssertions;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class DteGeneratorTests
{
    private readonly DteGeneratorService _gen = new();
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
