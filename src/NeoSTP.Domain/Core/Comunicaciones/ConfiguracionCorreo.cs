using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Comunicaciones;

/// <summary>
/// Configuración de correo saliente (SMTP) por empresa. Permite que cada empresa envíe
/// tickets y facturas con su propio remitente. La contraseña se guarda cifrada
/// (<c>ISecretProtector</c>). Si no hay config activa, se usa el correo global del sistema.
/// </summary>
public class ConfiguracionCorreo : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public bool Activo { get; set; }

    public string Host { get; set; } = null!;
    public int Puerto { get; set; } = 587;
    public bool UsarStartTls { get; set; } = true;

    public string? Usuario { get; set; }
    /// <summary>Contraseña SMTP cifrada con ISecretProtector. Nunca se expone en texto plano.</summary>
    public string? PasswordProtegida { get; set; }

    public string FromNombre { get; set; } = null!;
    public string FromEmail { get; set; } = null!;
}
