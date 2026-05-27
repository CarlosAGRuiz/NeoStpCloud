using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Construye el JSON DTE según el esquema oficial de Hacienda El Salvador.
/// Soporta: 01 Factura, 03 CCF, 05 NC, 06 ND, 14 Sujeto Excluido.
/// </summary>
public class DteGeneratorService : IDteGeneratorService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public Result<string> Generar(DteDocumento d)
    {
        if (string.IsNullOrWhiteSpace(d.NumeroControl))
            return Result<string>.Fail("Documento sin número de control.", "VALIDATION");
        if (string.IsNullOrWhiteSpace(d.CodigoGeneracion))
            return Result<string>.Fail("Documento sin código de generación.", "VALIDATION");
        if (d.Detalles.Count == 0)
            return Result<string>.Fail("El documento no tiene detalles.", "VALIDATION");

        var emisor = d.Empresa;
        if (emisor is null)
            return Result<string>.Fail("Empresa emisora no cargada.", "VALIDATION");

        object dte = d.TipoDteCodigo switch
        {
            TipoDteCodigos.FacturaConsumidorFinal => BuildFactura(d, emisor),
            TipoDteCodigos.ComprobanteCreditoFiscal => BuildCcf(d, emisor),
            TipoDteCodigos.NotaCredito => BuildNotaCreditoDebito(d, emisor, isNotaCredito: true),
            TipoDteCodigos.NotaDebito => BuildNotaCreditoDebito(d, emisor, isNotaCredito: false),
            TipoDteCodigos.FacturaSujetoExcluido => BuildSujetoExcluido(d, emisor),
            _ => throw new InvalidOperationException($"TipoDte no soportado: {d.TipoDteCodigo}"),
        };

        return Result<string>.Ok(JsonSerializer.Serialize(dte, JsonOpts));
    }

    // ----------- 01 Factura Consumidor Final --------------------------

    private static object BuildFactura(DteDocumento d, Empresa emisor)
    {
        return new
        {
            identificacion = BuildIdentificacion(d, "1.0"),
            documentoRelacionado = (object?)null,
            emisor = BuildEmisor(d, emisor),
            receptor = BuildReceptorFactura(d),
            otrosDocumentos = (object?)null,
            ventaTercero = BuildVentaTercero(d),
            cuerpoDocumento = BuildCuerpo(d, conIvaPorLinea: false),
            resumen = BuildResumenFactura(d),
            extension = BuildExtensionFactura(d),
            apendice = (object?)null,
        };
    }

    private static object BuildResumenFactura(DteDocumento d) => new
    {
        totalNoSuj = (double)d.TotalNoSujeto,
        totalExenta = (double)d.TotalExenta,
        totalGravada = (double)d.TotalGravada,
        subTotalVentas = (double)d.SubTotalVentas,
        descuNoSuj = 0d,
        descuExenta = 0d,
        descuGravada = (double)d.DescuentoGravada,
        porcentajeDescuento = (double)d.PorcentajeDescuento,
        totalDescu = (double)d.TotalDescuento,
        tributos = (object?)null,
        subTotal = (double)d.SubTotal,
        ivaRete1 = (double)d.IvaRetenido,
        reteRenta = (double)d.ReteRenta,
        montoTotalOperacion = (double)d.MontoTotalOperacion,
        totalNoGravado = (double)d.TotalNoGravado,
        totalPagar = (double)d.TotalPagar,
        totalLetras = d.TotalLetras,
        totalIva = (double)d.IvaTotal,
        saldoFavor = 0d,
        condicionOperacion = ToInt(d.CondicionOperacionCodigo),
        pagos = (object?)null,
        numPagoElectronico = (string?)null,
    };

    private static object BuildExtensionFactura(DteDocumento d) => new
    {
        nombEntrega = (string?)null,
        docuEntrega = (string?)null,
        nombRecibe = d.ReceptorNombre,
        docuRecibe = d.ReceptorNumeroDocumento,
        observaciones = d.Observaciones,
        placaVehiculo = (string?)null,
    };

    // ----------- 03 CCF -----------------------------------------------

    private static object BuildCcf(DteDocumento d, Empresa emisor)
    {
        return new
        {
            identificacion = BuildIdentificacion(d, "3.0"),
            documentoRelacionado = (object?)null,
            emisor = BuildEmisorCcf(d, emisor),
            receptor = BuildReceptorCcf(d),
            otrosDocumentos = (object?)null,
            ventaTercero = BuildVentaTercero(d),
            cuerpoDocumento = BuildCuerpo(d, conIvaPorLinea: true),
            resumen = BuildResumenCcf(d),
            extension = (object?)null,
            apendice = (object?)null,
        };
    }

    private static object BuildResumenCcf(DteDocumento d) => new
    {
        totalNoSuj = (double)d.TotalNoSujeto,
        totalExenta = (double)d.TotalExenta,
        totalGravada = (double)d.TotalGravada,
        subTotalVentas = (double)d.SubTotalVentas,
        descuNoSuj = 0d,
        descuExenta = 0d,
        descuGravada = (double)d.DescuentoGravada,
        porcentajeDescuento = (double)d.PorcentajeDescuento,
        totalDescu = (double)d.TotalDescuento,
        tributos = new[]
        {
            new { codigo = "20", descripcion = "Impuesto al Valor Agregado 13%", valor = (double)d.IvaTotal },
        },
        subTotal = (double)d.SubTotal,
        ivaPerci1 = 0d,
        ivaRete1 = (double)d.IvaRetenido,
        reteRenta = (double)d.ReteRenta,
        montoTotalOperacion = (double)d.MontoTotalOperacion,
        totalNoGravado = (double)d.TotalNoGravado,
        totalPagar = (double)d.TotalPagar,
        totalLetras = d.TotalLetras,
        saldoFavor = 0d,
        condicionOperacion = ToInt(d.CondicionOperacionCodigo),
        pagos = (object?)null,
    };

    // ----------- 05 NC / 06 ND ---------------------------------------

    private static object BuildNotaCreditoDebito(DteDocumento d, Empresa emisor, bool isNotaCredito)
    {
        var version = isNotaCredito ? "3.0" : "3.0";
        return new
        {
            identificacion = BuildIdentificacion(d, version),
            documentoRelacionado = BuildDocumentoRelacionado(d),
            emisor = BuildEmisorCcf(d, emisor),
            receptor = BuildReceptorCcf(d),
            ventaTercero = BuildVentaTercero(d),
            cuerpoDocumento = BuildCuerpo(d, conIvaPorLinea: true),
            resumen = BuildResumenCcf(d),
            extension = (object?)null,
            apendice = (object?)null,
        };
    }

    private static object? BuildDocumentoRelacionado(DteDocumento d)
    {
        if (string.IsNullOrEmpty(d.NumeroDocumentoRelacionado)) return null;
        return new[]
        {
            new
            {
                tipoDocumento = d.TipoDteRelacionado ?? "03",
                tipoGeneracion = ToInt(d.TipoGeneracionRelacionado ?? "2"),
                numeroDocumento = d.NumeroDocumentoRelacionado,
                fechaEmision = d.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            }
        };
    }

    // ----------- 14 Sujeto Excluido ----------------------------------

    private static object BuildSujetoExcluido(DteDocumento d, Empresa emisor)
    {
        return new
        {
            identificacion = BuildIdentificacion(d, "1.0"),
            emisor = BuildEmisor(d, emisor),
            sujetoExcluido = new
            {
                tipoDocumento = d.ReceptorTipoDocumento ?? "13",
                numDocumento = d.ReceptorNumeroDocumento,
                nombre = d.ReceptorNombre,
                codActividad = d.ReceptorCodigoActividad,
                descActividad = d.ReceptorActividadEconomica,
                direccion = string.IsNullOrEmpty(d.ReceptorDepartamentoCodigo) ? null : new
                {
                    departamento = d.ReceptorDepartamentoCodigo,
                    municipio = d.ReceptorMunicipioCodigo,
                    complemento = d.ReceptorDireccion,
                },
                telefono = d.ReceptorTelefono,
                correo = d.ReceptorCorreo,
            },
            cuerpoDocumento = d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) => new
            {
                numItem = idx + 1,
                tipoItem = l.TipoItem,
                cantidad = (double)l.Cantidad,
                codigo = l.Codigo,
                uniMedida = ToInt(l.UnidadMedidaCodigo, defaultValue: 59),
                descripcion = l.Descripcion,
                precioUni = (double)l.PrecioUnitario,
                montoDescu = (double)l.MontoDescuento,
                compra = (double)(l.VentaGravada + l.VentaExenta + l.VentaNoSujeta),
            }).ToArray(),
            resumen = new
            {
                totalCompra = (double)d.SubTotalVentas,
                descu = (double)d.TotalDescuento,
                totalDescu = (double)d.TotalDescuento,
                subTotal = (double)d.SubTotal,
                ivaRete1 = 0d,
                reteRenta = (double)d.ReteRenta,
                totalPagar = (double)d.TotalPagar,
                totalLetras = d.TotalLetras,
                condicionOperacion = ToInt(d.CondicionOperacionCodigo),
                pagos = (object?)null,
                observaciones = d.Observaciones,
            },
            apendice = (object?)null,
        };
    }

    // ----------- Bloques comunes -------------------------------------

    private static object BuildIdentificacion(DteDocumento d, string version) => new
    {
        version = version,
        ambiente = d.AmbienteCodigo == "PRODUCCION" ? "01" : "00",
        tipoDte = d.TipoDteCodigo,
        numeroControl = d.NumeroControl,
        codigoGeneracion = d.CodigoGeneracion,
        tipoModelo = d.ModeloFacturacion,
        tipoOperacion = d.TipoTransmision,
        tipoContingencia = d.TipoContingenciaCodigo,
        motivoContin = d.MotivoContingencia,
        fecEmi = d.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        horEmi = d.HoraEmision.ToString(@"hh\:mm\:ss"),
        tipoMoneda = d.TipoMonedaCodigo ?? "USD",
    };

    private static object BuildEmisor(DteDocumento d, Empresa e) => new
    {
        nit = e.Nit,
        nrc = e.Nrc,
        nombre = e.RazonSocial,
        codActividad = e.CodigoActividad,
        descActividad = e.ActividadEconomica,
        nombreComercial = e.NombreComercial,
        tipoEstablecimiento = "01",
        direccion = new
        {
            departamento = e.Departamento,
            municipio = e.Municipio,
            complemento = e.Direccion,
        },
        telefono = e.Telefono,
        correo = e.Correo,
        codEstableMH = (string?)null,
        codEstable = (string?)null,
        codPuntoVentaMH = (string?)null,
        codPuntoVenta = (string?)null,
    };

    private static object BuildEmisorCcf(DteDocumento d, Empresa e) => new
    {
        nit = e.Nit,
        nrc = e.Nrc,
        nombre = e.RazonSocial,
        codActividad = e.CodigoActividad,
        descActividad = e.ActividadEconomica,
        nombreComercial = e.NombreComercial,
        tipoEstablecimiento = "01",
        direccion = new
        {
            departamento = e.Departamento,
            municipio = e.Municipio,
            complemento = e.Direccion,
        },
        telefono = e.Telefono,
        correo = e.Correo,
    };

    private static object? BuildReceptorFactura(DteDocumento d)
    {
        if (string.IsNullOrEmpty(d.ReceptorNombre)
            && string.IsNullOrEmpty(d.ReceptorNumeroDocumento))
            return null;
        return new
        {
            tipoDocumento = d.ReceptorTipoDocumento,
            numDocumento = d.ReceptorNumeroDocumento,
            nrc = d.ReceptorNrc,
            nombre = d.ReceptorNombre,
            codActividad = d.ReceptorCodigoActividad,
            descActividad = d.ReceptorActividadEconomica,
            direccion = string.IsNullOrEmpty(d.ReceptorDepartamentoCodigo) ? null : new
            {
                departamento = d.ReceptorDepartamentoCodigo,
                municipio = d.ReceptorMunicipioCodigo,
                complemento = d.ReceptorDireccion,
            },
            telefono = d.ReceptorTelefono,
            correo = d.ReceptorCorreo,
        };
    }

    private static object BuildReceptorCcf(DteDocumento d) => new
    {
        nit = d.ReceptorNumeroDocumento,
        nrc = d.ReceptorNrc,
        nombre = d.ReceptorNombre,
        codActividad = d.ReceptorCodigoActividad,
        descActividad = d.ReceptorActividadEconomica,
        nombreComercial = (string?)null,
        direccion = string.IsNullOrEmpty(d.ReceptorDepartamentoCodigo) ? null : new
        {
            departamento = d.ReceptorDepartamentoCodigo,
            municipio = d.ReceptorMunicipioCodigo,
            complemento = d.ReceptorDireccion,
        },
        telefono = d.ReceptorTelefono,
        correo = d.ReceptorCorreo,
    };

    private static object? BuildVentaTercero(DteDocumento d)
    {
        if (string.IsNullOrEmpty(d.VentaTerceroNit)) return null;
        return new { nit = d.VentaTerceroNit, nombre = d.VentaTerceroNombre };
    }

    private static object[] BuildCuerpo(DteDocumento d, bool conIvaPorLinea)
    {
        return d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) => (object)new
        {
            numItem = idx + 1,
            tipoItem = l.TipoItem,
            numeroDocumento = (string?)null,
            cantidad = (double)l.Cantidad,
            codigo = l.Codigo,
            codTributo = (string?)null,
            uniMedida = ToInt(l.UnidadMedidaCodigo, defaultValue: 59),
            descripcion = l.Descripcion,
            precioUni = (double)l.PrecioUnitario,
            montoDescu = (double)l.MontoDescuento,
            ventaNoSuj = (double)l.VentaNoSujeta,
            ventaExenta = (double)l.VentaExenta,
            ventaGravada = (double)l.VentaGravada,
            tributos = conIvaPorLinea && l.VentaGravada > 0
                ? new[] { "20" }
                : null,
            psv = 0d,
            noGravado = (double)(l.NoGravado ? l.VentaNoSujeta : 0m),
            ivaItem = conIvaPorLinea ? (double?)null : (double)l.IvaItem,
        }).ToArray();
    }

    private static int ToInt(string? s, int defaultValue = 0)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : defaultValue;
}
