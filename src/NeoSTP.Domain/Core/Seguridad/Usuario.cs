using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Seguridad;

public class Usuario : AuditableEntity
{
    public int? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Guid SecurityStamp { get; set; }
    public string PasswordHash { get; set; } = null!;
    public string NombreCompleto { get; set; } = null!;
    public string? Telefono { get; set; }

    /// <summary>Código del catálogo TIPO_USUARIO: SUPERADMIN, ADMIN, OPERADOR, CONTADOR, READONLY.</summary>
    public string TipoUsuarioCodigo { get; set; } = "OPERADOR";

    public string EstadoCodigo { get; set; } = EstadoCodes.Activo;

    public DateTime? UltimoLogin { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }

    // MFA (TOTP) — Sprint 20 Hardening. Obligatorio para SuperAdmin.
    /// <summary>Indica si el usuario tiene activado el segundo factor (TOTP).</summary>
    public bool MfaHabilitado { get; set; }

    /// <summary>Control optimista de enrolamiento y consumo único de recuperación.</summary>
    public Guid MfaVersion { get; set; }

    /// <summary>Secreto TOTP cifrado con DataProtection. Nunca se expone en claro.</summary>
    public string? MfaSecretoCifrado { get; set; }

    /// <summary>Momento en que el usuario confirmó el enrolamiento del segundo factor.</summary>
    public DateTime? MfaConfirmadoAt { get; set; }

    /// <summary>Códigos de recuperación (hash) serializados en JSON.</summary>
    public string? MfaRecoveryCodesJson { get; set; }

    // SSO federado (E3). Vincula la cuenta local con una identidad OIDC estable.
    /// <summary>Proveedor OIDC con el que se vinculó la cuenta: ENTRA | GOOGLE. Null si es cuenta local.</summary>
    public string? SsoProveedor { get; set; }

    /// <summary>Issuer OIDC validado. Las vinculaciones antiguas deben probarse de nuevo.</summary>
    public string? SsoIssuer { get; set; }

    /// <summary>Identificador estable "sub"/"oid". Único junto al proveedor y al issuer.</summary>
    public string? SsoSubject { get; set; }

    public ICollection<UsuarioRol> Roles { get; set; } = new List<UsuarioRol>();
}
