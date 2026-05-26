using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Seguridad;

public class Usuario : AuditableEntity
{
    public int? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }

    /// <summary>Código del catálogo TIPO_USUARIO: SUPERADMIN, ADMIN, OPERADOR, CONTADOR, READONLY.</summary>
    public string TipoUsuarioCodigo { get; set; } = "OPERADOR";

    public string EstadoCodigo { get; set; } = EstadoCodes.Activo;

    public DateTime? UltimoLogin { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }

    public ICollection<UsuarioRol> Roles { get; set; } = new List<UsuarioRol>();
}
