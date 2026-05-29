using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Application.Dte;

/// <summary>
/// Genera el JSON DTE según el esquema oficial de Hacienda El Salvador.
/// </summary>
public interface IDteGeneratorService
{
    /// <summary>Construye el JSON sin firmar para el documento (validándolo previamente).</summary>
    /// <param name="documento">Documento DTE con detalles, empresa y receptor cargados.</param>
    /// <param name="config">Configuración DTE de la empresa (para inyectar codEstable/codPuntoVenta/tipoEstablecimiento en el bloque emisor). Opcional para compatibilidad.</param>
    Result<string> Generar(DteDocumento documento, DteConfiguracion? config = null);
}
