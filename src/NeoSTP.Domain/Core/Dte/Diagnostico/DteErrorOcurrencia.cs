using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Diagnostico;

/// <summary>
/// Registro de una ocurrencia concreta de un error, asociada a un documento DTE
/// o a un evento DTE de una empresa específica.
/// </summary>
public class DteErrorOcurrencia : AuditableEntity
{
    public int EmpresaId { get; set; }

    /// <summary>Código del error (referencia al catálogo, puede no existir en él).</summary>
    public string CodigoError { get; set; } = null!;

    /// <summary>DTE al que pertenece la ocurrencia (opcional).</summary>
    public int? DteDocumentoId { get; set; }

    /// <summary>Evento DTE al que pertenece (opcional).</summary>
    public int? DteEventoId { get; set; }

    /// <summary>Mensaje exacto recibido de Hacienda o generado internamente.</summary>
    public string Mensaje { get; set; } = null!;

    /// <summary>Respuesta cruda de Hacienda (JSON).</summary>
    public string? RespuestaMhJson { get; set; }

    /// <summary>JSON enviado a Hacienda en el momento del error.</summary>
    public string? JsonEnviado { get; set; }

    /// <summary>Fuente: TRANSMISION | CERTIFICACION | EVENTO | INTERNO</summary>
    public string Fuente { get; set; } = DteErrorFuente.Transmision;

    public DateTime OcurrioAt { get; set; } = DateTime.UtcNow;

    public bool Resuelta { get; set; } = false;
}

public static class DteErrorFuente
{
    public const string Transmision = "TRANSMISION";
    public const string Certificacion = "CERTIFICACION";
    public const string Evento = "EVENTO";
    public const string Interno = "INTERNO";
}
