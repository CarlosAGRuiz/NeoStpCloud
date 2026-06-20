using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Rrhh;

/// <summary>
/// Corrida de planilla de un período (quincena o mes). Agrupa el detalle por empleado y
/// los totales. Al cerrarse genera un gasto de NeoProfit (categoría PLANILLA).
/// </summary>
public class PlanillaPeriodo : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int Anio { get; set; }
    public int Mes { get; set; }
    /// <summary>1 = primera quincena (1-15), 2 = segunda (16-fin), 0 = mensual.</summary>
    public int Quincena { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }

    /// <summary>BORRADOR / CALCULADA / CERRADA / ANULADA.</summary>
    public string EstadoCodigo { get; set; } = PlanillaEstados.Borrador;

    public decimal TotalDevengado { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal TotalNeto { get; set; }
    public decimal TotalCostoPatronal { get; set; }

    /// <summary>Gasto generado en NeoProfit al cerrar (categoría PLANILLA).</summary>
    public int? ProfitGastoId { get; set; }

    public ICollection<PlanillaDetalle> Detalles { get; set; } = new List<PlanillaDetalle>();
    public ICollection<SolicitudVacacion> VacacionesAplicadas { get; set; } = new List<SolicitudVacacion>();
    public ICollection<AguinaldoCalculo> AguinaldosAplicados { get; set; } = new List<AguinaldoCalculo>();
}

/// <summary>Línea de planilla por empleado (snapshot del cálculo del período).</summary>
public class PlanillaDetalle : AuditableEntity
{
    public int PlanillaPeriodoId { get; set; }
    public PlanillaPeriodo PlanillaPeriodo { get; set; } = null!;

    public int EmpleadoId { get; set; }
    public string EmpleadoCodigo { get; set; } = null!;
    public string EmpleadoNombre { get; set; } = null!;

    public decimal SalarioMensual { get; set; }
    public decimal Devengado { get; set; }
    public decimal PrimaVacacion { get; set; }
    public decimal Aguinaldo { get; set; }
    public decimal OtrosIngresos { get; set; }

    public decimal Isss { get; set; }
    public decimal Afp { get; set; }
    public decimal Renta { get; set; }
    public decimal OtrosDescuentos { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal SalarioNeto { get; set; }

    public decimal IsssPatronal { get; set; }
    public decimal AfpPatronal { get; set; }
    public decimal CostoPatronal { get; set; }
}

public static class PlanillaEstados
{
    public const string Borrador = "BORRADOR";
    public const string Calculada = "CALCULADA";
    public const string Cerrada = "CERRADA";
    public const string Anulada = "ANULADA";
}
