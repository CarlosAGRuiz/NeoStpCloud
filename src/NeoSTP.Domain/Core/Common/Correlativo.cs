namespace NeoSTP.Domain.Core.Common;

/// <summary>
/// Contador atómico de numeración por empresa y serie, para documentos internos
/// (órdenes de compra, recepciones…). La serie incluye el año — p. ej. "OC-2026" —
/// para que la numeración reinicie cada año sin lógica extra.
///
/// No aplica a los DTE: esos llevan su propio correlativo fiscal (<c>Dte_Correlativos</c>)
/// con las reglas del Ministerio de Hacienda.
/// </summary>
public class Correlativo
{
    public int EmpresaId { get; set; }

    /// <summary>Serie del documento, con año incluido: "OC-2026", "RC-2026".</summary>
    public string Serie { get; set; } = null!;

    /// <summary>Último número entregado para esa serie.</summary>
    public int UltimoNumero { get; set; }

    public DateTime ActualizadoAt { get; set; }
}

/// <summary>Prefijos de serie de los documentos internos numerados.</summary>
public static class CorrelativoSeries
{
    /// <summary>Orden de compra.</summary>
    public const string OrdenCompra = "OC";

    /// <summary>Recepción de mercadería contra una orden de compra.</summary>
    public const string RecepcionCompra = "RC";
}
