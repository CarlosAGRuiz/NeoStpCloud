using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services.Catalogos;

namespace NeoSTP.Infrastructure.Services;

public class CatalogosService : ICatalogosService
{
    private const string AuditModule = "CATALOGOS";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public CatalogosService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // ---------------------------------------------------------------- Lectura

    public async Task<Result<IReadOnlyList<CatalogoDto>>> GetListAsync(int? empresaId, CancellationToken ct = default)
    {
        var items = await _db.Catalogos
            .AsNoTracking()
            .Where(c => c.EmpresaId == null || c.EmpresaId == empresaId)
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
                Version = c.Version,
                MetadataJson = c.MetadataJson,
                TotalItems = c.Items.Count(i => i.Activo),
            })
            .ToListAsync(ct);
        return Result<IReadOnlyList<CatalogoDto>>.Ok(items);
    }

    public async Task<Result<CatalogoDto>> GetByCodigoAsync(string codigoCatalogo, int? empresaId, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(codigoCatalogo);
        if (codigo is null) return Result<CatalogoDto>.Fail("Código de catálogo requerido.", "VALIDATION");

        var dto = await _db.Catalogos.AsNoTracking()
            .Where(c => c.Codigo == codigo && (c.EmpresaId == null || c.EmpresaId == empresaId))
            .Select(c => new CatalogoDto
            {
                Id = c.Id,
                Codigo = c.Codigo,
                Nombre = c.Nombre,
                Descripcion = c.Descripcion,
                EsSistema = c.EsSistema,
                Activo = c.Activo,
                EmpresaId = c.EmpresaId,
                Version = c.Version,
                MetadataJson = c.MetadataJson,
                TotalItems = c.Items.Count(i => i.Activo),
            })
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result<CatalogoDto>.Fail($"Catálogo {codigo} no encontrado.", "CAT_NOT_FOUND")
            : Result<CatalogoDto>.Ok(dto);
    }

    public async Task<Result<IReadOnlyList<CatalogoItemDto>>> GetItemsAsync(string codigoCatalogo, int? empresaId, string? parentCodigo = null, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(codigoCatalogo);
        if (codigo is null) return Result<IReadOnlyList<CatalogoItemDto>>.Fail("Código de catálogo requerido.", "VALIDATION");

        var catalogo = await _db.Catalogos.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Codigo == codigo && (c.EmpresaId == null || c.EmpresaId == empresaId), ct);
        if (catalogo is null)
        {
            return Result<IReadOnlyList<CatalogoItemDto>>.Fail($"Catálogo {codigo} no encontrado.", "CAT_NOT_FOUND");
        }

        var q = _db.CatalogoItems.AsNoTracking().Where(i => i.CatalogoId == catalogo.Id);

        if (parentCodigo is not null)
        {
            var parent = parentCodigo.Trim();
            // Convención: ?parent=__ROOT__ → solo ítems sin padre. Cadena vacía == sin filtro.
            if (parent.Equals("__ROOT__", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(i => i.ParentCodigo == null);
            }
            else if (parent.Length > 0)
            {
                q = q.Where(i => i.ParentCodigo == parent);
            }
        }

        var items = await q
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
                EsSistema = i.EsSistema,
                ParentCodigo = i.ParentCodigo,
                MetadataJson = i.MetadataJson,
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<CatalogoItemDto>>.Ok(items);
    }

    // -------------------------------------------------------- CRUD catálogos

    public async Task<Result<CatalogoDto>> CreateAsync(int? empresaId, CreateCatalogoRequest request, string? actor, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(request.Codigo);
        if (codigo is null || string.IsNullOrWhiteSpace(request.Nombre))
        {
            return Result<CatalogoDto>.Fail("Código y nombre son obligatorios.", "VALIDATION");
        }

        var version = request.Version <= 0 ? 1 : request.Version;

        if (await _db.Catalogos.AnyAsync(c => c.Codigo == codigo && c.EmpresaId == empresaId && c.Version == version, ct))
        {
            return Result<CatalogoDto>.Fail($"Ya existe un catálogo {codigo} v{version} en este ámbito.", "CAT_DUPLICATE");
        }

        var catalogo = new Catalogo
        {
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            EsSistema = empresaId is null, // sistema solo cuando EmpresaId es null (SuperAdmin)
            Activo = true,
            EmpresaId = empresaId,
            Version = version,
            MetadataJson = request.MetadataJson,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        _db.Catalogos.Add(catalogo);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREATE", $"Catálogo {catalogo.Codigo} v{catalogo.Version} creado", "Catalogo", catalogo.Id);

        return Result<CatalogoDto>.Ok(ToDto(catalogo, 0));
    }

    public async Task<Result<CatalogoDto>> UpdateAsync(int? empresaId, string codigoCatalogo, UpdateCatalogoRequest request, string? actor, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(codigoCatalogo);
        if (codigo is null || string.IsNullOrWhiteSpace(request.Nombre))
        {
            return Result<CatalogoDto>.Fail("Código y nombre son obligatorios.", "VALIDATION");
        }

        var catalogo = await _db.Catalogos.FirstOrDefaultAsync(c => c.Codigo == codigo && c.EmpresaId == empresaId, ct);
        if (catalogo is null) return Result<CatalogoDto>.Fail($"Catálogo {codigo} no encontrado.", "CAT_NOT_FOUND");

        catalogo.Nombre = request.Nombre.Trim();
        catalogo.Descripcion = request.Descripcion?.Trim();
        if (request.Activo is bool act)
        {
            // Bloquear inactivar catálogos del sistema (regla: no se eliminan/desactivan).
            if (catalogo.EsSistema && !act)
            {
                return Result<CatalogoDto>.Fail("Los catálogos del sistema no pueden inactivarse.", "CAT_SYSTEM_NOT_EDITABLE");
            }
            catalogo.Activo = act;
        }
        if (request.Version is int newVersion && newVersion > 0) catalogo.Version = newVersion;
        if (request.MetadataJson is not null) catalogo.MetadataJson = request.MetadataJson;
        catalogo.UpdatedAt = DateTime.UtcNow;
        catalogo.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPDATE", $"Catálogo {catalogo.Codigo} v{catalogo.Version} actualizado", "Catalogo", catalogo.Id);

        var totalItems = await _db.CatalogoItems.AsNoTracking().CountAsync(i => i.CatalogoId == catalogo.Id && i.Activo, ct);
        return Result<CatalogoDto>.Ok(ToDto(catalogo, totalItems));
    }

    // ------------------------------------------------------------ CRUD ítems

    public async Task<Result<CatalogoItemDto>> CreateItemAsync(int? empresaId, string codigoCatalogo, CreateCatalogoItemRequest request, string? actor, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(codigoCatalogo);
        if (codigo is null || string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Valor))
        {
            return Result<CatalogoItemDto>.Fail("Código y valor son obligatorios.", "VALIDATION");
        }

        var catalogo = await _db.Catalogos.FirstOrDefaultAsync(c => c.Codigo == codigo && c.EmpresaId == empresaId, ct);
        if (catalogo is null) return Result<CatalogoItemDto>.Fail($"Catálogo {codigo} no encontrado.", "CAT_NOT_FOUND");

        var itemCodigo = request.Codigo.Trim();
        var parent = string.IsNullOrWhiteSpace(request.ParentCodigo) ? null : request.ParentCodigo.Trim();

        if (await _db.CatalogoItems.AnyAsync(i => i.CatalogoId == catalogo.Id && i.Codigo == itemCodigo, ct))
        {
            return Result<CatalogoItemDto>.Fail($"Ya existe un ítem {itemCodigo} en el catálogo {catalogo.Codigo}.", "CAT_ITEM_DUPLICATE");
        }

        if (parent is not null && !await _db.CatalogoItems.AnyAsync(i => i.CatalogoId == catalogo.Id && i.Codigo == parent, ct))
        {
            return Result<CatalogoItemDto>.Fail($"El padre {parent} no existe en {catalogo.Codigo}.", "CAT_PARENT_NOT_FOUND");
        }

        var item = new CatalogoItem
        {
            CatalogoId = catalogo.Id,
            Codigo = itemCodigo,
            Valor = request.Valor.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            Orden = request.Orden,
            EsSistema = false, // sólo el seeder crea ítems del sistema
            Activo = true,
            ParentCodigo = parent,
            MetadataJson = request.MetadataJson,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        _db.CatalogoItems.Add(item);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREATE_ITEM", $"Ítem {catalogo.Codigo}/{item.Codigo} creado", "CatalogoItem", item.Id);

        return Result<CatalogoItemDto>.Ok(ToDto(item));
    }

    public async Task<Result<CatalogoItemDto>> UpdateItemAsync(int? empresaId, string codigoCatalogo, int itemId, UpdateCatalogoItemRequest request, string? actor, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(codigoCatalogo);
        if (codigo is null) return Result<CatalogoItemDto>.Fail("Código de catálogo requerido.", "VALIDATION");

        var item = await _db.CatalogoItems
            .Include(i => i.Catalogo)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Catalogo.Codigo == codigo && i.Catalogo.EmpresaId == empresaId, ct);
        if (item is null) return Result<CatalogoItemDto>.Fail("Ítem no encontrado.", "CAT_ITEM_NOT_FOUND");

        if (string.IsNullOrWhiteSpace(request.Valor))
        {
            return Result<CatalogoItemDto>.Fail("El valor es obligatorio.", "VALIDATION");
        }

        item.Valor = request.Valor.Trim();
        item.Descripcion = request.Descripcion?.Trim();
        if (request.Orden is int orden) item.Orden = orden;
        if (request.Activo is bool act) item.Activo = act;
        if (request.MetadataJson is not null) item.MetadataJson = request.MetadataJson;

        if (request.ParentCodigo is not null)
        {
            var parent = string.IsNullOrWhiteSpace(request.ParentCodigo) ? null : request.ParentCodigo.Trim();
            if (parent is not null && parent.Equals(item.Codigo, StringComparison.OrdinalIgnoreCase))
            {
                return Result<CatalogoItemDto>.Fail("Un ítem no puede ser su propio padre.", "CAT_PARENT_SELF");
            }
            if (parent is not null && !await _db.CatalogoItems.AnyAsync(i => i.CatalogoId == item.CatalogoId && i.Codigo == parent, ct))
            {
                return Result<CatalogoItemDto>.Fail($"El padre {parent} no existe.", "CAT_PARENT_NOT_FOUND");
            }
            item.ParentCodigo = parent;
        }

        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPDATE_ITEM", $"Ítem {item.Catalogo.Codigo}/{item.Codigo} actualizado", "CatalogoItem", item.Id);

        return Result<CatalogoItemDto>.Ok(ToDto(item));
    }

    public async Task<Result> DeleteItemAsync(int? empresaId, string codigoCatalogo, int itemId, string? actor, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(codigoCatalogo);
        if (codigo is null) return Result.Fail("Código de catálogo requerido.", "VALIDATION");

        var item = await _db.CatalogoItems
            .Include(i => i.Catalogo)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Catalogo.Codigo == codigo && i.Catalogo.EmpresaId == empresaId, ct);
        if (item is null) return Result.Fail("Ítem no encontrado.", "CAT_ITEM_NOT_FOUND");

        if (item.EsSistema)
        {
            return Result.Fail("Los ítems del sistema no se eliminan físicamente. Inactívelos en su lugar.", "CAT_ITEM_SYSTEM");
        }

        // Bloquear borrado si tiene hijos (cascada referencial por código).
        if (await _db.CatalogoItems.AnyAsync(i => i.CatalogoId == item.CatalogoId && i.ParentCodigo == item.Codigo, ct))
        {
            return Result.Fail($"El ítem {item.Codigo} tiene hijos. Reasígnelos o elimínelos primero.", "CAT_ITEM_HAS_CHILDREN");
        }

        _db.CatalogoItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "DELETE_ITEM", $"Ítem {item.Catalogo.Codigo}/{item.Codigo} eliminado", "CatalogoItem", item.Id);
        return Result.Ok();
    }

    // -------------------------------------------------------- Import / Export

    public async Task<Result<CatalogoImportResult>> ImportItemsAsync(int? empresaId, string codigoCatalogo, CatalogoImportRequest request, string? actor, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(codigoCatalogo);
        if (codigo is null) return Result<CatalogoImportResult>.Fail("Código de catálogo requerido.", "VALIDATION");

        var catalogo = await _db.Catalogos.FirstOrDefaultAsync(c => c.Codigo == codigo && c.EmpresaId == empresaId, ct);
        if (catalogo is null) return Result<CatalogoImportResult>.Fail($"Catálogo {codigo} no encontrado.", "CAT_NOT_FOUND");

        IReadOnlyList<CatalogoItemRow> rows;
        try
        {
            rows = request.Format switch
            {
                CatalogoFileFormat.Csv => CatalogoItemParsers.ParseCsv(request.Content),
                CatalogoFileFormat.Json => CatalogoItemParsers.ParseJson(request.Content),
                CatalogoFileFormat.Xlsx => CatalogoItemParsers.ParseXlsx(request.Content),
                _ => throw new FormatException("Formato no soportado."),
            };
        }
        catch (Exception ex)
        {
            return Result<CatalogoImportResult>.Fail($"No se pudo leer el archivo: {ex.Message}", "CAT_IMPORT_FORMAT");
        }

        var result = new CatalogoImportResult { DryRun = request.DryRun, Total = rows.Count };

        var existing = await _db.CatalogoItems
            .Where(i => i.CatalogoId == catalogo.Id)
            .ToListAsync(ct);
        var byCodigo = existing.ToDictionary(i => i.Codigo, StringComparer.OrdinalIgnoreCase);

        var nuevosCodigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ahora = DateTime.UtcNow;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Codigo) || string.IsNullOrWhiteSpace(row.Valor))
            {
                result.Errors.Add(new CatalogoImportError
                {
                    Row = row.RowNumber, Codigo = row.Codigo,
                    Message = "Código y valor son obligatorios.",
                });
                continue;
            }

            var rowCodigo = row.Codigo.Trim();
            var parent = string.IsNullOrWhiteSpace(row.ParentCodigo) ? null : row.ParentCodigo.Trim();

            if (parent is not null
                && !byCodigo.ContainsKey(parent)
                && !nuevosCodigos.Contains(parent))
            {
                // El padre debe existir previamente o en filas anteriores del mismo lote.
                result.Errors.Add(new CatalogoImportError
                {
                    Row = row.RowNumber, Codigo = rowCodigo,
                    Message = $"Padre '{parent}' no existe en el catálogo.",
                });
                continue;
            }

            if (byCodigo.TryGetValue(rowCodigo, out var item))
            {
                if (request.Mode == CatalogoImportMode.InsertOnly)
                {
                    result.Skipped++;
                    continue;
                }
                if (item.EsSistema)
                {
                    // Sólo se permite refrescar Valor/Descripcion/Orden/Metadata/Activo del sistema.
                    item.Valor = row.Valor!.Trim();
                    item.Descripcion = row.Descripcion?.Trim();
                    if (row.Orden is int o) item.Orden = o;
                    if (row.Activo is bool a) item.Activo = a;
                    if (row.MetadataJson is not null) item.MetadataJson = row.MetadataJson;
                    // No tocamos ParentCodigo de ítems del sistema desde import.
                    item.UpdatedAt = ahora;
                    item.UpdatedBy = actor;
                }
                else
                {
                    item.Valor = row.Valor!.Trim();
                    item.Descripcion = row.Descripcion?.Trim();
                    if (row.Orden is int o) item.Orden = o;
                    if (row.Activo is bool a) item.Activo = a;
                    if (row.MetadataJson is not null) item.MetadataJson = row.MetadataJson;
                    item.ParentCodigo = parent;
                    item.UpdatedAt = ahora;
                    item.UpdatedBy = actor;
                }
                result.Updated++;
            }
            else
            {
                var nuevo = new CatalogoItem
                {
                    CatalogoId = catalogo.Id,
                    Codigo = rowCodigo,
                    Valor = row.Valor!.Trim(),
                    Descripcion = row.Descripcion?.Trim(),
                    Orden = row.Orden ?? 0,
                    EsSistema = false,
                    Activo = row.Activo ?? true,
                    ParentCodigo = parent,
                    MetadataJson = row.MetadataJson,
                    CreatedAt = ahora,
                    CreatedBy = actor,
                };
                if (!request.DryRun)
                {
                    _db.CatalogoItems.Add(nuevo);
                }
                byCodigo[rowCodigo] = nuevo;
                nuevosCodigos.Add(rowCodigo);
                result.Inserted++;
            }
        }

        if (request.DryRun)
        {
            // Descartar cambios en memoria (sólo si tenemos updates pendientes).
            foreach (var entry in _db.ChangeTracker.Entries<CatalogoItem>().ToList())
            {
                entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            }
        }
        else
        {
            await _db.SaveChangesAsync(ct);
            await Audit(empresaId, actor, "IMPORT",
                $"Import {catalogo.Codigo}: {result.Inserted} insertados, {result.Updated} actualizados, {result.Skipped} omitidos, {result.ErrorCount} errores",
                "Catalogo", catalogo.Id);
        }

        return Result<CatalogoImportResult>.Ok(result);
    }

    public async Task<Result<CatalogoExportFile>> ExportItemsAsync(int? empresaId, string codigoCatalogo, CatalogoFileFormat format, CancellationToken ct = default)
    {
        var codigo = NormalizeCodigo(codigoCatalogo);
        if (codigo is null) return Result<CatalogoExportFile>.Fail("Código de catálogo requerido.", "VALIDATION");

        var catalogo = await _db.Catalogos.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Codigo == codigo && (c.EmpresaId == null || c.EmpresaId == empresaId), ct);
        if (catalogo is null) return Result<CatalogoExportFile>.Fail($"Catálogo {codigo} no encontrado.", "CAT_NOT_FOUND");

        var items = await _db.CatalogoItems.AsNoTracking()
            .Where(i => i.CatalogoId == catalogo.Id)
            .OrderBy(i => i.Orden).ThenBy(i => i.Codigo)
            .Select(i => new CatalogoItemDto
            {
                Id = i.Id, CatalogoId = i.CatalogoId,
                Codigo = i.Codigo, Valor = i.Valor, Descripcion = i.Descripcion,
                Orden = i.Orden, Activo = i.Activo, EsSistema = i.EsSistema,
                ParentCodigo = i.ParentCodigo, MetadataJson = i.MetadataJson,
            })
            .ToListAsync(ct);

        var (content, contentType, ext) = format switch
        {
            CatalogoFileFormat.Json => (CatalogoItemExporters.ToJson(items), "application/json", "json"),
            CatalogoFileFormat.Xlsx => (CatalogoItemExporters.ToXlsx(items, catalogo.Codigo),
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
            _ => (CatalogoItemExporters.ToCsv(items), "text/csv; charset=utf-8", "csv"),
        };

        return Result<CatalogoExportFile>.Ok(new CatalogoExportFile
        {
            FileName = $"{catalogo.Codigo}.v{catalogo.Version}.{ext}",
            ContentType = contentType,
            Content = content,
        });
    }

    // ----------------------------------------------------------- Helpers

    private static string? NormalizeCodigo(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToUpperInvariant();

    private static CatalogoDto ToDto(Catalogo c, int totalItems) => new()
    {
        Id = c.Id,
        Codigo = c.Codigo,
        Nombre = c.Nombre,
        Descripcion = c.Descripcion,
        EsSistema = c.EsSistema,
        Activo = c.Activo,
        EmpresaId = c.EmpresaId,
        Version = c.Version,
        MetadataJson = c.MetadataJson,
        TotalItems = totalItems,
    };

    private static CatalogoItemDto ToDto(CatalogoItem i) => new()
    {
        Id = i.Id,
        CatalogoId = i.CatalogoId,
        Codigo = i.Codigo,
        Valor = i.Valor,
        Descripcion = i.Descripcion,
        Orden = i.Orden,
        Activo = i.Activo,
        EsSistema = i.EsSistema,
        ParentCodigo = i.ParentCodigo,
        MetadataJson = i.MetadataJson,
    };

    private Task Audit(int? empresaId, string? actor, string accion, string detalle, string entidad, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId.ToString(),
            Resultado = "OK",
            Detalle = detalle,
        });
}
