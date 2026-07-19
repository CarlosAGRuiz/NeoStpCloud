using NeoSTP.Application.Common;

namespace NeoSTP.Application.Usuarios;

public sealed class MiembroExternoDto
{
    public int UsuarioId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public int RolId { get; set; }
    public string RolNombre { get; set; } = null!;
    public string EstadoCodigo { get; set; } = "ACTIVO";
}

public sealed class AgregarMiembroRequest
{
    /// <summary>Email o username de un usuario ya registrado en el sistema.</summary>
    public string EmailOUsername { get; set; } = null!;
    public int RolId { get; set; }
}

/// <summary>
/// Miembros externos de una empresa (E1): usuarios cuya empresa principal es otra
/// (contadores, asesores, personal de grupo) y acceden con un rol acotado.
/// No consumen el límite de usuarios del plan.
/// </summary>
public interface IUsuarioEmpresaService
{
    Task<Result<IReadOnlyList<MiembroExternoDto>>> ListarAsync(int empresaId, CancellationToken ct = default);
    Task<Result<MiembroExternoDto>> AgregarAsync(int empresaId, AgregarMiembroRequest request, string? actor, CancellationToken ct = default);
    Task<Result> QuitarAsync(int empresaId, int usuarioId, string? actor, CancellationToken ct = default);
}
