namespace NeoSTP.Application.Auth.Dtos;

public class LoginRequest
{
    public string UsernameOrEmail { get; set; } = null!;
    public string Password { get; set; } = null!;

    /// <summary>Código TOTP o de recuperación, requerido si el usuario tiene MFA habilitado.</summary>
    public string? MfaCode { get; set; }
}
