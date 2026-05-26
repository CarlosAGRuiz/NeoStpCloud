namespace NeoSTP.Application.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NeoSTP.Cloud";
    public string Audience { get; set; } = "NeoSTP.Cloud.Clients";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 14;
}
