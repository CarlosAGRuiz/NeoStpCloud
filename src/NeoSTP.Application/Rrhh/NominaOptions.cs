namespace NeoSTP.Application.Rrhh;

/// <summary>
/// Parámetros de nómina de El Salvador (sección "Nomina"). Tasas y tramos del año vigente,
/// **parametrizables** para ajustarlos sin recompilar. Defaults: tablas 2026.
/// </summary>
public sealed class NominaOptions
{
    public const string SectionName = "Nomina";

    /// <summary>ISSS (salud): 3% empleado / 7.5% patronal, tope salarial mensual $1,000.</summary>
    public CotizacionOptions Isss { get; set; } = new() { PorcentajeEmpleado = 0.03m, PorcentajePatronal = 0.075m, TopeMensual = 1000m };

    /// <summary>AFP (pensiones): 7.25% empleado / 8.75% patronal, tope salarial mensual.</summary>
    public CotizacionOptions Afp { get; set; } = new() { PorcentajeEmpleado = 0.0725m, PorcentajePatronal = 0.0875m, TopeMensual = 7401.10m };

    /// <summary>Tabla de retención de Renta (ISR) mensual MH, por tramos.</summary>
    public List<RentaTramo> RentaMensual { get; set; } = DefaultRentaMensual();

    /// <summary>Tabla oficial de retención mensual de El Salvador (Decreto vigente).</summary>
    public static List<RentaTramo> DefaultRentaMensual() => new()
    {
        new() { Desde = 0.01m,    Hasta = 472.00m,   CuotaFija = 0m,      Porcentaje = 0m,    SobreExcesoDe = 0m },
        new() { Desde = 472.01m,  Hasta = 895.24m,   CuotaFija = 17.67m,  Porcentaje = 0.10m, SobreExcesoDe = 472.00m },
        new() { Desde = 895.25m,  Hasta = 2038.10m,  CuotaFija = 60.00m,  Porcentaje = 0.20m, SobreExcesoDe = 895.24m },
        new() { Desde = 2038.11m, Hasta = null,      CuotaFija = 288.57m, Porcentaje = 0.30m, SobreExcesoDe = 2038.10m },
    };
}

/// <summary>Cotización social (ISSS/AFP): porcentajes empleado/patronal y tope salarial.</summary>
public sealed class CotizacionOptions
{
    public decimal PorcentajeEmpleado { get; set; }
    public decimal PorcentajePatronal { get; set; }
    /// <summary>Salario máximo cotizable mensual (0 = sin tope).</summary>
    public decimal TopeMensual { get; set; }
}

/// <summary>Tramo de la tabla de retención de Renta: cuota fija + % sobre el exceso.</summary>
public sealed class RentaTramo
{
    public decimal Desde { get; set; }
    /// <summary>Límite superior del tramo; null = sin límite (último tramo).</summary>
    public decimal? Hasta { get; set; }
    public decimal CuotaFija { get; set; }
    public decimal Porcentaje { get; set; }
    public decimal SobreExcesoDe { get; set; }
}
