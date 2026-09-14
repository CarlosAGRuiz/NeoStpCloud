namespace NeoSTP.Application.Auth.Dtos;

public class LoginResponse
{
    public string AccessToken { get; set; } = null!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserInfo User { get; set; } = null!;

    /// <summary>
    /// Campo conservado por compatibilidad con clientes anteriores. La política actual
    /// no fuerza el enrolamiento: MFA es una función de seguridad opcional por usuario.
    /// </summary>
    public bool MfaEnrollmentRequired { get; set; }
    public bool MfaVerificationRequired { get; set; }
}
