using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Connect;

public class ConnectWebhookDelivery : AuditableEntity
{
    public int WebhookId { get; set; }
    public int EmpresaId { get; set; }

    public string Evento { get; set; } = null!;

    /// <summary>JSON del payload enviado al endpoint del cliente.</summary>
    public string Payload { get; set; } = null!;

    /// <summary>PENDIENTE | ENTREGADO | FALLIDO</summary>
    public string Estado { get; set; } = ConnectDeliveryEstados.Pendiente;

    public int? HttpStatus { get; set; }
    public int Intentos { get; set; } = 0;
    public DateTime? ProximoIntento { get; set; }
    public DateTime? EntregadoAt { get; set; }

    /// <summary>Cuerpo o mensaje del último error de entrega.</summary>
    public string? Error { get; set; }

    public ConnectWebhook Webhook { get; set; } = null!;
}

public static class ConnectDeliveryEstados
{
    public const string Pendiente = "PENDIENTE";
    public const string Entregado = "ENTREGADO";
    public const string Fallido = "FALLIDO";
}
