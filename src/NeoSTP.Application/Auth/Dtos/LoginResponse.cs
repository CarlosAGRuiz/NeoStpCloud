namespace NeoSTP.Application.Auth.Dtos;

public class LoginResponse
{
    public string AccessToken { get; set; } = null!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserInfo User { get; set; } = null!;
}
