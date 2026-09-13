namespace NeoSTP.Application.Workers;

/// <summary>
/// Configuración de los jobs periódicos del Worker.
/// Sección en appsettings: "Worker".
/// </summary>
public class WorkerOptions
{
    public const string SectionName = "Worker";

    public RetransmisionContingenciaOptions RetransmisionContingencia { get; set; } = new();
    public LimpiezaTokensOptions LimpiezaTokens { get; set; } = new();
    public ContingenciaLoteOptions ContingenciaLote { get; set; } = new();
    public WebhookDeliveryOptions WebhookDelivery { get; set; } = new();
    public BillingProviderOperationOptions BillingProviderOperations { get; set; } = new();
    public NotificationOutboxOptions NotificationOutbox { get; set; } = new();
    public GeneracionAlertasOptions GeneracionAlertas { get; set; } = new();
    public RecordatoriosCobroOptions RecordatoriosCobro { get; set; } = new();
    public LimpiezaAuditoriaOptions LimpiezaAuditoria { get; set; } = new();
}

/// <summary>V2.5-S5 — purga programada de auditoría por retención.</summary>
public class LimpiezaAuditoriaOptions
{
    public bool Enabled { get; set; }

    /// <summary>Eventos de auditoría más viejos que esto se purgan. Default: 365 días.</summary>
    public int RetencionDias { get; set; } = 365;

    /// <summary>Intervalo entre ejecuciones del job (horas). Default: 24.</summary>
    public int IntervaloHoras { get; set; } = 24;

    /// <summary>Filas por lote de borrado (evita bloqueos largos). Default: 5000.</summary>
    public int BatchSize { get; set; } = 5000;
}

public class GeneracionAlertasOptions
{
    public bool Enabled { get; set; }

    /// <summary>Intervalo entre ejecuciones del job (minutos). Default: 60.</summary>
    public int IntervaloMinutos { get; set; } = 60;
}

public class RecordatoriosCobroOptions
{
    public bool Enabled { get; set; }
    /// <summary>Intervalo entre ejecuciones del job (horas). Default: 24.</summary>
    public int IntervaloHoras { get; set; } = 24;
    public int DiasVencidoMinimo { get; set; } = 1;
    public int MaximoPorEmpresa { get; set; } = 50;
    public bool EnviarEmail { get; set; } = true;
    public bool EnviarWhatsApp { get; set; }
}

public class RetransmisionContingenciaOptions
{
    public bool Enabled { get; set; }

    /// <summary>Intervalo entre ejecuciones del job (minutos). Default: 5.</summary>
    public int IntervaloMinutos { get; set; } = 5;

    /// <summary>Tiempo mínimo entre reintentos de un mismo documento (minutos). Default: 30.</summary>
    public int CooldownMinutos { get; set; } = 30;

    /// <summary>Máximo de reintentos automáticos por documento antes de desistir. Default: 5.</summary>
    public int MaxIntentos { get; set; } = 5;

    /// <summary>Máximo de documentos procesados por ejecución. Default: 50.</summary>
    public int LoteMaximo { get; set; } = 50;
}

public class LimpiezaTokensOptions
{
    public bool Enabled { get; set; }

    /// <summary>Intervalo entre ejecuciones del job (horas). Default: 24.</summary>
    public int IntervaloHoras { get; set; } = 24;

    /// <summary>
    /// Días de retención tras expiración/revocación antes de borrar un token.
    /// Default: 30 días.
    /// </summary>
    public int RetentionDias { get; set; } = 30;
}

public class ContingenciaLoteOptions
{
    public bool Enabled { get; set; }

    /// <summary>Intervalo entre ejecuciones del job (minutos). Default: 10.</summary>
    public int IntervaloMinutos { get; set; } = 10;
}

public class WebhookDeliveryOptions
{
    public bool Enabled { get; set; }

    /// <summary>Intervalo entre ejecuciones del job (segundos). Default: 30.</summary>
    public int IntervaloSegundos { get; set; } = 30;
}

public class BillingProviderOperationOptions
{
    /// <summary>Permanece deshabilitado hasta configurar y validar proveedores reales.</summary>
    public bool Enabled { get; set; }

    /// <summary>Intervalo entre búsquedas de operaciones pendientes. Default: 15 segundos.</summary>
    public int IntervaloSegundos { get; set; } = 15;
}

public class NotificationOutboxOptions
{
    /// <summary>Activa el dispatcher durable de notificaciones.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Intervalo entre búsquedas de mensajes pendientes. Default: 5 segundos.</summary>
    public int IntervaloSegundos { get; set; } = 5;

    /// <summary>Máximo de mensajes considerados por ciclo.</summary>
    public int LoteMaximo { get; set; } = 50;

    /// <summary>Tiempo máximo reservado a una instancia antes de permitir recuperación.</summary>
    public int LeaseSegundos { get; set; } = 120;

    /// <summary>Días de auditoría conservados para mensajes enviados.</summary>
    public int RetencionDias { get; set; } = 90;
}
