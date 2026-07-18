using NeoSTP.Application.Common;

namespace NeoSTP.Application.Agenda;

public sealed class CitaDto
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = null!;
    public int? EmpleadoId { get; set; }
    public string? EmpleadoNombre { get; set; }
    public int? ServicioProductoId { get; set; }
    public string ServicioNombre { get; set; } = null!;
    public decimal Precio { get; set; }
    public DateTime FechaInicio { get; set; }
    public int DuracionMinutos { get; set; }
    public DateTime FechaFin { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? Nota { get; set; }
}

public sealed class CrearCitaRequest
{
    public int? ClienteId { get; set; }
    /// <summary>Obligatorio si no hay ClienteId (walk-in).</summary>
    public string? ClienteNombre { get; set; }
    public int? EmpleadoId { get; set; }
    public int? ServicioProductoId { get; set; }
    /// <summary>Obligatorio si no hay ServicioProductoId.</summary>
    public string? ServicioNombre { get; set; }
    /// <summary>Si null, se toma el precio del producto servicio (o 0).</summary>
    public decimal? Precio { get; set; }
    public DateTime FechaInicio { get; set; }
    public int DuracionMinutos { get; set; } = 30;
    public string? Nota { get; set; }
}

public sealed class ComisionEmpleadoDto
{
    public int EmpleadoId { get; set; }
    public string EmpleadoNombre { get; set; } = null!;
    public decimal ComisionPorcentaje { get; set; }
    public int CitasCompletadas { get; set; }
    public decimal TotalServicios { get; set; }
    public decimal MontoComision { get; set; }
}

/// <summary>
/// NEOAGENDA — citas con cliente/empleado/servicio, validación de traslapes por
/// empleado y comisiones sobre citas completadas. Aislado por EmpresaId.
/// </summary>
public interface IAgendaService
{
    Task<Result<IReadOnlyList<CitaDto>>> ListAsync(int empresaId, DateTime desde, DateTime hasta, int? empleadoId = null, CancellationToken ct = default);
    Task<Result<CitaDto>> CrearAsync(int empresaId, CrearCitaRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CitaDto>> ReprogramarAsync(int empresaId, int id, DateTime nuevaFechaInicio, int? duracionMinutos, string? actor, CancellationToken ct = default);
    Task<Result<CitaDto>> CambiarEstadoAsync(int empresaId, int id, string estado, string? actor, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ComisionEmpleadoDto>>> ComisionesAsync(int empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}
