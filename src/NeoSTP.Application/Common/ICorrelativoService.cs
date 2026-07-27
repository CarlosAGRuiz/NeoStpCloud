namespace NeoSTP.Application.Common;

/// <summary>
/// Numeración correlativa de documentos internos (órdenes de compra, recepciones).
/// Sustituye los números con fragmento de GUID, que eran ilegibles para el proveedor
/// y no daban idea de secuencia.
/// </summary>
public interface ICorrelativoService
{
    /// <summary>
    /// Entrega el siguiente número de la serie ya formateado: <c>OC-2026-000042</c>.
    /// El contador es atómico y reinicia cada año.
    /// </summary>
    /// <param name="prefijo">Prefijo de la serie (ver <c>CorrelativoSeries</c>).</param>
    Task<string> SiguienteAsync(int empresaId, string prefijo, CancellationToken ct = default);
}
