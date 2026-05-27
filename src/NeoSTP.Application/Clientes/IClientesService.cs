using NeoSTP.Application.Clientes.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Clientes;

public interface IClientesService
{
    Task<Result<PagedResult<ClienteDto>>> GetListAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<ClienteDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<ClienteDto>> CreateAsync(int empresaId, CreateClienteRequest request, string? actor, CancellationToken ct = default);
    Task<Result<ClienteDto>> UpdateAsync(int empresaId, int id, UpdateClienteRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);
}
