using NeoSTP.Application.Tesoreria.Dtos;

namespace NeoSTP.Application.Tesoreria;

/// <summary>Línea bancaria candidata a conciliar (proyección mínima, monto con signo).</summary>
public sealed record BancoMatchRow(int Id, DateOnly Fecha, decimal Monto, string? Referencia);

/// <summary>Movimiento interno de tesorería candidato (monto positivo + tipo INGRESO/EGRESO).</summary>
public sealed record InternoMatchRow(int Id, DateOnly Fecha, decimal Monto, string Tipo, string? Referencia, string Concepto);

/// <summary>
/// Matching puro de conciliación bancaria. Empareja líneas del banco con movimientos internos
/// exigiendo monto exacto y signo compatible (abono↔INGRESO, cargo↔EGRESO) dentro de una
/// tolerancia de días; cada movimiento interno se usa una sola vez (greedy: primero los
/// emparejamientos con referencia coincidente, luego los de fecha más cercana).
/// </summary>
public static class ConciliacionCalculator
{
    public static IReadOnlyList<SugerenciaConciliacionDto> Sugerir(
        IReadOnlyList<BancoMatchRow> banco,
        IReadOnlyList<InternoMatchRow> internos,
        int toleranciaDias = 3)
    {
        // Todos los pares válidos (monto+signo+ventana de fecha), priorizados globalmente.
        var pares = new List<(BancoMatchRow B, InternoMatchRow I, bool RefMatch, int DiasDiff)>();
        foreach (var b in banco)
        {
            foreach (var i in internos)
            {
                if (!MontoCompatible(b, i)) continue;
                var diff = Math.Abs(b.Fecha.DayNumber - i.Fecha.DayNumber);
                if (diff > toleranciaDias) continue;
                pares.Add((b, i, ReferenciaCoincide(b.Referencia, i.Referencia), diff));
            }
        }

        var sugerencias = new List<SugerenciaConciliacionDto>();
        var bancoUsado = new HashSet<int>();
        var internoUsado = new HashSet<int>();
        foreach (var p in pares
            .OrderByDescending(p => p.RefMatch)
            .ThenBy(p => p.DiasDiff)
            .ThenBy(p => p.B.Id)
            .ThenBy(p => p.I.Id))
        {
            if (!bancoUsado.Add(p.B.Id)) continue;
            if (!internoUsado.Add(p.I.Id)) { bancoUsado.Remove(p.B.Id); continue; }
            sugerencias.Add(new SugerenciaConciliacionDto
            {
                MovimientoBancoId = p.B.Id,
                MovimientoTesoreriaId = p.I.Id,
                MovimientoTesoreriaConcepto = p.I.Concepto,
                MovimientoTesoreriaFecha = p.I.Fecha,
                DiferenciaDias = p.DiasDiff,
                Confianza = p.RefMatch || p.DiasDiff == 0 ? ConfianzasConciliacion.Alta : ConfianzasConciliacion.Media,
            });
        }
        return sugerencias.OrderBy(s => s.MovimientoBancoId).ToList();
    }

    /// <summary>Abono del banco (monto &gt; 0) ↔ INGRESO; cargo (monto &lt; 0) ↔ EGRESO. Monto exacto.</summary>
    public static bool MontoCompatible(BancoMatchRow banco, InternoMatchRow interno)
        => banco.Monto > 0
            ? interno.Tipo == "INGRESO" && interno.Monto == banco.Monto
            : interno.Tipo == "EGRESO" && interno.Monto == -banco.Monto;

    /// <summary>Coincidencia laxa de referencia: una contiene a la otra (sin caso, sin espacios).</summary>
    public static bool ReferenciaCoincide(string? a, string? b)
    {
        var na = Normalizar(a);
        var nb = Normalizar(b);
        if (na.Length == 0 || nb.Length == 0) return false;
        return na.Contains(nb, StringComparison.OrdinalIgnoreCase)
            || nb.Contains(na, StringComparison.OrdinalIgnoreCase);

        static string Normalizar(string? s) => (s ?? string.Empty).Replace(" ", string.Empty).Trim();
    }
}

public static class ConfianzasConciliacion
{
    public const string Alta = "ALTA";
    public const string Media = "MEDIA";
}
