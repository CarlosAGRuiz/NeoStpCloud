using NeoSTP.Application.Common;
using NeoSTP.Application.Productos.Dtos;

namespace NeoSTP.Application.Productos;

public interface IProductosService
{
    Task<Result<PagedResult<ProductoDto>>> GetListAsync(int empresaId, PagedQuery query, string? categoria = null, CancellationToken ct = default);

    /// <summary>Categorías en uso + las definidas en el catálogo CATEGORIA_PRODUCTO de la empresa.</summary>
    Task<Result<IReadOnlyList<string>>> GetCategoriasAsync(int empresaId, CancellationToken ct = default);
    Task<Result<ProductoDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<ProductoDto>> CreateAsync(int empresaId, CreateProductoRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ProductoDto>> UpdateAsync(int empresaId, int id, UpdateProductoRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
    /// <summary>Reactiva un producto previamente inactivado (soft restore).</summary>
    Task<Result> RestaurarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    /// <summary>Carga masiva (upsert por código interno). Soporta dry-run.</summary>
    Task<Result<BulkImportResult>> ImportAsync(int empresaId, BulkImportRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Escalas de precio por volumen y unidades alternativas del producto.</summary>
    Task<Result<ProductoPreciosDto>> GetPreciosAsync(int empresaId, int productoId, CancellationToken ct = default);

    /// <summary>Reemplaza escalas y unidades alternativas (juego completo).</summary>
    Task<Result<ProductoPreciosDto>> SetPreciosAsync(int empresaId, int productoId, SetProductoPreciosRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Escalas de precio de varios productos en una sola consulta (POS, formularios de venta).</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<PrecioEscalaDto>>> GetEscalasAsync(int empresaId, IReadOnlyCollection<int> productoIds, CancellationToken ct = default);
}
