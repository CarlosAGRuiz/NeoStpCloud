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

    public Result<string> Generar(DteDocumento d, DteConfiguracion? config = null)
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
            TipoDteCodigos.FacturaConsumidorFinal => BuildFactura(d, emisor, config),
            TipoDteCodigos.ComprobanteCreditoFiscal => BuildCcf(d, emisor, config),
            TipoDteCodigos.NotaCredito => BuildNotaCreditoDebito(d, emisor, config, isNotaCredito: true),
            TipoDteCodigos.NotaDebito => BuildNotaCreditoDebito(d, emisor, config, isNotaCredito: false),
            TipoDteCodigos.FacturaSujetoExcluido => BuildSujetoExcluido(d, emisor, config),
            _ => throw new InvalidOperationException($"TipoDte no soportado: {d.TipoDteCodigo}"),
        };

        return Result<string>.Ok(JsonSerializer.Serialize(dte, JsonOpts));
    }

    // ----------- 01 Factura Consumidor Final --------------------------

    private static object BuildFactura(DteDocumento d, Empresa emisor, DteConfiguracion? config)
    {
        return new
        {
            identificacion = BuildIdentificacion(d, 1),
            documentoRelacionado = (object?)null,
            emisor = BuildEmisor(d, emisor, config),
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

    private static object BuildCcf(DteDocumento d, Empresa emisor, DteConfiguracion? config)
    {
        return new
        {
            identificacion = BuildIdentificacion(d, 3),
            documentoRelacionado = (object?)null,
            emisor = BuildEmisorCcf(d, emisor, config),
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
        numPagoElectronico = (string?)null,
    };

    // ----------- 05 NC / 06 ND ---------------------------------------

    private static object BuildNotaCreditoDebito(DteDocumento d, Empresa emisor, DteConfiguracion? config, bool isNotaCredito)
    {
        return new
        {
            identificacion = BuildIdentificacion(d, 3),
            documentoRelacionado = BuildDocumentoRelacionado(d),
            emisor = BuildEmisorCcf(d, emisor, config),
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

    private static object BuildSujetoExcluido(DteDocumento d, Empresa emisor, DteConfiguracion? config)
    {
        return new
        {
            identificacion = BuildIdentificacion(d, 1),
            emisor = BuildEmisorFse(emisor, config),
            sujetoExcluido = new
            {
                tipoDocumento = MapTipoDocReceptorMh(d.ReceptorTipoDocumento) ?? "13",
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

    private static object BuildIdentificacion(DteDocumento d, int version) => new
    {
        version = version,   // Entero según spec MH (1 para 01/14, 3 para 03/05/06)
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

    /// <summary>Emisor para Factura de Sujeto Excluido (14): sin nombreComercial ni tipoEstablecimiento.</summary>
    private static object BuildEmisorFse(Empresa e, DteConfiguracion? config)
    {
        var codEst = string.IsNullOrWhiteSpace(config?.CodigoEstablecimientoMh) ? null : config!.CodigoEstablecimientoMh;
        var codPv  = string.IsNullOrWhiteSpace(config?.CodigoPuntoVentaMh)      ? null : config!.CodigoPuntoVentaMh;
        return new
        {
            nit = e.Nit,
            nrc = e.Nrc,
            nombre = e.RazonSocial,
            codActividad = e.CodigoActividad,
            descActividad = e.ActividadEconomica,
            direccion = new
            {
                departamento = e.Departamento,
                municipio = e.Municipio,
                complemento = e.Direccion,
            },
            telefono = e.Telefono,
            codEstableMH = codEst,
            codEstable = codEst,
            codPuntoVentaMH = codPv,
            codPuntoVenta = codPv,
            correo = e.Correo,
        };
    }

    private static object BuildEmisor(DteDocumento d, Empresa e, DteConfiguracion? config)
    {
        // tipoEstablecimiento por defecto 02 (Casa Matriz) si la config no lo trae.
        var tipoEst = string.IsNullOrWhiteSpace(config?.TipoEstablecimientoCodigo) ? "02" : config!.TipoEstablecimientoCodigo;
        var codEst  = string.IsNullOrWhiteSpace(config?.CodigoEstablecimientoMh)    ? null  : config!.CodigoEstablecimientoMh;
        var codPv   = string.IsNullOrWhiteSpace(config?.CodigoPuntoVentaMh)         ? null  : config!.CodigoPuntoVentaMh;

        return new
        {
            nit = e.Nit,
            nrc = e.Nrc,
            nombre = e.RazonSocial,
            codActividad = e.CodigoActividad,
            descActividad = e.ActividadEconomica,
            nombreComercial = e.NombreComercial,
            tipoEstablecimiento = tipoEst,
            direccion = new
            {
                departamento = e.Departamento,
                municipio = e.Municipio,
                complemento = e.Direccion,
            },
            telefono = e.Telefono,
            correo = e.Correo,
            codEstableMH = codEst,
            codEstable = codEst,
            codPuntoVentaMH = codPv,
            codPuntoVenta = codPv,
        };
    }

    private static object BuildEmisorCcf(DteDocumento d, Empresa e, DteConfiguracion? config)
    {
        var tipoEst = string.IsNullOrWhiteSpace(config?.TipoEstablecimientoCodigo) ? "02" : config!.TipoEstablecimientoCodigo;
        var codEst  = string.IsNullOrWhiteSpace(config?.CodigoEstablecimientoMh)    ? null  : config!.CodigoEstablecimientoMh;
        var codPv   = string.IsNullOrWhiteSpace(config?.CodigoPuntoVentaMh)         ? null  : config!.CodigoPuntoVentaMh;

        return new
        {
            nit = e.Nit,
            nrc = e.Nrc,
            nombre = e.RazonSocial,
            codActividad = e.CodigoActividad,
            descActividad = e.ActividadEconomica,
            nombreComercial = e.NombreComercial,
            tipoEstablecimiento = tipoEst,
            direccion = new
            {
                departamento = e.Departamento,
                municipio = e.Municipio,
                complemento = e.Direccion,
            },
            telefono = e.Telefono,
            correo = e.Correo,
            codEstableMH = codEst,
            codEstable = codEst,
            codPuntoVentaMH = codPv,
            codPuntoVenta = codPv,
        };
    }

    private static object? BuildReceptorFactura(DteDocumento d)
    {
        if (string.IsNullOrEmpty(d.ReceptorNombre)
            && string.IsNullOrEmpty(d.ReceptorNumeroDocumento))
            return null;
        return new
        {
            tipoDocumento = MapTipoDocReceptorMh(d.ReceptorTipoDocumento),
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
        // Factura (v1) lleva ivaItem por línea; CCF/NC/ND (v3) NO lo permiten en el cuerpo
        // (el IVA va desglosado en resumen.tributos). Por eso se emiten dos formas distintas.
        return d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) =>
        {
            var tributos = conIvaPorLinea && l.VentaGravada > 0 ? new[] { "20" } : null;
            var noGravado = (double)(l.NoGravado ? l.VentaNoSujeta : 0m);
            var uni = ToInt(l.UnidadMedidaCodigo, defaultValue: 59);

            if (conIvaPorLinea) // CCF / NC / ND — sin ivaItem
                return (object)new
                {
                    numItem = idx + 1,
                    tipoItem = l.TipoItem,
                    numeroDocumento = (string?)null,
                    cantidad = (double)l.Cantidad,
                    codigo = l.Codigo,
                    codTributo = (string?)null,
                    uniMedida = uni,
                    descripcion = l.Descripcion,
                    precioUni = (double)l.PrecioUnitario,
                    montoDescu = (double)l.MontoDescuento,
                    ventaNoSuj = (double)l.VentaNoSujeta,
                    ventaExenta = (double)l.VentaExenta,
                    ventaGravada = (double)l.VentaGravada,
                    tributos,
                    psv = 0d,
                    noGravado,
                };

            return (object)new // Factura — con ivaItem
            {
                numItem = idx + 1,
                tipoItem = l.TipoItem,
                numeroDocumento = (string?)null,
                cantidad = (double)l.Cantidad,
                codigo = l.Codigo,
                codTributo = (string?)null,
                uniMedida = uni,
                descripcion = l.Descripcion,
                precioUni = (double)l.PrecioUnitario,
                montoDescu = (double)l.MontoDescuento,
                ventaNoSuj = (double)l.VentaNoSujeta,
                ventaExenta = (double)l.VentaExenta,
                ventaGravada = (double)l.VentaGravada,
                tributos,
                psv = 0d,
                noGravado,
                ivaItem = (double)l.IvaItem,
            };
        }).ToArray();
    }

    private static int ToInt(string? s, int defaultValue = 0)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : defaultValue;

    /// <summary>
    /// Mapea el código interno de tipo de documento de identidad (catálogo TIPO_DOC_IDENTIDAD)
    /// al código oficial de Hacienda CAT-022 que va en <c>receptor.tipoDocumento</c>:
    /// 36=NIT, 13=DUI, 03=Pasaporte, 02=Carnet de Residente, 37=Otro.
    /// Si ya viene un código numérico MH válido se respeta tal cual.
    /// </summary>
    private static string? MapTipoDocReceptorMh(string? interno)
    {
        if (string.IsNullOrWhiteSpace(interno)) return null;
        return interno.Trim().ToUpperInvariant() switch
        {
            "NIT" => "36",
            "DUI" => "13",
            "PASAPORTE" => "03",
            "CARNET_RESIDENTE" or "CARNET RESIDENTE" => "02",
            "OTRO" or "OTRO_DOCUMENTO" => "37",
            // Códigos MH válidos pasan directo
            "36" or "13" or "03" or "02" or "37" => interno.Trim(),
            _ => interno.Trim(),
        };
    }
}
