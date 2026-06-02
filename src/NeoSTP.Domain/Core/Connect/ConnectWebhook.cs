using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Connect;

public class ConnectWebhook : AuditableEntity
{
    public int EmpresaId { get; set; }
    public int? ApiKeyId { get; set; }

    public string Url { get; set; } = null!;

    /// <summary>Secreto HMAC-SHA256 para firmar el payload. El cliente lo usa para verificar autenticidad.</summary>
    public string SecretoHmac { get; set; } = null!;

    /// <summary>Eventos suscritos separados por coma: DTE.Procesado, DTE.Rechazado, DTE.Contingencia.</summary>
    public string Eventos { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTime? UltimaEntregaAt { get; set; }

    public ConnectApiKey? ApiKey { get; set; }
    public ICollection<ConnectWebhookDelivery> Deliveries { get; set; } = [];
}

public static class ConnectEventos
{
    public const string DteProcesado = "DTE.Procesado";
    public const string DteRechazado = "DTE.Rechazado";
    public const string DteContingencia = "DTE.Contingencia";
    public const string DteInvalidado = "DTE.Invalidado";

    public static readonly string[] All = [DteProcesado, DteRechazado, DteContingencia, DteInvalidado];
}
