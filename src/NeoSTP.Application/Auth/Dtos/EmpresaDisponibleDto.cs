namespace NeoSTP.Application.Auth.Dtos;

/// <summary>Empresa en la que un usuario puede operar (principal o por membresía E1).</summary>
public sealed class EmpresaDisponibleDto
{
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = null!;
    /// <summary>True para la empresa principal del usuario (Usuario.EmpresaId).</summary>
    public bool EsPrincipal { get; set; }
    /// <summary>Rol con el que opera en esa empresa (roles propios si es la principal).</summary>
    public string? RolNombre { get; set; }
}
