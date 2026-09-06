namespace NeoSTP.Domain.Core.Seguridad;

// Una sesión persiste aunque rote su refresh token; revocarla invalida JWT y cookie.
public class AuthSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int? EmpresaId { get; set; }
    public string Purpose { get; set; } = "FULL";
    public string CredentialFingerprint { get; set; } = null!;
    public string AuthorizationFingerprint { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
