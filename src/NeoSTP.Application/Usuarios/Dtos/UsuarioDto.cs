namespace NeoSTP.Application.Usuarios.Dtos;

public class UsuarioDto
{
    public int Id { get; set; }
    public int? EmpresaId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
    public string TipoUsuarioCodigo { get; set; } = null!;
    public string EstadoCodigo { get; set; } = null!;
    public DateTime? UltimoLogin { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<int> RoleIds { get; set; } = Array.Empty<int>();
    public IReadOnlyList<string> RoleCodigos { get; set; } = Array.Empty<string>();
}

public class CreateUsuarioRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
    public string TipoUsuarioCodigo { get; set; } = "OPERADOR";
    public IReadOnlyList<int>? RoleIds { get; set; }
}

public class UpdateUsuarioRequest
{
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }
    public string TipoUsuarioCodigo { get; set; } = null!;
    public string EstadoCodigo { get; set; } = null!;
    public IReadOnlyList<int>? RoleIds { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = null!;
}
