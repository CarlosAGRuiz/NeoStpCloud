using NeoSTP.Application.Common;
using NeoSTP.Application.Productos.Dtos;

namespace NeoSTP.Application.Productos;

public interface IProductosService
{
    Task<Result<PagedResult<ProductoDto>>> GetListAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<ProductoDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<ProductoDto>> CreateAsync(int empresaId, CreateProductoRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ProductoDto>> UpdateAsync(int empresaId, int id, UpdateProductoRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
}
