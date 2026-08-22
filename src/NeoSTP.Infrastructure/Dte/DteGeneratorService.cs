using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Construye el JSON DTE según el esquema oficial de Hacienda El Salvador.
/// Soporta: 01 Factura, 03 CCF, 04 Nota de Remisión, 05 NC, 06 ND, 07 Retención,
/// 08 Comprobante de Liquidación, 09 Documento Contable de Liquidación,
/// 11 Exportación, 14 Sujeto Excluido y 15 Donación.
/// </summary>
public class DteGeneratorService : IDteGeneratorService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    // Defaults territoriales (división 2024) configurables — reemplazan los literales "23"/"03".
    private readonly TerritorialOptions _territorial;

    public DteGeneratorService(IOptions<TerritorialOptions> territorial)
    {
        _territorial = territorial.Value;
    }

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
            TipoDteCodigos.NotaRemision => BuildNotaRemision(d, emisor, config),
            TipoDteCodigos.FacturaExportacion => BuildFacturaExportacion(d, emisor, config, _territorial),
            TipoDteCodigos.ComprobanteDonacion => BuildComprobanteDonacion(d, emisor, config, _territorial),
            TipoDteCodigos.ComprobanteRetencion => BuildComprobanteRetencion(d, emisor, config),
            TipoDteCodigos.ComprobanteLiquidacion => BuildComprobanteLiquidacion(d, emisor, config, _territorial),
            TipoDteCodigos.DocumentoContableLiquidacion => BuildDocumentoContableLiquidacion(d, emisor, config, _territorial),
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
            emisor = BuildEmisorNotaCreditoDebito(emisor, config),
            receptor = BuildReceptorCcf(d),
            ventaTercero = BuildVentaTercero(d),
            cuerpoDocumento = BuildCuerpoNotaCreditoDebito(d),
            resumen = BuildResumenNotaCreditoDebito(d, isNotaCredito),
            extension = (object?)null,
            apendice = (object?)null,
        };
    }

    /// <summary>
    /// Emisor para NC/ND: el esquema de Hacienda para estos tipos NO admite los códigos de
    /// establecimiento ni de punto de venta que sí lleva el CCF. Enviarlos hace que MH rechace
    /// con "Campo codEstable no esta permitido en #/emisor" (verificado en apitest).
    /// </summary>
    private static object BuildEmisorNotaCreditoDebito(Empresa e, DteConfiguracion? config) => new
    {
        nit = e.Nit,
        nrc = e.Nrc,
        nombre = e.RazonSocial,
        codActividad = e.CodigoActividad,
        descActividad = e.ActividadEconomica,
        nombreComercial = e.NombreComercial,
        tipoEstablecimiento = string.IsNullOrWhiteSpace(config?.TipoEstablecimientoCodigo) ? "02" : config!.TipoEstablecimientoCodigo,
        direccion = new
        {
            departamento = e.Departamento,
            municipio = e.Municipio,
            complemento = e.Direccion,
        },
        telefono = e.Telefono,
        correo = e.Correo,
    };

    /// <summary>
    /// Cuerpo para NC/ND: a diferencia del CCF no admite <c>psv</c> ni <c>noGravado</c>, y cada
    /// línea debe referenciar en <c>numeroDocumento</c> el documento que ajusta (código de
    /// generación del relacionado).
    /// </summary>
    private static object[] BuildCuerpoNotaCreditoDebito(DteDocumento d)
    {
        var relacionado = d.NumeroDocumentoRelacionado;
        return d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) => (object)new
        {
            numItem = idx + 1,
            tipoItem = l.TipoItem,
            numeroDocumento = relacionado,
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
            tributos = l.VentaGravada > 0 ? new[] { "20" } : null,
        }).ToArray();
    }

    /// <summary>
    /// Resumen para NC/ND: no admite <c>pagos</c>, <c>porcentajeDescuento</c>,
    /// <c>totalNoGravado</c>, <c>saldoFavor</c> ni <c>totalPagar</c> (una nota ajusta un
    /// documento previo; no se "paga" por sí sola). Los dos tipos difieren en un campo:
    /// la <b>ND exige</b> <c>numPagoElectronico</c> y la <b>NC no lo permite</b> — ambas
    /// reglas verificadas contra apitest de Hacienda.
    /// </summary>
    private static object BuildResumenNotaCreditoDebito(DteDocumento d, bool isNotaCredito)
    {
        var tributos = new[]
        {
            new { codigo = "20", descripcion = "Impuesto al Valor Agregado 13%", valor = (double)d.IvaTotal },
        };

        if (isNotaCredito)
            return new
            {
                totalNoSuj = (double)d.TotalNoSujeto,
                totalExenta = (double)d.TotalExenta,
                totalGravada = (double)d.TotalGravada,
                subTotalVentas = (double)d.SubTotalVentas,
                descuNoSuj = 0d,
                descuExenta = 0d,
                descuGravada = (double)d.DescuentoGravada,
                totalDescu = (double)d.TotalDescuento,
                tributos,
                subTotal = (double)d.SubTotal,
                ivaPerci1 = 0d,
                ivaRete1 = (double)d.IvaRetenido,
                reteRenta = (double)d.ReteRenta,
                montoTotalOperacion = (double)d.MontoTotalOperacion,
                totalLetras = d.TotalLetras,
                condicionOperacion = ToInt(d.CondicionOperacionCodigo),
            };

        return new
        {
            totalNoSuj = (double)d.TotalNoSujeto,
            totalExenta = (double)d.TotalExenta,
            totalGravada = (double)d.TotalGravada,
            subTotalVentas = (double)d.SubTotalVentas,
            descuNoSuj = 0d,
            descuExenta = 0d,
            descuGravada = (double)d.DescuentoGravada,
            totalDescu = (double)d.TotalDescuento,
            tributos,
            subTotal = (double)d.SubTotal,
            ivaPerci1 = 0d,
            ivaRete1 = (double)d.IvaRetenido,
            reteRenta = (double)d.ReteRenta,
            montoTotalOperacion = (double)d.MontoTotalOperacion,
            totalLetras = d.TotalLetras,
            condicionOperacion = ToInt(d.CondicionOperacionCodigo),
            numPagoElectronico = (string?)null,
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

    // ----------- 04 Nota de Remisión ---------------------------------

    private static object BuildNotaRemision(DteDocumento d, Empresa emisor, DteConfiguracion? config) => new
    {
        identificacion = BuildIdentificacion(d, 3),
        documentoRelacionado = BuildDocumentoRelacionado(d),
        emisor = BuildEmisor(d, emisor, config),
        receptor = BuildReceptorRemision(d),
        ventaTercero = BuildVentaTercero(d),
        cuerpoDocumento = BuildCuerpoRemision(d),
        resumen = BuildResumenRemision(d),
        extension = new   // NR: extension sin placaVehiculo
        {
            nombEntrega = (string?)null,
            docuEntrega = (string?)null,
            nombRecibe = d.ReceptorNombre,
            docuRecibe = d.ReceptorNumeroDocumento,
            observaciones = d.Observaciones,
        },
        apendice = (object?)null,
    };

    private static object BuildReceptorRemision(DteDocumento d) => new
    {
        tipoDocumento = MapTipoDocReceptorMh(d.ReceptorTipoDocumento),
        numDocumento = d.ReceptorNumeroDocumento,
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
        bienTitulo = "05",   // CAT-018 "Bienes Remitidos a Título de" — 05 Traslado entre establecimientos
    };

    private static object[] BuildCuerpoRemision(DteDocumento d) =>
        d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) => (object)new
        {
            numItem = idx + 1,
            tipoItem = l.TipoItem,
            numeroDocumento = (string?)null,
            codigo = l.Codigo,
            codTributo = (string?)null,
            descripcion = l.Descripcion,
            cantidad = (double)l.Cantidad,
            uniMedida = ToInt(l.UnidadMedidaCodigo, defaultValue: 59),
            precioUni = (double)l.PrecioUnitario,
            montoDescu = (double)l.MontoDescuento,
            ventaNoSuj = (double)l.VentaNoSujeta,
            ventaExenta = (double)l.VentaExenta,
            ventaGravada = (double)l.VentaGravada,
            tributos = l.VentaGravada > 0 ? new[] { "20" } : null,
        }).ToArray();

    private static object BuildResumenRemision(DteDocumento d) => new
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
        // MH exige el desglose de tributos cuando hay venta gravada; enviarlo en null
        // devuelve "[resumen.tributos] FALTAN DATOS PARA VALIDAR INFORMACION" (apitest).
        tributos = d.TotalGravada > 0
            ? new[] { new { codigo = "20", descripcion = "Impuesto al Valor Agregado 13%", valor = (double)d.IvaTotal } }
            : null,
        subTotal = (double)d.SubTotal,
        montoTotalOperacion = (double)d.MontoTotalOperacion,
        totalLetras = d.TotalLetras,
    };

    // ----------- 11 Factura de Exportación ---------------------------

    private static object BuildFacturaExportacion(DteDocumento d, Empresa e, DteConfiguracion? config, TerritorialOptions terr) => new
    {
        identificacion = BuildIdentificacion(d, 3),
        documentoRelacionado = (object?)null,
        emisor = BuildEmisorExportacion(e, config, terr),
        receptor = BuildReceptorExportacion(d),
        otrosDocumentos = (object?)null,
        ventaTercero = (object?)null,
        compraTercero = (object?)null,
        cuerpoDocumento = d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) => (object)new
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
            ventaGravada = (double)(l.VentaGravada + l.VentaExenta + l.VentaNoSujeta),
            tributos = new[] { "C3" },   // CAT-015 C3 = IVA exportaciones 0%
            noGravado = 0d,
        }).ToArray(),
        resumen = new
        {
            totalGravada = (double)d.SubTotalVentas,
            descuGravada = (double)d.DescuentoGravada,
            porcentajeDescuento = (double)d.PorcentajeDescuento,
            totalDescu = (double)d.TotalDescuento,
            seguro = 0d,
            flete = 0d,
            tributos = new[]
            {
                new { codigo = "C3", descripcion = "Impuesto al Valor Agregado (exportaciones) 0%", valor = 0.0 },
            },
            montoTotalOperacion = (double)d.MontoTotalOperacion,
            totalNoGravado = 0d,
            totalNoOnerosas = 0d,
            totalPagar = (double)d.TotalPagar,
            totalLetras = d.TotalLetras,
            saldoFavor = 0d,
            condicionOperacion = ToInt(d.CondicionOperacionCodigo),
            pagos = new[]
            {
                new { codigo = (string?)"01", montoPago = (double)d.TotalPagar, referencia = (string?)null, plazo = (string?)null, periodo = (int?)null },
            },
            codIncoterms = (string?)null,
            descIncoterms = (string?)null,
            numPagoElectronico = (string?)null,
            observaciones = d.Observaciones,
        },
        apendice = (object?)null,
    };

    private static object BuildEmisorExportacion(Empresa e, DteConfiguracion? config, TerritorialOptions terr)
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
            nombreComercial = e.NombreComercial,
            // FEX v3 usa división territorial 2024 (municipio nuevo + distrito). El municipio
            // por defecto sale de Dte:Territorial; el distrito del emisor si lo tiene registrado.
            direccion = new { departamento = e.Departamento ?? "06", municipio = terr.MunicipioDivision2024Default, distrito = e.Distrito ?? terr.DistritoDefault, complemento = e.Direccion },
            telefono = e.Telefono,
            correo = e.Correo,
            codEstable = codEst,
            codPuntoVenta = codPv,
            tipoItemExpor = 2,            // probar 2 (servicios)
            recintoFiscal = (string?)null,
            tipoRegimen = (string?)null,
            regimen = (string?)null,
        };
    }

    private static object BuildReceptorExportacion(DteDocumento d)
    {
        if (string.IsNullOrWhiteSpace(d.ReceptorPaisCodigo))
            throw new InvalidOperationException("Factura de exportación sin código de país receptor.");

        if (string.IsNullOrWhiteSpace(d.ReceptorPaisNombre))
            throw new InvalidOperationException("Factura de exportación sin nombre de país receptor.");

        if (!d.ReceptorTipoPersona.HasValue)
            throw new InvalidOperationException("Factura de exportación sin tipo de persona receptor.");

        return new
        {
            nombre = d.ReceptorNombre,
            tipoDocumento = MapTipoDocReceptorMh(d.ReceptorTipoDocumento) ?? "37",
            numDocumento = d.ReceptorNumeroDocumento,
            descActividad = d.ReceptorActividadEconomica ?? "Comercio exterior",
            nombreComercial = (string?)d.ReceptorNombre,
            codPais = d.ReceptorPaisCodigo,
            nombrePais = d.ReceptorPaisNombre,
            complemento = d.ReceptorDireccion ?? "Exterior",
            tipoPersona = d.ReceptorTipoPersona.Value,
            telefono = d.ReceptorTelefono,
            correo = d.ReceptorCorreo,
        };
    }

    // ----------- 15 Comprobante de Donación --------------------------

    private static object BuildComprobanteDonacion(DteDocumento d, Empresa e, DteConfiguracion? config, TerritorialOptions terr)
    {
        var codEst = string.IsNullOrWhiteSpace(config?.CodigoEstablecimientoMh) ? null : config!.CodigoEstablecimientoMh;
        var codPv  = string.IsNullOrWhiteSpace(config?.CodigoPuntoVentaMh)      ? null : config!.CodigoPuntoVentaMh;
        return new
        {
            identificacion = new   // CD no lleva tipoContingencia/motivoContin
            {
                version = 2,
                ambiente = d.AmbienteCodigo == "PRODUCCION" ? "01" : "00",
                tipoDte = d.TipoDteCodigo,
                numeroControl = d.NumeroControl,
                codigoGeneracion = d.CodigoGeneracion,
                tipoModelo = d.ModeloFacturacion,
                tipoOperacion = d.TipoTransmision,
                fecEmi = d.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                horEmi = d.HoraEmision.ToString(@"hh\:mm\:ss"),
                tipoMoneda = d.TipoMonedaCodigo ?? "USD",
            },
            emisor = new   // Donatario
            {
                tipoDocumento = "36",   // NIT
                numDocumento = e.Nit,
                nrc = e.Nrc,
                nombre = e.RazonSocial,
                codActividad = e.CodigoActividad,
                descActividad = e.ActividadEconomica,
                nombreComercial = e.NombreComercial,
                // CD v2 usa división territorial 2024 (municipio nuevo + distrito).
                // Municipio por defecto desde Dte:Territorial; distrito del emisor si está registrado.
                direccion = new { departamento = e.Departamento ?? "06", municipio = terr.MunicipioDivision2024Default, distrito = e.Distrito ?? terr.DistritoDefault, complemento = e.Direccion },
                telefono = e.Telefono,
                correo = e.Correo,
                codEstable = codEst,
                codPuntoVenta = codPv,
            },
            receptor = new   // Donante
            {
                tipoDocumento = MapTipoDocReceptorMh(d.ReceptorTipoDocumento) ?? "36",
                numDocumento = d.ReceptorNumeroDocumento,
                nrc = d.ReceptorNrc,
                nombre = d.ReceptorNombre,
                codActividad = d.ReceptorCodigoActividad,
                descActividad = d.ReceptorActividadEconomica,
                direccion = string.IsNullOrEmpty(d.ReceptorDepartamentoCodigo) ? null : new
                {
                    departamento = d.ReceptorDepartamentoCodigo,
                    municipio = terr.MunicipioDivision2024Default,   // división 2024 (default config)
                    distrito = d.ReceptorDistritoCodigo ?? terr.DistritoDefault,
                    complemento = d.ReceptorDireccion,
                },
                telefono = d.ReceptorTelefono,
                correo = d.ReceptorCorreo,
                codDomiciliado = 1,
                codPais = "9300",
            },
            otrosDocumentos = new[]
            {
                new { codDocAsociado = 1, descDocumento = (string?)"Acta de donacion", detalleDocumento = (string?)"Donacion de bienes" },
            },
            cuerpoDocumento = d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) => (object)new
            {
                numItem = idx + 1,
                tipoDonacion = 1,
                cantidad = (double)l.Cantidad,
                codigo = l.Codigo,
                uniMedida = ToInt(l.UnidadMedidaCodigo, defaultValue: 59),
                descripcion = l.Descripcion,
                tipoDepreciacion = 0d,
                valorUni = (double)l.PrecioUnitario,
                valor = (double)(l.Cantidad * l.PrecioUnitario),
            }).ToArray(),
            resumen = new
            {
                valorTotal = (double)d.TotalPagar,
                totalLetras = d.TotalLetras,
                pagos = new[]
                {
                    new { codigo = (string?)"01", montoPago = (double)d.TotalPagar, referencia = (string?)null },
                },
            },
            apendice = (object?)null,
        };
    }

    // ----------- Bloques comunes -------------------------------------

    // ----------- 07 Comprobante de Retención (fe-cr-v1) --------------

    private static object BuildComprobanteRetencion(DteDocumento d, Empresa emisor, DteConfiguracion? config)
    {
        return new
        {
            identificacion = BuildIdentificacion(d, 1),
            emisor = BuildEmisorRetencion(emisor, config),
            receptor = BuildReceptorRetencion(d),
            // Cuerpo: documentos sujetos a retención. El número sale del documento relacionado
            // (código de generación en MAYÚSCULAS si es electrónico), no del código del ítem.
            // El campo se llama tipoDoc en este esquema y no admite tipoGeneracion.
            cuerpoDocumento = d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) =>
            {
                var numRelacionado = l.Codigo;   // en 07 la línea guarda aquí el número del doc retenido
                return new
                {
                numItem = idx + 1,
                tipoDte = string.IsNullOrWhiteSpace(l.DocRelacionadoTipoDte) ? "03" : l.DocRelacionadoTipoDte,
                // tipoDoc = forma de generación del documento retenido: 2 electrónico, 1 físico.
                tipoDoc = DteRetencion.EsCodigoGeneracion(numRelacionado) ? 2 : 1,
                numDocumento = DteRetencion.EsCodigoGeneracion(numRelacionado) ? numRelacionado.ToUpperInvariant() : numRelacionado,
                fechaEmision = (l.DocRelacionadoFecha ?? d.FechaEmision).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                montoSujetoGrav = (double)l.VentaGravada,
                codigoRetencionMH = string.IsNullOrWhiteSpace(l.RetencionCodigoMH) ? DteRetencion.CodigoIva1 : l.RetencionCodigoMH,
                ivaRetenido = (double)l.IvaItem,
                descripcion = l.Descripcion,
                };
            }).ToArray(),
            resumen = new
            {
                totalSujetoRetencion = (double)d.TotalGravada,
                totalIVAretenido = (double)d.IvaRetenido,
                totalIVAretenidoLetras = d.TotalLetras,
            },
            extension = (object?)null,
            apendice = (object?)null,
        };
    }

    /// <summary>
    /// Emisor del comprobante de retención (07): mismos datos que el CCF pero con otros nombres.
    /// MH exige <c>codigo</c>, <c>codigoMH</c>, <c>puntoVenta</c> y <c>puntoVentaMH</c>, y rechaza
    /// los <c>codEstable*</c>/<c>codPuntoVenta*</c> del resto de tipos (verificado en apitest).
    /// </summary>
    private static object BuildEmisorRetencion(Empresa e, DteConfiguracion? config)
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
            nombreComercial = e.NombreComercial,
            tipoEstablecimiento = string.IsNullOrWhiteSpace(config?.TipoEstablecimientoCodigo) ? "02" : config!.TipoEstablecimientoCodigo,
            direccion = new
            {
                departamento = e.Departamento,
                municipio = e.Municipio,
                complemento = e.Direccion,
            },
            telefono = e.Telefono,
            correo = e.Correo,
            codigoMH = codEst,
            codigo = codEst,
            puntoVentaMH = codPv,
            puntoVenta = codPv,
        };
    }

    private static object BuildReceptorRetencion(DteDocumento d) => new
    {
        tipoDocumento = MapTipoDocReceptorMh(d.ReceptorTipoDocumento) ?? "36",
        numDocumento = d.ReceptorNumeroDocumento,
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

    // ----------- 08 Comprobante de Liquidación (fe-cl-v2) ------------

    /// <summary>
    /// Comprobante de Liquidación (08). Lo emite el mandatario al mandante y su cuerpo es la
    /// lista de documentos vendidos por cuenta de éste — no productos. El esquema fe-cl-v2
    /// cierra con <c>additionalProperties: false</c> en todos los bloques, así que solo van
    /// los campos que declara: el emisor no lleva <c>tipoEstablecimiento</c> ni los
    /// <c>codEstableMH</c>/<c>codPuntoVentaMH</c> del resto de tipos, la identificación no
    /// admite contingencia (tipoModelo y tipoOperacion son <c>const 1</c>) pero sí exige
    /// <c>fusion</c>, y no existe bloque <c>extension</c>.
    /// <para>
    /// <b>Regla de negocio que no está en el esquema</b> (verificada en apitest): cuando la
    /// línea referencia un DTE electrónico (<c>tipoGeneracion 2</c>), Hacienda cruza ese
    /// documento y exige que se haya emitido <b>por cuenta del mandante</b>, es decir con el
    /// bloque <c>ventaTercero</c> apuntando al receptor de esta liquidación. Liquidar un DTE
    /// propio del emisor se rechaza con <c>099 ERROR NO CATALOGADO</c> y <c>observaciones</c>
    /// vacías — sin pista del campo culpable. Con documentos físicos
    /// (<c>tipoGeneracion 1</c>) no hay cruce y pasa igual.
    /// </para>
    /// </summary>
    private static object BuildComprobanteLiquidacion(DteDocumento d, Empresa e, DteConfiguracion? config, TerritorialOptions terr)
    {
        var codEst = string.IsNullOrWhiteSpace(config?.CodigoEstablecimientoMh) ? null : config!.CodigoEstablecimientoMh;
        var codPv  = string.IsNullOrWhiteSpace(config?.CodigoPuntoVentaMh)      ? null : config!.CodigoPuntoVentaMh;

        return new
        {
            identificacion = new
            {
                version = 2,
                ambiente = d.AmbienteCodigo == "PRODUCCION" ? "01" : "00",
                tipoDte = d.TipoDteCodigo,
                numeroControl = d.NumeroControl,
                codigoGeneracion = d.CodigoGeneracion,
                tipoModelo = 1,      // const en el esquema: el CL no admite modelo diferido
                tipoOperacion = 1,   // const en el esquema: el CL no admite contingencia
                fecEmi = d.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                horEmi = d.HoraEmision.ToString(@"hh\:mm\:ss"),
                tipoMoneda = d.TipoMonedaCodigo ?? "USD",
                fusion = (string?)null,
            },
            emisor = new
            {
                nit = e.Nit,
                nrc = e.Nrc,
                nombre = e.RazonSocial,
                codActividad = e.CodigoActividad,
                descActividad = e.ActividadEconomica,
                nombreComercial = NullSiVacio(e.NombreComercial),
                // CL v2 usa división territorial 2024 (municipio nuevo + distrito), igual que FEX y CD.
                direccion = new
                {
                    departamento = e.Departamento ?? "06",
                    municipio = terr.MunicipioDivision2024Default,
                    distrito = e.Distrito ?? terr.DistritoDefault,
                    complemento = e.Direccion,
                },
                telefono = NullSiVacio(e.Telefono),
                correo = e.Correo,
                codEstable = codEst,
                codPuntoVenta = codPv,
            },
            receptor = new
            {
                tipoDocumento = MapTipoDocReceptorMh(d.ReceptorTipoDocumento) ?? "36",
                numDocumento = d.ReceptorNumeroDocumento,
                codDomiciliado = 1,
                nrc = NullSiVacio(d.ReceptorNrc),
                nombre = d.ReceptorNombre,
                codActividad = d.ReceptorCodigoActividad,
                descActividad = d.ReceptorActividadEconomica,
                nombreComercial = NullSiVacio(d.ReceptorNombre),
                direccion = new
                {
                    departamento = d.ReceptorDepartamentoCodigo ?? "06",
                    municipio = terr.MunicipioDivision2024Default,
                    distrito = d.ReceptorDistritoCodigo ?? terr.DistritoDefault,
                    complemento = d.ReceptorDireccion,
                },
                telefono = NullSiVacio(d.ReceptorTelefono),
                correo = NullSiVacio(d.ReceptorCorreo),
            },
            cuerpoDocumento = d.Detalles.OrderBy(l => l.NumeroLinea).Select((l, idx) =>
            {
                // Igual que en el 07, la línea guarda en Codigo el número del documento
                // liquidado: código de generación en MAYÚSCULAS si es electrónico.
                var numRelacionado = l.Codigo;
                var esElectronico = DteRetencion.EsCodigoGeneracion(numRelacionado);
                return (object)new
                {
                    numItem = idx + 1,
                    tipoDte = string.IsNullOrWhiteSpace(l.DocRelacionadoTipoDte) ? "01" : l.DocRelacionadoTipoDte,
                    tipoGeneracion = esElectronico ? 2 : 1,
                    numeroDocumento = esElectronico ? numRelacionado.ToUpperInvariant() : numRelacionado,
                    fechaEmision = (l.DocRelacionadoFecha ?? d.FechaEmision).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ventaNoSuj = (double)l.VentaNoSujeta,
                    ventaExenta = (double)l.VentaExenta,
                    ventaGravada = (double)l.VentaGravada,
                    exportaciones = 0d,
                    tributos = l.VentaGravada > 0 ? new[] { "20" } : null,
                    ivaItem = (double)l.IvaItem,
                    observaciones = NullSiVacio(l.Observaciones),
                };
            }).ToArray(),
            resumen = new
            {
                totalNoSuj = (double)d.TotalNoSujeto,
                totalExenta = (double)d.TotalExenta,
                totalGravada = (double)d.TotalGravada,
                exportacion = 0d,
                subTotalVentas = (double)d.SubTotalVentas,
                tributos = d.TotalGravada > 0
                    ? new[] { new { codigo = "20", descripcion = "Impuesto al Valor Agregado 13%", valor = (double)d.IvaTotal } }
                    : null,
                montoTotalOperacion = (double)d.MontoTotalOperacion,
                ivaPerci = 0d,
                total = (double)d.TotalPagar,
                totalLetras = d.TotalLetras,
                condicionOperacion = ToInt(d.CondicionOperacionCodigo),
                observaciones = NullSiVacio(d.Observaciones),
            },
            apendice = (object?)null,
        };
    }

    // ----------- 09 Documento Contable de Liquidación (fe-dcl-v2) ----

    /// <summary>
    /// Documento Contable de Liquidación (09). Es el corte del período, no una venta: su
    /// <c>cuerpoDocumento</c> es un <b>objeto único</b> (no un arreglo) con el valor liquidado,
    /// la percepción de IVA del 2 %, la comisión del mandatario y el líquido a pagar; y no
    /// existe bloque <c>resumen</c>. Los importes los deriva <see cref="DteLiquidacion"/> desde
    /// las líneas del documento, que aquí solo sirven de insumo y no viajan al JSON.
    /// </summary>
    private static object BuildDocumentoContableLiquidacion(DteDocumento d, Empresa e, DteConfiguracion? config, TerritorialOptions terr)
    {
        var codEst = string.IsNullOrWhiteSpace(config?.CodigoEstablecimientoMh) ? null : config!.CodigoEstablecimientoMh;
        var codPv  = string.IsNullOrWhiteSpace(config?.CodigoPuntoVentaMh)      ? null : config!.CodigoPuntoVentaMh;
        var inicio = d.LiquidacionPeriodoInicio ?? d.FechaEmision;
        var fin    = d.LiquidacionPeriodoFin ?? d.FechaEmision;

        return new
        {
            identificacion = new
            {
                version = 2,
                ambiente = d.AmbienteCodigo == "PRODUCCION" ? "01" : "00",
                tipoDte = d.TipoDteCodigo,
                numeroControl = d.NumeroControl,
                codigoGeneracion = d.CodigoGeneracion,
                tipoModelo = 1,      // const en el esquema
                tipoOperacion = 1,   // const en el esquema: el DCL no admite contingencia
                fecEmi = d.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                horEmi = d.HoraEmision.ToString(@"hh\:mm\:ss"),
                tipoMoneda = d.TipoMonedaCodigo ?? "USD",
            },
            emisor = new
            {
                nit = e.Nit,
                nrc = e.Nrc,
                nombre = e.RazonSocial,
                codActividad = e.CodigoActividad,
                descActividad = e.ActividadEconomica,
                nombreComercial = NullSiVacio(e.NombreComercial),
                telefono = e.Telefono,
                correo = e.Correo,
                direccion = new
                {
                    departamento = e.Departamento ?? "06",
                    municipio = terr.MunicipioDivision2024Default,
                    distrito = e.Distrito ?? terr.DistritoDefault,
                    complemento = e.Direccion,
                },
                codEstable = codEst,
                codPuntoVenta = codPv,
            },
            receptor = new
            {
                nit = d.ReceptorNumeroDocumento,
                nrc = NullSiVacio(d.ReceptorNrc),
                nombre = d.ReceptorNombre,
                codActividad = d.ReceptorCodigoActividad,
                descActividad = d.ReceptorActividadEconomica,
                nombreComercial = NullSiVacio(d.ReceptorNombre),
                tipoEstablecimiento = string.IsNullOrWhiteSpace(config?.TipoEstablecimientoCodigo) ? "02" : config!.TipoEstablecimientoCodigo,
                direccion = new
                {
                    departamento = d.ReceptorDepartamentoCodigo ?? "06",
                    municipio = terr.MunicipioDivision2024Default,
                    distrito = d.ReceptorDistritoCodigo ?? terr.DistritoDefault,
                    complemento = d.ReceptorDireccion,
                },
                telefono = NullSiVacio(d.ReceptorTelefono),
                correo = d.ReceptorCorreo,
            },
            cuerpoDocumento = new
            {
                periodoLiquidacionFechaInicio = inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                periodoLiquidacionFechaFin = fin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                codLiquidacion = NullSiVacio(d.LiquidacionCodigo),
                cantidadDoc = d.LiquidacionCantidadDocumentos ?? d.Detalles.Count,
                valorOperaciones = (double)d.MontoTotalOperacion,
                montoSinPercepcion = (double)(d.LiquidacionMontoSinPercepcion ?? 0m),
                descripSinPercepcion = NullSiVacio(d.LiquidacionDescripcionSinPercepcion),
                subTotal = (double)d.SubTotal,
                iva = (double)d.IvaTotal,
                montoSujetoPercepcion = (double)d.TotalGravada,
                ivaPercibido = (double)(d.LiquidacionIvaPercibido ?? 0m),
                comision = (double)(d.LiquidacionComision ?? 0m),
                porcentComision = (double)(d.LiquidacionPorcentajeComision ?? DteLiquidacion.PorcentajeComisionDefault),
                ivaComision = (double)(d.LiquidacionIvaComision ?? 0m),
                liquidoApagar = (double)d.TotalPagar,
                totalLetras = d.TotalLetras,
                observaciones = NullSiVacio(d.Observaciones),
            },
            extension = new
            {
                nombEntrega = NullSiVacio(d.LiquidacionNombreEntrega) ?? e.RazonSocial,
                docuEntrega = NullSiVacio(d.LiquidacionDocumentoEntrega) ?? e.Nit,
                codEmpleado = NullSiVacio(d.LiquidacionCodigoEmpleado),
            },
            apendice = (object?)null,
        };
    }

    /// <summary>
    /// Devuelve null en lugar de cadena vacía. Los esquemas v2 declaran
    /// <c>minLength: 1</c> en los campos opcionales, así que una cadena vacía se rechaza
    /// mientras que null pasa.
    /// </summary>
    private static string? NullSiVacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

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
