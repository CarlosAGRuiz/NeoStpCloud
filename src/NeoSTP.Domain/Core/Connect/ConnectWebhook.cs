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
    // ── Facturación electrónica ───────────────────────────────────────────
    public const string DteProcesado = "DTE.Procesado";
    public const string DteRechazado = "DTE.Rechazado";
    public const string DteContingencia = "DTE.Contingencia";
    public const string DteInvalidado = "DTE.Invalidado";

    // ── Negocio (E6) ──────────────────────────────────────────────────────
    // El integrador no solo quiere saber de la factura: quiere reaccionar cuando
    // le pagan, cuando hay que autorizar una compra o cuando se le acaba el producto.
    public const string CobroPagoConfirmado = "Cobros.PagoConfirmado";
    public const string CompraOrdenPorAprobar = "Compras.OrdenPorAprobar";
    public const string InventarioStockBajo = "Inventario.StockBajo";
    public const string AgendaCitaCreada = "Agenda.CitaCreada";

    public static readonly string[] All =
    [
        DteProcesado, DteRechazado, DteContingencia, DteInvalidado,
        CobroPagoConfirmado, CompraOrdenPorAprobar, InventarioStockBajo, AgendaCitaCreada,
    ];

    /// <summary>Eventos de negocio: los que no nacen del ciclo de vida de un DTE.</summary>
    public static readonly string[] Negocio =
    [
        CobroPagoConfirmado, CompraOrdenPorAprobar, InventarioStockBajo, AgendaCitaCreada,
    ];

    /// <summary>Descripción para la UI de suscripción de webhooks.</summary>
    public static string Describir(string evento) => evento switch
    {
        DteProcesado => "Un documento fue aceptado por Hacienda",
        DteRechazado => "Hacienda rechazó un documento",
        DteContingencia => "Un documento quedó en contingencia",
        DteInvalidado => "Un documento fue invalidado",
        CobroPagoConfirmado => "Se confirmó el pago de una factura",
        CompraOrdenPorAprobar => "Una orden de compra superó el umbral y espera aprobación",
        InventarioStockBajo => "Un producto llegó a su stock mínimo",
        AgendaCitaCreada => "Se agendó una cita",
        _ => evento,
    };
}
