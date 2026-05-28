using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Application.Dte.Abstractions;

/// <summary>
/// Renderiza la representación gráfica (PDF) de un DTE.
///
/// Sigue el formato de la representación gráfica que exige Hacienda El Salvador
/// para los Documentos Tributarios Electrónicos: encabezado del emisor, datos
/// del receptor, líneas de detalle, totales y sello recibido cuando aplica.
/// </summary>
public interface IDtePdfService
{
    /// <summary>Genera el PDF del DTE y devuelve los bytes.</summary>
    byte[] Generar(DteDocumento documento);
}
