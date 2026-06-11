namespace NeoSTP.Application.Ops;

/// <summary>
/// V2.5-S5 — purga de eventos de auditoría más viejos que la retención configurada.
/// La auditoría es operativa, no fiscal: los DTE y libros nunca se purgan.
/// </summary>
public interface ILimpiezaAuditoriaService
{
    /// <summary>Borra por lotes y devuelve cuántos eventos purgó.</summary>
    Task<int> PurgarAsync(int retencionDias, int batchSize = 5000, CancellationToken ct = default);
}
