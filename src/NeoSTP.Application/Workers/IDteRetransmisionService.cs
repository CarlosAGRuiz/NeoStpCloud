namespace NeoSTP.Application.Workers;

/// <summary>
/// Servicio de retransmisión automática de DTE en estado CONTINGENCIA.
/// Ejecutado periódicamente por <c>RetransmisionContingenciaWorker</c>.
/// </summary>
public interface IDteRetransmisionService
{
    /// <summary>
    /// Busca documentos en CONTINGENCIA elegibles para retransmisión y los reenvía a Hacienda.
    /// </summary>
    /// <returns>Número de documentos retransmitidos exitosamente.</returns>
    Task<RetransmisionResultado> RetransmitirContingenciasAsync(CancellationToken ct = default);
}

public class RetransmisionResultado
{
    public int Procesados { get; set; }
    public int Exitosos { get; set; }
    public int Fallidos { get; set; }
    public int Omitidos { get; set; }
    public List<string> Detalles { get; set; } = [];
}
