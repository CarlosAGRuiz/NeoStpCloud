using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Rrhh.Dtos;

public class PoliticaPrestacionesDto
{
    public DateOnly VigenteDesde { get; set; }
    public int MesesParaVacacion { get; set; }
    public int DiasVacacionAnuales { get; set; }
    public decimal PrimaVacacionPorcentaje { get; set; }
    public int AguinaldoAniosTramoMedio { get; set; }
    public int AguinaldoAniosTramoLargo { get; set; }
    public decimal AguinaldoDiasTramoCorto { get; set; }
    public decimal AguinaldoDiasTramoMedio { get; set; }
    public decimal AguinaldoDiasTramoLargo { get; set; }
    public int AguinaldoMesPago { get; set; }
    public int AguinaldoDiaPago { get; set; }
}

public class UpdatePoliticaPrestacionesRequest
{
    public DateOnly? VigenteDesde { get; set; }
    [Range(1, 60)] public int MesesParaVacacion { get; set; } = 12;
    [Range(1, 60)] public int DiasVacacionAnuales { get; set; } = 15;
    [Range(0, 2)] public decimal PrimaVacacionPorcentaje { get; set; } = 0.30m;
    [Range(1, 50)] public int AguinaldoAniosTramoMedio { get; set; } = 3;
    [Range(2, 60)] public int AguinaldoAniosTramoLargo { get; set; } = 10;
    [Range(1, 60)] public decimal AguinaldoDiasTramoCorto { get; set; } = 15m;
    [Range(1, 60)] public decimal AguinaldoDiasTramoMedio { get; set; } = 19m;
    [Range(1, 60)] public decimal AguinaldoDiasTramoLargo { get; set; } = 21m;
    [Range(1, 12)] public int AguinaldoMesPago { get; set; } = 12;
    [Range(1, 31)] public int AguinaldoDiaPago { get; set; } = 12;
}

public class VacacionResumenEmpleadoDto
{
    public int EmpleadoId { get; set; }
    public string EmpleadoCodigo { get; set; } = null!;
    public string EmpleadoNombre { get; set; } = null!;
    public DateOnly FechaIngreso { get; set; }
    public DateOnly FechaCorte { get; set; }
    public int DiasDevengados { get; set; }
    public int DiasAprobados { get; set; }
    public int DiasDisponibles { get; set; }
}

public class SolicitudVacacionDto
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string EmpleadoCodigo { get; set; } = null!;
    public string EmpleadoNombre { get; set; } = null!;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public int Dias { get; set; }
    public decimal PrimaMonto { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? Motivo { get; set; }
    public string? ResolucionNota { get; set; }
    public int? PlanillaPeriodoId { get; set; }
}

public class CrearSolicitudVacacionRequest
{
    [Required] public int EmpleadoId { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    [StringLength(500)] public string? Motivo { get; set; }
}

public class ResolverSolicitudVacacionRequest
{
    [StringLength(500)] public string? Nota { get; set; }
}

public class AguinaldoCalculoDto
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string EmpleadoCodigo { get; set; } = null!;
    public string EmpleadoNombre { get; set; } = null!;
    public int Anio { get; set; }
    public DateOnly FechaCorte { get; set; }
    public int AntiguedadAnios { get; set; }
    public decimal SalarioMensual { get; set; }
    public decimal DiasCalculados { get; set; }
    public decimal Monto { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public int? PlanillaPeriodoId { get; set; }
}
