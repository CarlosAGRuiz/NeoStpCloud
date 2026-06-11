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

    /// <summary>
    /// V2.5-S1 — combinaciones N:1: para líneas del banco sin match 1:1, busca pares o tríos de
    /// movimientos internos compatibles (mismo signo, dentro de la tolerancia) cuya suma iguala
    /// exactamente el monto de la línea. Cada interno se usa en una sola combinación.
    /// </summary>
    public static IReadOnlyList<SugerenciaConciliacionDto> SugerirCombinaciones(
        IReadOnlyList<BancoMatchRow> banco,
        IReadOnlyList<InternoMatchRow> internos,
        int toleranciaDias = 3,
        int maxPorCombinacion = 3)
    {
        // Excluye lo que ya resuelve el matching 1:1 para no competir con él.
        var unoAUno = Sugerir(banco, internos, toleranciaDias);
        var bancoResuelto = unoAUno.Select(s => s.MovimientoBancoId).ToHashSet();
        var internoResuelto = unoAUno.Select(s => s.MovimientoTesoreriaId).ToHashSet();

        var resultado = new List<SugerenciaConciliacionDto>();
        var internoUsado = new HashSet<int>(internoResuelto);
        foreach (var b in banco.Where(x => !bancoResuelto.Contains(x.Id)).OrderBy(x => x.Fecha).ThenBy(x => x.Id))
        {
            var objetivo = Math.Abs(b.Monto);
            var tipo = b.Monto > 0 ? "INGRESO" : "EGRESO";
            var candidatos = internos
                .Where(i => !internoUsado.Contains(i.Id)
                    && i.Tipo == tipo
                    && i.Monto < objetivo
                    && Math.Abs(b.Fecha.DayNumber - i.Fecha.DayNumber) <= toleranciaDias)
                .OrderBy(i => Math.Abs(b.Fecha.DayNumber - i.Fecha.DayNumber)).ThenBy(i => i.Id)
                .Take(12) // acota la búsqueda combinatoria
                .ToList();

            var combo = BuscarCombinacion(candidatos, objetivo, Math.Max(2, maxPorCombinacion));
            if (combo is null) continue;

            foreach (var i in combo) internoUsado.Add(i.Id);
            resultado.Add(new SugerenciaConciliacionDto
            {
                MovimientoBancoId = b.Id,
                MovimientoTesoreriaId = combo[0].Id,
                CombinacionIds = combo.Select(i => i.Id).ToList(),
                MovimientoTesoreriaConcepto = string.Join(" + ", combo.Select(i => i.Concepto)),
                MovimientoTesoreriaFecha = combo[0].Fecha,
                DiferenciaDias = combo.Max(i => Math.Abs(b.Fecha.DayNumber - i.Fecha.DayNumber)),
                Confianza = ConfianzasConciliacion.Media,
            });
        }
        return resultado;
    }

    /// <summary>Primera combinación de 2..max candidatos que suma exacto (orden: fecha más cercana).</summary>
    private static List<InternoMatchRow>? BuscarCombinacion(List<InternoMatchRow> candidatos, decimal objetivo, int max)
    {
        for (var i = 0; i < candidatos.Count; i++)
        {
            for (var j = i + 1; j < candidatos.Count; j++)
            {
                var suma2 = candidatos[i].Monto + candidatos[j].Monto;
                if (suma2 == objetivo) return [candidatos[i], candidatos[j]];
                if (max < 3 || suma2 >= objetivo) continue;
                for (var k = j + 1; k < candidatos.Count; k++)
                {
                    if (suma2 + candidatos[k].Monto == objetivo)
                        return [candidatos[i], candidatos[j], candidatos[k]];
                }
            }
        }
        return null;
    }

    /// <summary>Abono del banco (monto &gt; 0) ↔ INGRESO; cargo (monto &lt; 0) ↔ EGRESO. Monto exacto.</summary>
    public static bool MontoCompatible(BancoMatchRow banco, InternoMatchRow interno)
        => banco.Monto > 0
            ? interno.Tipo == "INGRESO" && interno.Monto == banco.Monto
            : interno.Tipo == "EGRESO" && interno.Monto == -banco.Monto;

    /// <summary>V2.5-S1 — el signo es compatible (sin exigir monto exacto, para conciliación parcial).</summary>
    public static bool SignoCompatible(decimal montoBanco, string tipoInterno)
        => montoBanco > 0 ? tipoInterno == "INGRESO" : tipoInterno == "EGRESO";

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
