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
    public int CotizacionesAbiertas { get; set; }
}

// ── Cotizaciones ──────────────────────────────────────────────────────────────

public class CotizacionCrmDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public int? OportunidadCrmId { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public int? ContactoCrmId { get; set; }
    public string? ContactoNombre { get; set; }
    public DateOnly FechaEmision { get; set; }
    public DateOnly? FechaValidez { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string MonedaCodigo { get; set; } = "USD";
    public decimal SubTotal { get; set; }
    public decimal DescuentoTotal { get; set; }
    public decimal IvaTotal { get; set; }
    public decimal Total { get; set; }
    public int? DteDocumentoId { get; set; }
    public int Items { get; set; }
}

public class CotizacionCrmDetalleDto : CotizacionCrmDto
{
    public string? Observaciones { get; set; }
    public string? Terminos { get; set; }
    public List<CotizacionCrmLineaDto> Lineas { get; set; } = [];
}

public class CotizacionCrmLineaDto
{
    public int Id { get; set; }
    public int NumeroLinea { get; set; }
    public int? ProductoId { get; set; }
    public string? Codigo { get; set; }
    public string Descripcion { get; set; } = null!;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal MontoDescuento { get; set; }
    public decimal IvaItem { get; set; }
    public decimal TotalLinea { get; set; }
    public bool AplicaIva { get; set; }
}

public class CrearCotizacionCrmRequest
{
    public int? OportunidadCrmId { get; set; }
    public int? ClienteId { get; set; }
    public int? ContactoCrmId { get; set; }
    [Required, StringLength(160)] public string Titulo { get; set; } = null!;
    public DateOnly? FechaValidez { get; set; }
    [StringLength(1000)] public string? Observaciones { get; set; }
    [StringLength(1000)] public string? Terminos { get; set; }
    public List<CrearCotizacionCrmLineaRequest> Lineas { get; set; } = new();
}

/// <summary>Línea de cotización. Precios CON IVA incluido (mapean directo a FC 01 al convertir).</summary>
public class CrearCotizacionCrmLineaRequest
{
    public int? ProductoId { get; set; }
    [StringLength(50)] public string? Codigo { get; set; }
    [StringLength(250)] public string? Descripcion { get; set; }
    [Range(0.0001, 9_999_999)] public decimal Cantidad { get; set; } = 1;
    /// <summary>Si null y hay producto, usa el precio del producto.</summary>
    public decimal? PrecioUnitario { get; set; }
    [Range(0, 9_999_999)] public decimal MontoDescuento { get; set; }
    public bool? AplicaIva { get; set; }
}

public class CambiarEstadoCotizacionRequest
{
    /// <summary>ENVIADA, ACEPTADA, RECHAZADA o ANULADA.</summary>
    [Required, StringLength(20)] public string EstadoCodigo { get; set; } = null!;
}

public class ConvertirCotizacionRequest
{
    /// <summary>01 Factura (default) o 03 CCF.</summary>
    public string TipoDteCodigo { get; set; } = "01";
    /// <summary>Override del cliente receptor (si la cotización no tiene o se quiere otro).</summary>
    public int? ClienteId { get; set; }
}
