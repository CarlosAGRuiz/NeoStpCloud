namespace NeoSTP.Application.Auth.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    int? EmpresaId { get; }
    string? Username { get; }
    string? Email { get; }
    string? TipoUsuarioCodigo { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permisos { get; }
    bool HasPermiso(string codigo);
    bool IsInRole(string codigo);
}
