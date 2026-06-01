namespace NeoSTP.Application.Dte.Contingencia.Dtos;

// ─── Resumen de la cola de contingencia ──────────────────────────────────────

/// <summary>Resumen de la situación de contingencia de la empresa.</summary>
public class ContingenciaResumenDto
{
    /// <summary>Documentos en estado CONTINGENCIA que aún no tienen lote.</summary>
    public int DocumentosPendientes { get; set; }
    /// <summary>Lotes en estado PENDIENTE o ENVIADO esperando consulta.</summary>
    public int LotesPendientes { get; set; }
    /// <summary>Documentos cuyo lote ya quedó PROCESADO (con sello individual).</summary>
    public int DocumentosProcesados { get; set; }
    /// <summary>Documentos en lotes con estado ERROR.</summary>
    public int DocumentosConError { get; set; }
    /// <summary>Fecha de vencimiento de la ventana de 72 h del lote más antiguo aún pendiente. Null si no hay lotes activos.</summary>
    public DateTime? VencimientoLoteMasAntiguo { get; set; }
    /// <summary>Id del evento de contingencia PROCESADO más reciente sin lote creado (o null).</summary>
    public int? EventoSinLoteId { get; set; }
}

// ─── Documentos en cola ───────────────────────────────────────────────────────

/// <summary>Ítem de la lista de DTE en cola de contingencia.</summary>
public class ContingenciaDocumentoDto
{
    public int Id { get; set; }
    public string TipoDteCodigo { get; set; } = null!;
    public string NumeroControl { get; set; } = null!;
    public string CodigoGeneracion { get; set; } = null!;
    public DateTime FechaEmision { get; set; }
    public string? ReceptorNombre { get; set; }
    public decimal TotalPagar { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public int IntentoRetransmision { get; set; }
    public DateTime? UltimoIntentoAt { get; set; }
    /// <summary>Horas transcurridas desde la emisión. Límite: 24 h para el evento, 72 h para el lote.</summary>
    public double HorasDesdeEmision { get; set; }
}

// ─── Lotes ────────────────────────────────────────────────────────────────────

/// <summary>Ítem de lista de lotes de contingencia.</summary>
public class ContingenciaLoteListItemDto
{
    public int Id { get; set; }
    public int EventoContingenciaId { get; set; }
    public string? CodigoLote { get; set; }
    public string? SelloRecibido { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string AmbienteCodigo { get; set; } = null!;
    public int TotalDte { get; set; }
    public int DteProcesados { get; set; }
    public DateTime? EnviadoAt { get; set; }
    public DateTime? UltimaConsultaAt { get; set; }
    public int Intentos { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Detalle completo de un lote.</summary>
public class ContingenciaLoteDto : ContingenciaLoteListItemDto
{
    public IReadOnlyList<ContingenciaLoteDetalleDto> Detalles { get; set; } = Array.Empty<ContingenciaLoteDetalleDto>();
    public string? RawEnvio { get; set; }
    public string? RawConsulta { get; set; }
}

/// <summary>Detalle de un DTE dentro del lote.</summary>
public class ContingenciaLoteDetalleDto
{
    public int Id { get; set; }
    public int DteDocumentoId { get; set; }
    public string CodigoGeneracion { get; set; } = null!;
    public string TipoDteCodigo { get; set; } = null!;
    public string? SelloRecibido { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? MensajeHacienda { get; set; }
}

// ─── Comandos ─────────────────────────────────────────────────────────────────

/// <summary>Request para crear un lote a partir de un evento de contingencia PROCESADO.</summary>
public class CrearLoteDesdeEventoRequest
{
    /// <summary>Id del DteEvento de contingencia con sello (PROCESADO).</summary>
    public int EventoContingenciaId { get; set; }
}

/// <summary>Resultado de la creación de un lote.</summary>
public class CrearLoteResultadoDto
{
    public int LoteId { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? CodigoLote { get; set; }
    public string? SelloRecibido { get; set; }
    public string? Mensaje { get; set; }
}

/// <summary>Resultado de la consulta de estado de un lote.</summary>
public class ConsultarLoteResultadoDto
{
    public int LoteId { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? CodigoLote { get; set; }
    public int DteProcesados { get; set; }
    public int DtePendientes { get; set; }
    public string? Mensaje { get; set; }
}
