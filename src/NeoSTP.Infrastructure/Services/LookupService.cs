using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Common;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="ILookupService"/>. Catálogos en dos niveles de caché:
/// L1 por instancia scoped (varias lecturas dentro de la misma petición) y L2 distribuida
/// (V2.5-S4: memoria o Redis según Cache:Provider, TTL 5 min, claves versionadas — al mutar
/// catálogos se publica una versión nueva y todas las instancias dejan de ver la anterior).
/// Datos maestros (clientes/productos/sucursales) siempre vía EF, sin caché.
/// </summary>
public sealed class LookupService : ILookupService
{
    internal const string VersionKey = "neostp:lookup:ver";
    private const string KeyPrefix = "neostp:lookup:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, (DateTime Expira, IReadOnlyList<LookupItem> Items)> _cache = new();

    private readonly NeoStpDbContext _db;
    private readonly ICatalogosService _catalogos;
    private readonly IDistributedCache? _distributed;

    public LookupService(NeoStpDbContext db, ICatalogosService catalogos, IDistributedCache? distributed = null)
    {
        _db = db;
        _catalogos = catalogos;
        _distributed = distributed;
    }

    public async Task<IReadOnlyList<LookupItem>> GetCatalogoAsync(string codigo, int? empresaId, string? parent = null, CancellationToken ct = default)
    {
        var version = _distributed is null ? "0" : await _distributed.GetStringAsync(VersionKey, ct) ?? "0";
        var key = $"{version}:{codigo}|{empresaId}|{parent}";
        if (_cache.TryGetValue(key, out var hit) && hit.Expira > DateTime.UtcNow)
            return hit.Items;

        if (_distributed is not null)
        {
            var json = await _distributed.GetStringAsync(KeyPrefix + key, ct);
            if (json is not null)
            {
                var cached = JsonSerializer.Deserialize<List<LookupItem>>(json) ?? [];
                _cache[key] = (DateTime.UtcNow.Add(CacheTtl), cached);
                return cached;
            }
        }

        var result = await _catalogos.GetItemsAsync(codigo, empresaId, parent, ct);
        var items = (result.Value ?? Array.Empty<Application.Catalogos.Dtos.CatalogoItemDto>())
            .Where(i => i.Activo)
            .OrderBy(i => i.Orden)
            .Select(i => new LookupItem(i.Codigo, i.Valor, i.ParentCodigo, i.MetadataJson))
            .ToList();

        _cache[key] = (DateTime.UtcNow.Add(CacheTtl), items);
        if (_distributed is not null)
        {
            await _distributed.SetStringAsync(KeyPrefix + key, JsonSerializer.Serialize(items),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        }
        return items;
    }

    public Task<IReadOnlyList<LookupItem>> GetDepartamentosAsync(int? empresaId, CancellationToken ct = default)
        => GetCatalogoAsync(CatalogCodes.DepartamentoEs, empresaId, null, ct);

    public Task<IReadOnlyList<LookupItem>> GetMunicipiosAsync(string departamentoCodigo, int? empresaId, CancellationToken ct = default)
        => GetCatalogoAsync(CatalogCodes.MunicipioEs, empresaId, departamentoCodigo, ct);

    public Task<IReadOnlyList<LookupItem>> GetDistritosAsync(string municipioCodigo, int? empresaId, CancellationToken ct = default)
        => GetCatalogoAsync(CatalogCodes.DistritoEs, empresaId, municipioCodigo, ct);

    public async Task<string?> ResolverMunicipio2024Async(string distritoCodigo, int? empresaId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(distritoCodigo)) return null;
        var distritos = await GetCatalogoAsync(CatalogCodes.DistritoEs, empresaId, null, ct);
        return distritos.FirstOrDefault(d => d.Value == distritoCodigo)?.Parent;
    }

    public async Task<IReadOnlyList<LookupItem>> BuscarClientesAsync(int empresaId, string? search, int max = 20, CancellationToken ct = default)
    {
        var q = _db.Clientes.AsNoTracking().Where(c => c.EmpresaId == empresaId && c.EstadoCodigo == "ACTIVO");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => c.Nombre.Contains(s) || c.NumeroDocumento.Contains(s) || (c.Nrc != null && c.Nrc.Contains(s)));
        }
        return await q.OrderBy(c => c.Nombre).Take(Math.Clamp(max, 1, 100))
            .Select(c => new LookupItem(c.Id.ToString(), c.Nombre, c.TipoDocumentoCodigo, c.NumeroDocumento))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LookupItem>> BuscarProductosAsync(int empresaId, string? search, int max = 20, CancellationToken ct = default)
    {
        var q = _db.Productos.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.EstadoCodigo == "ACTIVO");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p => p.Nombre.Contains(s) || p.CodigoInterno.Contains(s));
        }
        return await q.OrderBy(p => p.Nombre).Take(Math.Clamp(max, 1, 100))
            .Select(p => new LookupItem(p.Id.ToString(), p.Nombre, p.CodigoInterno, p.PrecioUnitario.ToString("0.####")))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LookupItem>> GetSucursalesAsync(int empresaId, CancellationToken ct = default)
        => await _db.Sucursales.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.EstadoCodigo == "ACTIVO")
            .OrderBy(s => s.Nombre)
            .Select(s => new LookupItem(s.Id.ToString(), s.Nombre, null, s.Codigo))
            .ToListAsync(ct);
}

/// <summary>
/// V2.5-S4 — publica una versión nueva de la caché de lookups; las claves anteriores
/// quedan huérfanas y expiran solas por TTL (no hay borrado por comodín en IDistributedCache).
/// </summary>
public sealed class LookupCacheInvalidator : ILookupCacheInvalidator
{
    private readonly IDistributedCache _distributed;

    public LookupCacheInvalidator(IDistributedCache distributed) => _distributed = distributed;

    public Task InvalidarCatalogosAsync(CancellationToken ct = default)
        => _distributed.SetStringAsync(LookupService.VersionKey, DateTime.UtcNow.Ticks.ToString(), ct);
}
