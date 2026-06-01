using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Catalogos;

public interface ICatalogosService
{
    // Lectura
    Task<Result<IReadOnlyList<CatalogoDto>>> GetListAsync(int? empresaId, CancellationToken ct = default);
    Task<Result<CatalogoDto>> GetByCodigoAsync(string codigoCatalogo, int? empresaId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CatalogoItemDto>>> GetItemsAsync(string codigoCatalogo, int? empresaId, string? parentCodigo = null, CancellationToken ct = default);

    // CRUD catálogos
    Task<Result<CatalogoDto>> CreateAsync(int? empresaId, CreateCatalogoRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CatalogoDto>> UpdateAsync(int? empresaId, string codigoCatalogo, UpdateCatalogoRequest request, string? actor, CancellationToken ct = default);

    // CRUD ítems
    Task<Result<CatalogoItemDto>> CreateItemAsync(int? empresaId, string codigoCatalogo, CreateCatalogoItemRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CatalogoItemDto>> UpdateItemAsync(int? empresaId, string codigoCatalogo, int itemId, UpdateCatalogoItemRequest request, string? actor, CancellationToken ct = default);
    Task<Result> DeleteItemAsync(int? empresaId, string codigoCatalogo, int itemId, string? actor, CancellationToken ct = default);

    // Import / Export
    Task<Result<CatalogoImportResult>> ImportItemsAsync(int? empresaId, string codigoCatalogo, CatalogoImportRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CatalogoExportFile>> ExportItemsAsync(int? empresaId, string codigoCatalogo, CatalogoFileFormat format, CancellationToken ct = default);
}
