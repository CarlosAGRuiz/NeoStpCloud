using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    // ---- validación de request ----

    private static List<string> ValidateRequest(CreateDteDocumentoRequest r)
    {
        var errors = new List<string>();
        if (!TiposSoportados.Contains(r.TipoDteCodigo))
            errors.Add($"Tipo de DTE no soportado: {r.TipoDteCodigo}.");
        if (r.Lineas is null || r.Lineas.Count == 0)
            errors.Add("Debe incluir al menos una línea de detalle.");
        else if (r.TipoDteCodigo == TipoDteCodigos.ComprobanteRetencion)
        {
            // CR (07): cada línea es un documento sujeto a retención.
            for (var i = 0; i < r.Lineas.Count; i++)
            {
                var l = r.Lineas[i];
                var numero = l.DocRelacionadoNumero?.Trim();
                if (string.IsNullOrWhiteSpace(numero))
                    errors.Add($"Línea {i + 1}: número del documento relacionado requerido (código de generación para DTE, o número físico).");
                else if (!DteRetencion.EsCodigoGeneracion(numero) && !DteRetencion.EsNumeroFisicoValido(numero))
                    errors.Add($"Línea {i + 1}: número inválido. Para DTE electrónicos usa el CÓDIGO DE GENERACIÓN (UUID, ej. 76F19422-085D-45B7-A998-4374A3A8EAD7), no el número de control; para documentos físicos, alfanumérico de hasta 20 caracteres sin guiones.");
                if (l.DocRelacionadoFecha is null)
                    errors.Add($"Línea {i + 1}: fecha de emisión del documento relacionado requerida.");
                var monto = l.MontoSujetoRetencion ?? (l.Cantidad * l.PrecioUnitario);
                if (monto <= 0)
                    errors.Add($"Línea {i + 1}: el monto sujeto a retención debe ser > 0.");
                if (!string.IsNullOrWhiteSpace(l.RetencionCodigoMH)
                    && !DteRetencion.CodigosMH.Contains(l.RetencionCodigoMH.Trim().ToUpperInvariant()))
                    errors.Add($"Línea {i + 1}: código de retención inválido (usa 22 = IVA 1%, C4 = IVA 13% o C9 = otros).");
            }
        }
        else if (r.TipoDteCodigo == TipoDteCodigos.ComprobanteLiquidacion)
        {
            // CL (08): cada línea es un documento vendido por cuenta del mandante.
            for (var i = 0; i < r.Lineas.Count; i++)
            {
                var l = r.Lineas[i];
                var numero = (l.DocRelacionadoNumero ?? l.Codigo)?.Trim();
                if (string.IsNullOrWhiteSpace(numero))
                    errors.Add($"Línea {i + 1}: número del documento liquidado requerido (código de generación para DTE, o número físico).");
                else if (!DteRetencion.EsCodigoGeneracion(numero) && !DteRetencion.EsNumeroFisicoValido(numero))
                    errors.Add($"Línea {i + 1}: número inválido. Para DTE electrónicos usa el CÓDIGO DE GENERACIÓN (UUID); para documentos físicos, alfanumérico de hasta 20 caracteres sin guiones.");
                if (l.DocRelacionadoFecha is null)
                    errors.Add($"Línea {i + 1}: fecha de emisión del documento liquidado requerida.");
                if (l.PrecioUnitario <= 0)
                    errors.Add($"Línea {i + 1}: el monto del documento liquidado debe ser > 0.");
                if (l.MontoDescuento < 0)
                    errors.Add($"Línea {i + 1}: el descuento no puede ser negativo.");
            }
        }
        else
        {
            for (var i = 0; i < r.Lineas.Count; i++)
            {
                var l = r.Lineas[i];
                if (string.IsNullOrWhiteSpace(l.Descripcion) && !l.ProductoId.HasValue)
                    errors.Add($"Línea {i + 1}: descripción requerida.");
                if (l.Cantidad <= 0)
                    errors.Add($"Línea {i + 1}: la cantidad debe ser > 0.");
                if (l.PrecioUnitario < 0)
                    errors.Add($"Línea {i + 1}: el precio no puede ser negativo.");
                if (l.MontoDescuento < 0)
                    errors.Add($"Línea {i + 1}: el descuento no puede ser negativo.");
            }
        }

        // Para CCF, NC, ND y Sujeto Excluido: receptor con identificación obligatorio
        var requiereReceptor = r.TipoDteCodigo != TipoDteCodigos.FacturaConsumidorFinal;
        if (requiereReceptor && r.ClienteId is null && r.ReceptorManual is null)
            errors.Add("Para este tipo de DTE el receptor es obligatorio.");

        // NC y ND: documento relacionado
        if (r.TipoDteCodigo is TipoDteCodigos.NotaCredito or TipoDteCodigos.NotaDebito)
        {
            if (string.IsNullOrWhiteSpace(r.NumeroDocumentoRelacionado) && r.DocumentoRelacionadoId is null)
                errors.Add("Nota de crédito/débito requiere documento relacionado.");
        }
        // FEX con ClienteId puede omitir país/tipo de persona: se precargan del
        // catálogo de clientes en CreateAsync, que valida de nuevo tras la precarga.
        if (r.TipoDteCodigo == TipoDteCodigos.FacturaExportacion && r.ClienteId is null)
        {
            var tienePaisCodigo = !string.IsNullOrWhiteSpace(r.ReceptorPaisCodigo)
                || !string.IsNullOrWhiteSpace(r.ReceptorManual?.PaisCodigo);

            var tienePaisNombre = !string.IsNullOrWhiteSpace(r.ReceptorPaisNombre)
                || !string.IsNullOrWhiteSpace(r.ReceptorManual?.PaisNombre);

            var tieneTipoPersona = r.ReceptorTipoPersona.HasValue
                || r.ReceptorManual?.TipoPersona.HasValue == true;

            if (!tienePaisCodigo)
                errors.Add("Factura de exportación requiere país receptor.");

            if (!tienePaisNombre)
                errors.Add("Factura de exportación requiere nombre de país receptor.");

            if (!tieneTipoPersona)
                errors.Add("Factura de exportación requiere tipo de persona receptor.");
        }

        // DCL (09): el corte del período
        if (r.TipoDteCodigo == TipoDteCodigos.DocumentoContableLiquidacion && r.Liquidacion is { } liq)
        {
            if (liq.PeriodoInicio is { } inicio && liq.PeriodoFin is { } fin && fin < inicio)
                errors.Add("El período de liquidación termina antes de empezar.");
            if (liq.PorcentajeComision is { } pct && (pct < 0 || pct > 100))
                errors.Add("El porcentaje de comisión debe estar entre 0 y 100.");
            if (liq.MontoSinPercepcion is { } sinPercepcion && sinPercepcion < 0)
                errors.Add("El monto sin percepción no puede ser negativo.");
            if (liq.CantidadDocumentos is { } cantidad && cantidad <= 0)
                errors.Add("La cantidad de documentos del corte debe ser > 0.");
        }

        return errors;
    }

    private static List<string> ValidateDomain(DteDocumento d)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(d.NumeroControl)) errors.Add("Falta número de control.");
        if (string.IsNullOrWhiteSpace(d.CodigoGeneracion)) errors.Add("Falta código de generación.");
        if (d.Detalles.Count == 0) errors.Add("Documento sin detalles.");
        if (d.MontoTotalOperacion <= 0 && d.TotalPagar <= 0) errors.Add("Monto total debe ser > 0.");
        if (d.TipoDteCodigo != TipoDteCodigos.FacturaConsumidorFinal && string.IsNullOrEmpty(d.ReceptorNombre))
            errors.Add("Receptor obligatorio para este tipo de DTE.");
        if (d.TipoDteCodigo == TipoDteCodigos.ComprobanteCreditoFiscal && string.IsNullOrEmpty(d.ReceptorNrc))
            errors.Add("CCF requiere NRC del receptor.");
        if (d.TipoDteCodigo == TipoDteCodigos.ComprobanteRetencion && string.IsNullOrEmpty(d.ReceptorNrc))
            errors.Add("El Comprobante de Retención requiere NRC del receptor (sujeto de la retención).");

        if (d.TipoDteCodigo == TipoDteCodigos.FacturaExportacion)
        {
            if (string.IsNullOrWhiteSpace(d.ReceptorPaisCodigo))
                errors.Add("Factura de exportación sin código de país receptor.");

            if (string.IsNullOrWhiteSpace(d.ReceptorPaisNombre))
                errors.Add("Factura de exportación sin nombre de país receptor.");

            if (!d.ReceptorTipoPersona.HasValue)
                errors.Add("Factura de exportación sin tipo de persona receptor.");
        }

        if (d.TipoDteCodigo is TipoDteCodigos.ComprobanteLiquidacion or TipoDteCodigos.DocumentoContableLiquidacion)
        {
            // Ambos esquemas declaran codActividad/descActividad del receptor como requeridos
            // y no nulos (el mandante es contribuyente, no consumidor final).
            if (string.IsNullOrWhiteSpace(d.ReceptorCodigoActividad) || string.IsNullOrWhiteSpace(d.ReceptorActividadEconomica))
                errors.Add("Los documentos de liquidación requieren código y descripción de actividad económica del receptor.");
        }

        if (d.TipoDteCodigo == TipoDteCodigos.DocumentoContableLiquidacion)
        {
            // fe-dcl-v2 declara "exclusiveMinimum": 0 en estos importes — un cero se rechaza.
            if (d.MontoTotalOperacion <= 0) errors.Add("El DCL requiere valor de operaciones > 0.");
            if (d.SubTotal <= 0) errors.Add("El DCL requiere sub-total > 0 (revise el monto sin percepción).");
            if (d.TotalGravada <= 0) errors.Add("El DCL requiere monto sujeto a percepción > 0.");
            if (d.IvaTotal <= 0) errors.Add("El DCL requiere IVA de las operaciones > 0.");
            if ((d.LiquidacionIvaPercibido ?? 0) <= 0) errors.Add("El DCL requiere IVA percibido > 0.");
            if (d.TotalPagar <= 0) errors.Add("El DCL requiere líquido a pagar > 0 (la comisión supera el monto liquidado).");
        }

        return errors;
    }

    // ---- mapping ----

    private static DteDocumentoDto MapToDto(DteDocumento d) => new()
    {
        Id = d.Id,
        EmpresaId = d.EmpresaId,
        TipoDteCodigo = d.TipoDteCodigo,
        VersionDte = d.VersionDte,
        AmbienteCodigo = d.AmbienteCodigo,
        NumeroControl = d.NumeroControl,
        CodigoGeneracion = d.CodigoGeneracion,
        SelloRecibido = d.SelloRecibido,
        ModeloFacturacion = d.ModeloFacturacion,
        TipoTransmision = d.TipoTransmision,
        FechaEmision = d.FechaEmision,
        HoraEmision = d.HoraEmision,
        TipoMonedaCodigo = d.TipoMonedaCodigo,
        ClienteId = d.ClienteId,
        ReceptorTipoDocumento = d.ReceptorTipoDocumento,
        ReceptorNumeroDocumento = d.ReceptorNumeroDocumento,
        ReceptorNrc = d.ReceptorNrc,
        ReceptorNombre = d.ReceptorNombre,
        ReceptorTipoContribuyente = d.ReceptorTipoContribuyente,
        ReceptorCodigoActividad = d.ReceptorCodigoActividad,
        ReceptorActividadEconomica = d.ReceptorActividadEconomica,
        ReceptorDepartamentoCodigo = d.ReceptorDepartamentoCodigo,
        ReceptorMunicipioCodigo = d.ReceptorMunicipioCodigo,
        ReceptorDistritoCodigo = d.ReceptorDistritoCodigo,
        ReceptorDireccion = d.ReceptorDireccion,
        ReceptorCorreo = d.ReceptorCorreo,
        ReceptorTelefono = d.ReceptorTelefono,
        ReceptorPaisCodigo = d.ReceptorPaisCodigo,
        ReceptorPaisNombre = d.ReceptorPaisNombre,
        ReceptorTipoPersona = d.ReceptorTipoPersona,
        CondicionOperacionCodigo = d.CondicionOperacionCodigo,
        FormaPagoCodigo = d.FormaPagoCodigo,
        PlazoDias = d.PlazoDias,
        DocumentoRelacionadoId = d.DocumentoRelacionadoId,
        NumeroDocumentoRelacionado = d.NumeroDocumentoRelacionado,
        TipoDteRelacionado = d.TipoDteRelacionado,
        Observaciones = d.Observaciones,
        VentaTerceroNit = d.VentaTerceroNit,
        VentaTerceroNombre = d.VentaTerceroNombre,
        TotalNoSujeto = d.TotalNoSujeto,
        TotalExenta = d.TotalExenta,
        TotalGravada = d.TotalGravada,
        SubTotalVentas = d.SubTotalVentas,
        TotalDescuento = d.TotalDescuento,
        IvaTotal = d.IvaTotal,
        IvaRetenido = d.IvaRetenido,
        ReteRenta = d.ReteRenta,
        SubTotal = d.SubTotal,
        MontoTotalOperacion = d.MontoTotalOperacion,
        TotalNoGravado = d.TotalNoGravado,
        TotalPagar = d.TotalPagar,
        TotalLetras = d.TotalLetras,
        EstadoCodigo = d.EstadoCodigo,
        CreatedAt = d.CreatedAt,
        GeneradoAt = d.GeneradoAt,
        ValidadoAt = d.ValidadoAt,
        FirmadoAt = d.Json?.FirmadoAt,
        EnviadoAt = d.EnviadoAt,
        ProcesadoAt = d.ProcesadoAt,
        RespuestaAt = d.Json?.RespuestaAt,
        Detalles = d.Detalles
            .OrderBy(x => x.NumeroLinea)
            .Select(l => new DteDocumentoDetalleDto
            {
                Id = l.Id,
                NumeroLinea = l.NumeroLinea,
                ProductoId = l.ProductoId,
                TipoItem = l.TipoItem,
                Codigo = l.Codigo,
                Descripcion = l.Descripcion,
                UnidadMedidaCodigo = l.UnidadMedidaCodigo,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario,
                MontoDescuento = l.MontoDescuento,
                VentaNoSujeta = l.VentaNoSujeta,
                VentaExenta = l.VentaExenta,
                VentaGravada = l.VentaGravada,
                IvaItem = l.IvaItem,
                NoGravado = l.NoGravado,
                Observaciones = l.Observaciones,
                DocRelacionadoTipoDte = l.DocRelacionadoTipoDte,
                DocRelacionadoFecha = l.DocRelacionadoFecha,
                RetencionCodigoMH = l.RetencionCodigoMH,
            }).ToList(),
        JsonDte = d.Json?.JsonDte,
        JsonFirmado = d.Json?.JsonFirmado,
        RespuestaHacienda = d.Json?.RespuestaHacienda,
        IntentoRetransmision = d.IntentoRetransmision,
        UltimoIntentoRetransmisionAt = d.UltimoIntentoRetransmisionAt,
        NotaInterna = d.NotaInterna,
    };

}
