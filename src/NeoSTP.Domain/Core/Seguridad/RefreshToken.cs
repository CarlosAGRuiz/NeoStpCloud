using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Seguridad;

public class RefreshToken : AuditableEntity
{
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    // El contexto pertenece a esta sesión; no se reconstruye desde la empresa principal.
    public int? ContextEmpresaId { get; set; }
    public bool ContextInitialized { get; set; }
    public Guid? SessionId { get; set; }

    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public string? RevokedReason { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
