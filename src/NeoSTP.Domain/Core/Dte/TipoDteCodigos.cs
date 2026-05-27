namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Códigos oficiales de tipos de DTE según catálogo CAT-002 de Hacienda El Salvador.
/// </summary>
public static class TipoDteCodigos
{
    public const string FacturaConsumidorFinal = "01";
    public const string ComprobanteCreditoFiscal = "03";
    public const string NotaRemision = "04";
    public const string NotaCredito = "05";
    public const string NotaDebito = "06";
    public const string ComprobanteRetencion = "07";
    public const string ComprobanteLiquidacion = "08";
    public const string DocumentoContableLiquidacion = "09";
    public const string FacturaExportacion = "11";
    public const string FacturaSujetoExcluido = "14";
    public const string ComprobanteDonacion = "15";
}

/// <summary>
/// Estados del ciclo de vida de un DTE.
/// </summary>
public static class DteEstadoCodigos
{
    public const string Borrador = "BORRADOR";
    public const string Generado = "GENERADO";
    public const string Validado = "VALIDADO";
    public const string Firmado = "FIRMADO";
    public const string Enviado = "ENVIADO";
    public const string Procesado = "PROCESADO";
    public const string Rechazado = "RECHAZADO";
    public const string Contingencia = "CONTINGENCIA";
    public const string Invalidado = "INVALIDADO";
    public const string Error = "ERROR";
}

/// <summary>Modelo de facturación según Hacienda.</summary>
public static class DteModeloFacturacion
{
    public const int Previo = 1;
    public const int Diferido = 2;
}

/// <summary>Tipo de transmisión.</summary>
public static class DteTipoTransmision
{
    public const int Normal = 1;
    public const int Contingencia = 2;
}
