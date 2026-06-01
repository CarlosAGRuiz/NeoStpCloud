namespace NeoSTP.Domain.Core.Dte.Eventos;

/// <summary>
/// Estados del ciclo de vida de un evento DTE. Análogo a DteEstadoCodigos
/// pero específico al evento: un evento no pasa por VALIDADO/CONTINGENCIA
/// (esas son etapas del DTE original).
/// </summary>
public static class DteEventoEstadoCodigos
{
    /// <summary>Persistido localmente, sin firmar todavía.</summary>
    public const string Borrador = "BORRADOR";

    /// <summary>JWS RS512 generado y guardado, listo para transmitir.</summary>
    public const string Firmado = "FIRMADO";

    /// <summary>Transmitido a Hacienda, esperando respuesta.</summary>
    public const string Enviado = "ENVIADO";

    /// <summary>Hacienda devolvió PROCESADO con sello.</summary>
    public const string Procesado = "PROCESADO";

    /// <summary>Hacienda devolvió RECHAZADO.</summary>
    public const string Rechazado = "RECHAZADO";

    /// <summary>Falla técnica (firma, red, etc.) — no es un rechazo MH.</summary>
    public const string Error = "ERROR";

    public static readonly string[] Todos =
    {
        Borrador, Firmado, Enviado, Procesado, Rechazado, Error,
    };
}
