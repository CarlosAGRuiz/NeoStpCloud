using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Common;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Numeración correlativa por empresa y serie. Usa un UPDATE atómico (mismo patrón que
/// los correlativos de DTE) para que dos usuarios emitiendo a la vez no reciban el mismo
/// número; con proveedor no relacional (tests InMemory) cae a un incremento por EF.
/// </summary>
public sealed class CorrelativoService : ICorrelativoService
{
    private readonly NeoStpDbContext _db;

    public CorrelativoService(NeoStpDbContext db) => _db = db;

    public async Task<string> SiguienteAsync(int empresaId, string prefijo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prefijo))
            throw new ArgumentException("El prefijo de la serie es obligatorio.", nameof(prefijo));

        var anio = DateTime.UtcNow.Year;
        var serie = $"{prefijo.Trim().ToUpperInvariant()}-{anio}";
        var numero = _db.Database.IsRelational()
            ? await SiguienteRelacionalAsync(empresaId, serie, ct)
            : await SiguienteEnMemoriaAsync(empresaId, serie, ct);

        return $"{serie}-{numero:D6}";
    }

    private async Task<int> SiguienteRelacionalAsync(int empresaId, string serie, CancellationToken ct)
    {
        // OUTPUT devuelve el valor ya incrementado dentro de la misma sentencia: no hay
        // ventana entre incrementar y leer, así que dos peticiones nunca comparten número.
        var asignados = await _db.Database.SqlQuery<int>(
            $"""
            UPDATE Core_Correlativos
               SET UltimoNumero  = UltimoNumero + 1,
                   ActualizadoAt = GETUTCDATE()
             OUTPUT INSERTED.UltimoNumero AS [Value]
             WHERE EmpresaId = {empresaId} AND Serie = {serie}
            """).ToListAsync(ct);

        if (asignados.Count > 0) return asignados[0];

        // Primera vez para esa empresa y serie.
        await _db.Database.ExecuteSqlAsync(
            $"""
            IF NOT EXISTS (SELECT 1 FROM Core_Correlativos WHERE EmpresaId = {empresaId} AND Serie = {serie})
                INSERT INTO Core_Correlativos (EmpresaId, Serie, UltimoNumero, ActualizadoAt)
                VALUES ({empresaId}, {serie}, 1, GETUTCDATE())
            """, ct);

        var nuevos = await _db.Database.SqlQuery<int>(
            $"""
            SELECT UltimoNumero AS [Value] FROM Core_Correlativos
             WHERE EmpresaId = {empresaId} AND Serie = {serie}
            """).ToListAsync(ct);
        return nuevos.Count > 0 ? nuevos[0] : 1;
    }

    private async Task<int> SiguienteEnMemoriaAsync(int empresaId, string serie, CancellationToken ct)
    {
        var fila = await _db.Correlativos
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Serie == serie, ct);
        if (fila is null)
        {
            fila = new Correlativo { EmpresaId = empresaId, Serie = serie, UltimoNumero = 0 };
            _db.Correlativos.Add(fila);
        }

        fila.UltimoNumero++;
        fila.ActualizadoAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return fila.UltimoNumero;
    }
}
