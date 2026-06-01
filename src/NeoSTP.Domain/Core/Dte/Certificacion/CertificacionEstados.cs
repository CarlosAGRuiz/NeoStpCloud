namespace NeoSTP.Domain.Core.Dte.Certificacion;

/// <summary>
/// Estados de cada prueba/intento de certificación contra apitest de Hacienda.
/// Una prueba es la asociación entre un escenario de la matriz y un DteDocumento
/// emitido (o intentado) por la empresa.
/// </summary>
public static class CertificacionEstadoCodigos
{
    /// <summary>El escenario aún no se ha intentado.</summary>
    public const string Pendiente = "PENDIENTE";

    /// <summary>Hay un DTE asociado pero no se ha procesado por Hacienda.</summary>
    public const string EnProgreso = "EN_PROGRESO";

    /// <summary>Hacienda devolvió PROCESADO con sello. El escenario cuenta como cubierto.</summary>
    public const string Completado = "COMPLETADO";

    /// <summary>Hacienda devolvió RECHAZADO o el intento falló. Hay que reintentar.</summary>
    public const string Error = "ERROR";

    public static readonly string[] Todos = { Pendiente, EnProgreso, Completado, Error };
}
