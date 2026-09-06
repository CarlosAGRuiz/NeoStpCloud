namespace NeoSTP.Application.Auth.Dtos;

public class UserInfo
{
    public Guid SessionId { get; set; }
    public DateTime SessionExpiresAt { get; set; }
    public string SessionPurpose { get; set; } = SessionClaims.Full;
    public int Id { get; set; }
    public int? EmpresaId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string TipoUsuarioCodigo { get; set; } = null!;
    public DateTime? UltimoLogin { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Permisos { get; set; } = Array.Empty<string>();
}
