using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class CatalogosService : ICatalogosService
{
    private readonly NeoStpDbContext _db;

    public CatalogosService(NeoStpDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<CatalogoDto>>> GetListAsync(int? empresaId, CancellationToken ct = default)
    {
        var items = await _db.Catalogos
            .AsNoTracking()
            .Where(c => c.Activo && (c.EmpresaId == null || c.EmpresaId == empresaId))
            .OrderBy(c => c.EmpresaId == null ? 0 : 1)
            .ThenBy(c => c.Nombre)
            .Select(c => new CatalogoDto
            {
                Id = c.Id,
                Codigo = c.Codigo,
                Nombre = c.Nombre,
                Descripcion = c.Descripcion,
                EsSistema = c.EsSistema,
                Activo = c.Activo,
                EmpresaId = c.EmpresaId,
            })
            .ToListAsync(ct);
        return Result<IReadOnlyList<CatalogoDto>>.Ok(items);
    }

    public async Task<Result<IReadOnlyList<CatalogoItemDto>>> GetItemsAsync(string codigoCatalogo, int? empresaId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigoCatalogo))
        {
            return Result<IReadOnlyList<CatalogoItemDto>>.Fail("Código de catálogo requerido.", "VALIDATION");
        }

        var codigo = codigoCatalogo.Trim().ToUpperInvariant();
        var catalogo = await _db.Catalogos.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Codigo == codigo && c.Activo && (c.EmpresaId == null || c.EmpresaId == empresaId), ct);
        if (catalogo is null)
        {
            return Result<IReadOnlyList<CatalogoItemDto>>.Fail($"Catálogo {codigo} no encontrado.", "CAT_NOT_FOUND");
        }

        var items = await _db.CatalogoItems
            .AsNoTracking()
            .Where(i => i.CatalogoId == catalogo.Id && i.Activo)
            .OrderBy(i => i.Orden).ThenBy(i => i.Codigo)
            .Select(i => new CatalogoItemDto
            {
                Id = i.Id,
                CatalogoId = i.CatalogoId,
                Codigo = i.Codigo,
                Valor = i.Valor,
                Descripcion = i.Descripcion,
                Orden = i.Orden,
                Activo = i.Activo,
                MetadataJson = i.MetadataJson,
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<CatalogoItemDto>>.Ok(items);
    }
}
