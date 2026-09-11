using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Notificaciones;

/// <summary>
/// Intención durable de notificación. Se confirma en la misma transacción que el
/// cambio de negocio y se entrega de forma asíncrona con semántica al menos una vez.
/// </summary>
public sealed class NotificationOutboxMessage : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public string Tipo { get; set; } = string.Empty;
    public string Canal { get; set; } = string.Empty;
    public string? Destinatario { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string ClaveIdempotencia { get; set; } = string.Empty;

    public string Estado { get; set; } = NotificationOutboxEstados.Pending;
    public int Intentos { get; set; }
    public int MaxIntentos { get; set; } = 6;
    public DateTime DisponibleDesde { get; set; } = DateTime.UtcNow;
    public DateTime? ProcesadoAt { get; set; }
    public string? ErrorUltimo { get; set; }

    public string? LeaseId { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public static class NotificationOutboxEstados
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Sent = "SENT";
    public const string Failed = "FAILED";
    public const string Dead = "DEAD";
}

public static class NotificationOutboxCanales
{
    public const string Email = "EMAIL";
    public const string WhatsApp = "WHATSAPP";
    public const string Push = "PUSH";
    public const string Webhook = "WEBHOOK";
}

public static class NotificationOutboxTipos
{
    public const string AlertaCreada = "ALERTA_CREADA";
}
