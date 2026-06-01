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
}

public class RetransmisionContingenciaOptions
{
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
    /// <summary>Intervalo entre ejecuciones del job (minutos). Default: 10.</summary>
    public int IntervaloMinutos { get; set; } = 10;
}
