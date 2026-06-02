namespace NeoSTP.Application.Lookups;

/// <summary>Ítem genérico para dropdowns/cascadas: valor, etiqueta, padre y metadata opcional.</summary>
public sealed record LookupItem(string Value, string Label, string? Parent = null, string? Meta = null);

/// <summary>
/// Acceso unificado a catálogos y datos maestros para poblar selects, cascadas y
/// autocompletes desde vistas Razor o la API, evitando consultas dispersas y hardcodeos.
/// Cachea los catálogos del sistema (cambian poco).
/// </summary>
public interface ILookupService
{
    /// <summary>Ítems de un catálogo (opcionalmente filtrados por padre para cascadas).</summary>
    Task<IReadOnlyList<LookupItem>> GetCatalogoAsync(string codigo, int? empresaId, string? parent = null, CancellationToken ct = default);

    // Cascada territorial El Salvador
    Task<IReadOnlyList<LookupItem>> GetDepartamentosAsync(int? empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<LookupItem>> GetMunicipiosAsync(string departamentoCodigo, int? empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<LookupItem>> GetDistritosAsync(string municipioCodigo, int? empresaId, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el municipio (división 2024) padre de un distrito, derivado del catálogo
    /// DISTRITO_ES (ParentCodigo). Null si el distrito no está en catálogo.
    /// </summary>
    Task<string?> ResolverMunicipio2024Async(string distritoCodigo, int? empresaId, CancellationToken ct = default);

    // Datos maestros de la empresa
    Task<IReadOnlyList<LookupItem>> BuscarClientesAsync(int empresaId, string? search, int max = 20, CancellationToken ct = default);
    Task<IReadOnlyList<LookupItem>> BuscarProductosAsync(int empresaId, string? search, int max = 20, CancellationToken ct = default);
    Task<IReadOnlyList<LookupItem>> GetSucursalesAsync(int empresaId, CancellationToken ct = default);
}
