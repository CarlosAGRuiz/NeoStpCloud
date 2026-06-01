namespace NeoSTP.Application.Dte.Eventos.Dtos;

public class DteEventoListDto
{
    public int Id { get; set; }
    public string TipoEventoCodigo { get; set; } = null!;
    public string CodigoGeneracion { get; set; } = null!;
    public string EstadoCodigo { get; set; } = null!;
    public string AmbienteCodigo { get; set; } = null!;
    public DateTime FechaTransmision { get; set; }
    public string? SelloRecibido { get; set; }
    public int DocumentosRelacionados { get; set; }
}

public class DteEventoRelacionadoDto
{
    public int DocumentoId { get; set; }
    public string RolCodigo { get; set; } = null!;
    public string? NumeroControl { get; set; }
    public string? TipoDteCodigo { get; set; }
}

public class DteEventoRespuestaDto
{
    public int Id { get; set; }
    public string? Estado { get; set; }
    public string? CodigoMsg { get; set; }
    public string? DescripcionMsg { get; set; }
    public string? SelloRecibido { get; set; }
    public string RespuestaCrudaJson { get; set; } = null!;
    public DateTime RecibidoAt { get; set; }
}

public class DteEventoDetalleDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string TipoEventoCodigo { get; set; } = null!;
    public string CodigoGeneracion { get; set; } = null!;
    public int Version { get; set; }
    public string AmbienteCodigo { get; set; } = null!;
    public string EstadoCodigo { get; set; } = null!;
    public DateTime FechaTransmision { get; set; }
    public string? SelloRecibido { get; set; }
    public string? NumeroControlReferencia { get; set; }
    public string? MotivoLibre { get; set; }
    public DateTime? FinalizadoAt { get; set; }
    public bool TieneJson { get; set; }
    public bool TieneJws { get; set; }
    public List<DteEventoRelacionadoDto> Relacionados { get; set; } = new();
    public List<DteEventoRespuestaDto> Respuestas { get; set; } = new();
}

public class DteEventoJsonDto
{
    public int EventoId { get; set; }
    public string JsonSinFirmar { get; set; } = null!;
    public string? JwsFirmado { get; set; }
}

// ----- Requests de creación (espejo de los parámetros de transmisión existentes) -----

public class CrearEventoInvalidacionRequest
{
    public int DocumentoId { get; set; }
    public int TipoAnulacion { get; set; } = 2;
    public string? MotivoAnulacion { get; set; }
    public string? CodigoGeneracionReemplazo { get; set; }
    public string NombreResponsable { get; set; } = null!;
    public string TipoDocResponsable { get; set; } = null!;
    public string NumDocResponsable { get; set; } = null!;
}

public class CrearEventoContingenciaRequest
{
    public List<int> DocumentoIds { get; set; } = new();
    public int TipoContingencia { get; set; } = 1;
    public string? Motivo { get; set; }
    public string NombreResponsable { get; set; } = null!;
    public string TipoDocResponsable { get; set; } = null!;
    public string NumeroDocResponsable { get; set; } = null!;
}

public class CrearEventoRetornoRequest
{
    public int DocumentoOrigenId { get; set; }
}

public class CrearEventoOperacionesEspecialesRequest
{
    public string? CodigoGeneracionRef { get; set; }
    public string Descripcion { get; set; } = null!;
    public decimal Monto { get; set; }
}

/// <summary>Resultado de crear un evento: sello/estado + id del evento persistido (si se persistió).</summary>
public class CrearEventoResultadoDto
{
    public string SelloOEstado { get; set; } = null!;
    public int? EventoId { get; set; }
}
