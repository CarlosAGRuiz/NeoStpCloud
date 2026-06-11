namespace NeoSTP.Application.Lookups;

/// <summary>
/// V2.5-S4 — invalida la caché distribuida de lookups/catálogos. Se invoca al mutar
/// catálogos para que todas las instancias (multi-nodo con Redis) dejen de servir
/// la versión anterior sin esperar el TTL.
/// </summary>
public interface ILookupCacheInvalidator
{
    Task InvalidarCatalogosAsync(CancellationToken ct = default);
}
