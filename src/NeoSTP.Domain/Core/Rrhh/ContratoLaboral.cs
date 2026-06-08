using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Rrhh;

/// <summary>
/// Contrato laboral de un empleado: condiciones (salario, periodicidad, tipo y vigencia).
/// El empleado puede tener historial; el contrato VIGENTE define el salario para la nómina.
/// </summary>
public class ContratoLaboral : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>INDEFINIDO / TEMPORAL / SERVICIOS.</summary>
    public string TipoContrato { get; set; } = "INDEFINIDO";

    /// <summary>Salario base mensual (insumo del NominaCalculator).</summary>
    public decimal SalarioMensual { get; set; }

    /// <summary>QUINCENAL / MENSUAL.</summary>
    public string PeriodicidadPago { get; set; } = "QUINCENAL";

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }

    /// <summary>VIGENTE / FINALIZADO.</summary>
    public string EstadoCodigo { get; set; } = "VIGENTE";
}

public static class ContratoEstados
{
    public const string Vigente = "VIGENTE";
    public const string Finalizado = "FINALIZADO";
}
