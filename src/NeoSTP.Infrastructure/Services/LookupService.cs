using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Common;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="ILookupService"/>. Catálogos vía <see cref="ICatalogosService"/>
/// (con caché en memoria de 5 min) y datos maestros vía EF Core.
/// </summary>
public sealed class LookupService : ILookupService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    // Caché por instancia (servicio scoped): evita relecturas dentro de una misma petición
    // (p. ej. resolver territorial + varios catálogos al generar un DTE) sin riesgo de staleness.
    private readonly ConcurrentDictionary<string, (DateTime Expira, IReadOnlyList<LookupItem> Items)> _cache = new();

    private readonly NeoStpDbContext _db;
    private readonly ICatalogosService _catalogos;

    public LookupService(NeoStpDbContext db, ICatalogosService catalogos)
    {
        _db = db;
        _catalogos = catalogos;
    }

    public async Task<IReadOnlyList<LookupItem>> GetCatalogoAsync(string codigo, int? empresaId, string? parent = null, CancellationToken ct = default)
    {
        var key = $"{codigo}|{empresaId}|{parent}";
        if (_cache.TryGetValue(key, out var hit) && hit.Expira > DateTime.UtcNow)
            return hit.Items;

        var result = await _catalogos.GetItemsAsync(codigo, empresaId, parent, ct);
        var items = (result.Value ?? Array.Empty<Application.Catalogos.Dtos.CatalogoItemDto>())
            .Where(i => i.Activo)
            .OrderBy(i => i.Orden)
            .Select(i => new LookupItem(i.Codigo, i.Valor, i.ParentCodigo, i.MetadataJson))
            .ToList();

        _cache[key] = (DateTime.UtcNow.Add(CacheTtl), items);
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
