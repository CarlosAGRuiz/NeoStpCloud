using NeoSTP.Application.Common;

namespace NeoSTP.Application.Dte;

public static class DteCorreoFinalidades
{
    public const string Receptor = "RECEPTOR";
    public const string Emisor = "EMISOR";
    public const string Ambos = "AMBOS";
}

public static class DteCorreoEstados
{
    public const string NoAplica = "NO_APLICA";
    public const string SinRegistro = "SIN_REGISTRO";
    public const string Pendiente = "PENDIENTE";
    public const string Enviando = "ENVIANDO";
    public const string Enviado = "ENVIADO";
    public const string Reintentando = "REINTENTANDO";
    public const string Fallido = "FALLIDO";
}

public sealed class DteCorreoEntregaDto
{
    public int? OutboxId { get; set; }
    public string Finalidad { get; set; } = string.Empty;
    public string Estado { get; set; } = DteCorreoEstados.SinRegistro;
    public string? Destinatario { get; set; }
    public bool Automatico { get; set; }
    public int Intentos { get; set; }
    public int MaxIntentos { get; set; }
    public DateTime? ProximoIntentoAt { get; set; }
    public DateTime? EnviadoAt { get; set; }
    public string? Error { get; set; }
    public string? MessageId { get; set; }
    public bool Reintentable { get; set; }
}

public sealed class DteCorreoEstadoDto
{
    public int DteDocumentoId { get; set; }
    public string EstadoGeneral { get; set; } = DteCorreoEstados.SinRegistro;
    public DteCorreoEntregaDto Receptor { get; set; } = new() { Finalidad = DteCorreoFinalidades.Receptor };
    public DteCorreoEntregaDto Emisor { get; set; } = new() { Finalidad = DteCorreoFinalidades.Emisor };
    public IReadOnlyList<DteCorreoEntregaDto> Historial { get; set; } = [];
}

public interface IDteCorreoEntregaService
{
    Task<Result<DteCorreoEstadoDto>> GetEstadoAsync(
        int empresaId,
        int dteDocumentoId,
        CancellationToken ct = default);

    Task<Result<DteCorreoEstadoDto>> ReencolarAsync(
        int empresaId,
        int dteDocumentoId,
        string finalidad,
        string idempotencyKey,
        string? actor,
        CancellationToken ct = default);
}
