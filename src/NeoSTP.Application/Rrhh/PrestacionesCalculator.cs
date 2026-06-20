namespace NeoSTP.Application.Rrhh;

public sealed record AguinaldoResultado(
    int AntiguedadAnios,
    decimal Dias,
    decimal Monto,
    bool EsProporcional);

/// <summary>Calculos puros para vacaciones y aguinaldo, sin acceso a persistencia.</summary>
public static class PrestacionesCalculator
{
    public static int AntiguedadAnios(DateOnly fechaIngreso, DateOnly fechaCorte)
    {
        if (fechaCorte < fechaIngreso) return 0;
        var anios = fechaCorte.Year - fechaIngreso.Year;
        if (fechaIngreso.AddYears(anios) > fechaCorte) anios--;
        return Math.Max(0, anios);
    }

    public static int DiasVacacionDevengados(
        DateOnly fechaIngreso,
        DateOnly fechaCorte,
        int mesesParaDerecho,
        int diasPorPeriodo)
    {
        if (fechaCorte < fechaIngreso || mesesParaDerecho <= 0 || diasPorPeriodo <= 0) return 0;
        var meses = MesesCompletos(fechaIngreso, fechaCorte);
        return (meses / mesesParaDerecho) * diasPorPeriodo;
    }

    public static decimal PrimaVacacion(decimal salarioMensual, int dias, decimal porcentaje)
    {
        if (salarioMensual <= 0 || dias <= 0 || porcentaje <= 0) return 0m;
        return R((salarioMensual / 30m) * dias * porcentaje);
    }

    public static AguinaldoResultado CalcularAguinaldo(
        DateOnly fechaIngreso,
        DateOnly fechaCorte,
        decimal salarioMensual,
        int aniosTramoMedio,
        int aniosTramoLargo,
        decimal diasTramoCorto,
        decimal diasTramoMedio,
        decimal diasTramoLargo)
    {
        if (fechaCorte < fechaIngreso || salarioMensual <= 0)
            return new AguinaldoResultado(0, 0m, 0m, false);

        var antiguedad = AntiguedadAnios(fechaIngreso, fechaCorte);
        var proporcional = antiguedad < 1;
        decimal dias;
        if (proporcional)
        {
            var inicioAnio = new DateOnly(fechaCorte.Year, 1, 1);
            var desde = fechaIngreso > inicioAnio ? fechaIngreso : inicioAnio;
            var diasServicio = fechaCorte.DayNumber - desde.DayNumber + 1;
            var diasAnio = DateTime.IsLeapYear(fechaCorte.Year) ? 366m : 365m;
            dias = R4(diasTramoCorto * diasServicio / diasAnio);
        }
        else if (antiguedad >= aniosTramoLargo)
        {
            dias = diasTramoLargo;
        }
        else if (antiguedad >= aniosTramoMedio)
        {
            dias = diasTramoMedio;
        }
        else
        {
            dias = diasTramoCorto;
        }

        return new AguinaldoResultado(antiguedad, dias, R((salarioMensual / 30m) * dias), proporcional);
    }

    private static int MesesCompletos(DateOnly inicio, DateOnly fin)
    {
        var meses = (fin.Year - inicio.Year) * 12 + fin.Month - inicio.Month;
        if (inicio.AddMonths(meses) > fin) meses--;
        return Math.Max(0, meses);
    }

    private static decimal R(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal R4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
