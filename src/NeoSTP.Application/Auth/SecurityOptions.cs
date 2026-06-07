namespace NeoSTP.Application.Auth;

/// <summary>
/// Políticas de seguridad configurables (sección "Security"): complejidad de contraseña
/// y bloqueo de cuenta por intentos fallidos (M6.2). Valores por defecto seguros.
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public PasswordPolicyOptions Password { get; set; } = new();
    public LockoutOptions Lockout { get; set; } = new();
}

public sealed class PasswordPolicyOptions
{
    public int MinLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = false;
}

public sealed class LockoutOptions
{
    /// <summary>Intentos fallidos consecutivos antes de bloquear (0 = sin bloqueo).</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Duración del bloqueo temporal, en minutos.</summary>
    public int LockoutMinutes { get; set; } = 15;
}
