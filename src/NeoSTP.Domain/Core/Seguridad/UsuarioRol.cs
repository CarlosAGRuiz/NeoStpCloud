namespace NeoSTP.Domain.Core.Seguridad;

public class UsuarioRol
{
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
