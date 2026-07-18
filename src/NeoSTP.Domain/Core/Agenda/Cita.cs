using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Agenda;

/// <summary>
/// Cita de agenda (NEOAGENDA — salones, clínicas, servicios). Asocia cliente,
/// empleado que atiende y servicio (producto tipo SERVICIO) con precio congelado
/// al agendar; la comisión del empleado se calcula sobre citas completadas.
/// </summary>
public class Cita : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? ClienteId { get; set; }
    /// <summary>Nombre visible del cliente (snapshot o texto libre para walk-in).</summary>
    public string ClienteNombre { get; set; } = null!;

    public int? EmpleadoId { get; set; }
    public string? EmpleadoNombre { get; set; }

    public int? ServicioProductoId { get; set; }
    public string ServicioNombre { get; set; } = null!;
    /// <summary>Precio del servicio congelado al agendar.</summary>
    public decimal Precio { get; set; }

    public DateTime FechaInicio { get; set; }
    public int DuracionMinutos { get; set; } = 30;
    public DateTime FechaFin => FechaInicio.AddMinutes(DuracionMinutos);

    /// <summary>PROGRAMADA | CONFIRMADA | COMPLETADA | CANCELADA | NO_ASISTIO.</summary>
    public string EstadoCodigo { get; set; } = CitaEstados.Programada;

    public string? Nota { get; set; }
}

public static class CitaEstados
{
    public const string Programada = "PROGRAMADA";
    public const string Confirmada = "CONFIRMADA";
    public const string Completada = "COMPLETADA";
    public const string Cancelada = "CANCELADA";
    public const string NoAsistio = "NO_ASISTIO";

    public static readonly string[] All = [Programada, Confirmada, Completada, Cancelada, NoAsistio];

    /// <summary>Estados que ocupan la franja del empleado (bloquean traslapes).</summary>
    public static readonly string[] Activos = [Programada, Confirmada];
}
