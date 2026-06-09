using System.ComponentModel.DataAnnotations;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Comunicaciones;

/// <summary>
/// Configuración de correo saliente (SMTP) por empresa. La contraseña se cifra y nunca se
/// devuelve en claro. Si no hay configuración activa, el sistema usa el correo global.
/// </summary>
public interface IConfiguracionCorreoService
{
    Task<Result<ConfiguracionCorreoDto>> GetAsync(int empresaId, CancellationToken ct = default);
    Task<Result<ConfiguracionCorreoDto>> GuardarAsync(int empresaId, GuardarConfiguracionCorreoRequest request, string? actor, CancellationToken ct = default);
    Task<Result> ProbarAsync(int empresaId, string destino, string? actor, CancellationToken ct = default);
}

/// <summary>
/// Envío de correo resolviendo la configuración por empresa: usa el SMTP propio de la empresa
/// si está activo; en caso contrario, recae en el <c>IEmailSender</c> global.
/// </summary>
public interface ITenantEmailSender
{
    Task<NeoSTP.Application.Dte.Abstractions.EmailSendResult> EnviarAsync(int empresaId, NeoSTP.Application.Dte.Abstractions.EmailMessage message, CancellationToken ct = default);
}

public class ConfiguracionCorreoDto
{
    public bool Configurado { get; set; }
    public bool Activo { get; set; }
    public string? Host { get; set; }
    public int Puerto { get; set; } = 587;
    public bool UsarStartTls { get; set; } = true;
    public string? Usuario { get; set; }
    /// <summary>Indica si hay contraseña guardada (no se expone el valor).</summary>
    public bool TienePassword { get; set; }
    public string? FromNombre { get; set; }
    public string? FromEmail { get; set; }
}

public class GuardarConfiguracionCorreoRequest
{
    public bool Activo { get; set; }
    [Required, StringLength(160)] public string Host { get; set; } = null!;
    [Range(1, 65535)] public int Puerto { get; set; } = 587;
    public bool UsarStartTls { get; set; } = true;
    [StringLength(160)] public string? Usuario { get; set; }
    /// <summary>Si viene vacío, se conserva la contraseña previa.</summary>
    [StringLength(200)] public string? Password { get; set; }
    [Required, StringLength(120)] public string FromNombre { get; set; } = null!;
    [Required, EmailAddress, StringLength(160)] public string FromEmail { get; set; } = null!;
}
