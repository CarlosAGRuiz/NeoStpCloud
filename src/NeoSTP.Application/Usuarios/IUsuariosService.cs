using NeoSTP.Application.Common;
using NeoSTP.Application.Usuarios.Dtos;

namespace NeoSTP.Application.Usuarios;

public interface IUsuariosService
{
    Task<Result<PagedResult<UsuarioDto>>> GetListAsync(int? empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result<UsuarioDto>> GetByIdAsync(int? empresaId, int id, CancellationToken ct = default);
    Task<Result<UsuarioDto>> CreateAsync(int? empresaId, CreateUsuarioRequest request, string? actor, CancellationToken ct = default);
    Task<Result<UsuarioDto>> UpdateAsync(int? empresaId, int id, UpdateUsuarioRequest request, string? actor, CancellationToken ct = default);
    Task<Result> BloquearAsync(int? empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result> DesbloquearAsync(int? empresaId, int id, string? actor, CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(int userId, ChangePasswordRequest request, string? actor, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(int? empresaId, int id, ResetPasswordRequest request, string? actor, CancellationToken ct = default);
}
