namespace NeoSTP.Application.Rrhh;

/// <summary>Desglose del cálculo de nómina de un empleado para un salario base mensual.</summary>
public sealed class NominaResultado
{
    public decimal SalarioBruto { get; init; }

    public decimal BaseIsss { get; init; }
    public decimal IsssEmpleado { get; init; }
    public decimal IsssPatronal { get; init; }

    public decimal BaseAfp { get; init; }
    public decimal AfpEmpleado { get; init; }
    public decimal AfpPatronal { get; init; }

    /// <summary>Base gravable de Renta = bruto − ISSS empleado − AFP empleado.</summary>
    public decimal BaseRenta { get; init; }
    public decimal Renta { get; init; }

    /// <summary>Total descontado al empleado (ISSS + AFP + Renta).</summary>
    public decimal TotalDeduccionesEmpleado { get; init; }

    /// <summary>Líquido a pagar al empleado.</summary>
    public decimal SalarioNeto { get; init; }

    /// <summary>Costo total para el patrono (bruto + aportes patronales ISSS y AFP).</summary>
    public decimal CostoPatronal { get; init; }
}

/// <summary>
/// Calculadora de nómina de El Salvador, pura y sin estado (estilo ProfitCalculator).
/// Aplica ISSS, AFP y retención de Renta sobre un salario base mensual, con tasas/tramos
/// parametrizables. La orquestación quincenal/planilla se construye encima (Sprint 2).
/// </summary>
public sealed class NominaCalculator
{
    /// <summary>Calcula deducciones y aportes para un salario base mensual.</summary>
    public NominaResultado CalcularMensual(decimal salarioBruto, NominaOptions opciones)
    {
        if (salarioBruto < 0) salarioBruto = 0;

        var baseIsss = Tope(salarioBruto, opciones.Isss.TopeMensual);
        var isssEmp = R(baseIsss * opciones.Isss.PorcentajeEmpleado);
        var isssPat = R(baseIsss * opciones.Isss.PorcentajePatronal);

        var baseAfp = Tope(salarioBruto, opciones.Afp.TopeMensual);
        var afpEmp = R(baseAfp * opciones.Afp.PorcentajeEmpleado);
        var afpPat = R(baseAfp * opciones.Afp.PorcentajePatronal);

        var baseRenta = R(salarioBruto - isssEmp - afpEmp);
        var renta = CalcularRenta(baseRenta, opciones.RentaMensual);

        var deducciones = R(isssEmp + afpEmp + renta);
        var neto = R(salarioBruto - deducciones);
        var costoPatronal = R(salarioBruto + isssPat + afpPat);

        return new NominaResultado
        {
            SalarioBruto = R(salarioBruto),
            BaseIsss = baseIsss, IsssEmpleado = isssEmp, IsssPatronal = isssPat,
            BaseAfp = baseAfp, AfpEmpleado = afpEmp, AfpPatronal = afpPat,
            BaseRenta = baseRenta, Renta = renta,
            TotalDeduccionesEmpleado = deducciones,
            SalarioNeto = neto,
            CostoPatronal = costoPatronal,
        };
    }

    /// <summary>
    /// Cálculo de una quincena por prorrateo 50/50: devengado y deducciones (ISSS/AFP/Renta)
    /// son la mitad del cálculo mensual. La suma de las dos quincenas reconcilia el mes
    /// (con posible diferencia de centavos por redondeo). Política configurable a futuro.
    /// </summary>
    public NominaResultado CalcularQuincena(decimal salarioMensual, NominaOptions opciones)
    {
        var m = CalcularMensual(salarioMensual, opciones);
        decimal H(decimal v) => R(v / 2m);

        var bruto = H(m.SalarioBruto);
        var isssEmp = H(m.IsssEmpleado);
        var afpEmp = H(m.AfpEmpleado);
        var renta = H(m.Renta);
        var isssPat = H(m.IsssPatronal);
        var afpPat = H(m.AfpPatronal);
        var deducciones = R(isssEmp + afpEmp + renta);

        return new NominaResultado
        {
            SalarioBruto = bruto,
            BaseIsss = H(m.BaseIsss), IsssEmpleado = isssEmp, IsssPatronal = isssPat,
            BaseAfp = H(m.BaseAfp), AfpEmpleado = afpEmp, AfpPatronal = afpPat,
            BaseRenta = H(m.BaseRenta), Renta = renta,
            TotalDeduccionesEmpleado = deducciones,
            SalarioNeto = R(bruto - deducciones),
            CostoPatronal = R(bruto + isssPat + afpPat),
        };
    }

    /// <summary>Retención de Renta según la tabla de tramos (cuota fija + % sobre el exceso).</summary>
    public decimal CalcularRenta(decimal baseGravable, IReadOnlyList<RentaTramo> tabla)
    {
        if (baseGravable <= 0 || tabla.Count == 0) return 0m;
        foreach (var t in tabla)
        {
            if (baseGravable >= t.Desde && (t.Hasta is null || baseGravable <= t.Hasta))
                return R(t.CuotaFija + (baseGravable - t.SobreExcesoDe) * t.Porcentaje);
        }
        // Si supera todos los límites definidos, aplica el último tramo.
        var ultimo = tabla[^1];
        return R(ultimo.CuotaFija + (baseGravable - ultimo.SobreExcesoDe) * ultimo.Porcentaje);
    }

    private static decimal Tope(decimal valor, decimal tope) => tope > 0 ? Math.Min(valor, tope) : valor;

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
