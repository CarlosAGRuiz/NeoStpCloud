using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Crm.Dtos;

public class ContactoCrmDto
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Cargo { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string Origen { get; set; } = null!;
    public string EstadoCodigo { get; set; } = null!;
    public string? Notas { get; set; }
}

public class UpsertContactoCrmRequest
{
    public int? ClienteId { get; set; }
    [Required, StringLength(160)] public string Nombre { get; set; } = null!;
    [StringLength(100)] public string? Cargo { get; set; }
    [StringLength(160)] public string? Email { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(20)] public string Origen { get; set; } = "MANUAL";
    [StringLength(500)] public string? Notas { get; set; }
}

public class EtapaPipelineCrmDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }
    public decimal ProbabilidadDefault { get; set; }
    public bool Activa { get; set; }
    public bool EsCierreGanado { get; set; }
    public bool EsCierrePerdido { get; set; }
}

public class UpsertEtapaPipelineCrmRequest
{
    [Required, StringLength(30)] public string Codigo { get; set; } = null!;
    [Required, StringLength(80)] public string Nombre { get; set; } = null!;
    [Range(1, 999)] public int Orden { get; set; }
    [Range(0, 100)] public decimal ProbabilidadDefault { get; set; }
    public bool Activa { get; set; } = true;
    public bool EsCierreGanado { get; set; }
    public bool EsCierrePerdido { get; set; }
}

public class OportunidadCrmDto
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public int? ContactoCrmId { get; set; }
    public string? ContactoNombre { get; set; }
    public int EtapaPipelineCrmId { get; set; }
    public string EtapaCodigo { get; set; } = null!;
    public string EtapaNombre { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public decimal MontoEstimado { get; set; }
    public decimal Probabilidad { get; set; }
    public DateOnly FechaApertura { get; set; }
    public DateOnly? FechaCierreEstimada { get; set; }
    public DateOnly? FechaCierreReal { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? MotivoPerdida { get; set; }
    public int? DteDocumentoId { get; set; }
    public int? CuentaCobroId { get; set; }
    public int ActividadesPendientes { get; set; }
}

public class OportunidadCrmDetalleDto : OportunidadCrmDto
{
    public List<ActividadCrmDto> Actividades { get; set; } = [];
}

public class CrearOportunidadCrmRequest
{
    public int? ClienteId { get; set; }
    public int? ContactoCrmId { get; set; }
    public int? EtapaPipelineCrmId { get; set; }
    [Required, StringLength(160)] public string Titulo { get; set; } = null!;
    [StringLength(1000)] public string? Descripcion { get; set; }
    [Range(0, 99_999_999)] public decimal MontoEstimado { get; set; }
    [Range(0, 100)] public decimal? Probabilidad { get; set; }
    public DateOnly? FechaCierreEstimada { get; set; }
}

public class ActualizarOportunidadCrmRequest : CrearOportunidadCrmRequest
{
    [Required] public string EstadoCodigo { get; set; } = "ABIERTA";
    [StringLength(250)] public string? MotivoPerdida { get; set; }
    public int? DteDocumentoId { get; set; }
    public int? CuentaCobroId { get; set; }
}

public class CambiarEtapaOportunidadRequest
{
    [Required] public int EtapaPipelineCrmId { get; set; }
    [Range(0, 100)] public decimal? Probabilidad { get; set; }
    [StringLength(250)] public string? MotivoPerdida { get; set; }
    public int? DteDocumentoId { get; set; }
    public int? CuentaCobroId { get; set; }
}

public class ActividadCrmDto
{
    public int Id { get; set; }
    public int? OportunidadCrmId { get; set; }
    public int? ContactoCrmId { get; set; }
    public int? ClienteId { get; set; }
    public string Tipo { get; set; } = null!;
    public string Asunto { get; set; } = null!;
    public string? Descripcion { get; set; }
    public DateTime FechaProgramada { get; set; }
    public DateTime? FechaRealizada { get; set; }
    public DateTime? RecordatorioAt { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? Resultado { get; set; }
}

public class CrearActividadCrmRequest
{
    public int? OportunidadCrmId { get; set; }
    public int? ContactoCrmId { get; set; }
    public int? ClienteId { get; set; }
    [Required, StringLength(20)] public string Tipo { get; set; } = "NOTA";
    [Required, StringLength(160)] public string Asunto { get; set; } = null!;
    [StringLength(1000)] public string? Descripcion { get; set; }
    public DateTime? FechaProgramada { get; set; }
    public DateTime? RecordatorioAt { get; set; }
}

public class CompletarActividadCrmRequest
{
    [StringLength(1000)] public string? Resultado { get; set; }
}

public class CrmResumenDto
{
    public int ContactosActivos { get; set; }
    public int OportunidadesAbiertas { get; set; }
    public decimal PipelineAbierto { get; set; }
    public decimal PipelinePonderado { get; set; }
    public int ActividadesPendientes { get; set; }
    public int ActividadesVencidas { get; set; }
}
