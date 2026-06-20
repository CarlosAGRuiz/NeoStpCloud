using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Rrhh;

/// <summary>Politica tenant de vacaciones y aguinaldo; defaults basados en normativa ES.</summary>
public class PoliticaPrestaciones : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public DateOnly VigenteDesde { get; set; }
    public int MesesParaVacacion { get; set; } = 12;
    public int DiasVacacionAnuales { get; set; } = 15;
    public decimal PrimaVacacionPorcentaje { get; set; } = 0.30m;

    public int AguinaldoAniosTramoMedio { get; set; } = 3;
    public int AguinaldoAniosTramoLargo { get; set; } = 10;
    public decimal AguinaldoDiasTramoCorto { get; set; } = 15m;
    public decimal AguinaldoDiasTramoMedio { get; set; } = 19m;
    public decimal AguinaldoDiasTramoLargo { get; set; } = 21m;
    public int AguinaldoMesPago { get; set; } = 12;
    public int AguinaldoDiaPago { get; set; } = 12;
}

public class SolicitudVacacion : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public int Dias { get; set; }
    public decimal PrimaMonto { get; set; }
    public string EstadoCodigo { get; set; } = VacacionEstados.Solicitada;
    public string? Motivo { get; set; }
    public string? ResolucionNota { get; set; }
    public DateTime? ResueltaAt { get; set; }
    public string? ResueltaPor { get; set; }

    public int? PlanillaPeriodoId { get; set; }
    public PlanillaPeriodo? PlanillaPeriodo { get; set; }
}

public class AguinaldoCalculo : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;
    public int Anio { get; set; }
    public DateOnly FechaCorte { get; set; }
    public int AntiguedadAnios { get; set; }
    public decimal SalarioMensual { get; set; }
    public decimal DiasCalculados { get; set; }
    public decimal Monto { get; set; }
    public string EstadoCodigo { get; set; } = AguinaldoEstados.Calculado;

    public int? PlanillaPeriodoId { get; set; }
    public PlanillaPeriodo? PlanillaPeriodo { get; set; }
}

public static class VacacionEstados
{
    public const string Solicitada = "SOLICITADA";
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";
    public const string Cancelada = "CANCELADA";
    public static readonly string[] All = [Solicitada, Aprobada, Rechazada, Cancelada];
}

public static class AguinaldoEstados
{
    public const string Calculado = "CALCULADO";
    public const string Aprobado = "APROBADO";
    public const string Pagado = "PAGADO";
    public const string Anulado = "ANULADO";
    public static readonly string[] All = [Calculado, Aprobado, Pagado, Anulado];
}
