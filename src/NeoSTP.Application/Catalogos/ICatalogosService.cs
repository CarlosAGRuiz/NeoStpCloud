using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Catalogos;

public interface ICatalogosService
{
    Task<Result<IReadOnlyList<CatalogoDto>>> GetListAsync(int? empresaId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CatalogoItemDto>>> GetItemsAsync(string codigoCatalogo, int? empresaId, CancellationToken ct = default);
}
