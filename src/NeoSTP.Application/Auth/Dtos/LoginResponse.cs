namespace NeoSTP.Application.Auth.Dtos;

public class LoginResponse
{
    public string AccessToken { get; set; } = null!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserInfo User { get; set; } = null!;

    /// <summary>
    /// True cuando el usuario (SuperAdmin) inició sesión sin MFA habilitado y debe
    /// enrolar el segundo factor obligatoriamente. El cliente debe redirigir a /auth/mfa/enroll.
    /// </summary>
    public bool MfaEnrollmentRequired { get; set; }
}
