using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;

namespace NeoSTP.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    public const string ClaimEmpresaId = "empresa_id";
    public const string ClaimTipoUsuario = "tipo_usuario";
    public const string ClaimPermiso = "permiso";

    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key debe estar configurado y tener al menos 32 caracteres. Revisa appsettings.Local.json o variables de entorno.");
        }
    }

    public (string Token, DateTime ExpiresAt) CreateAccessToken(UserInfo user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTipoUsuario, user.TipoUsuarioCodigo),
        };

        if (user.EmpresaId is not null)
        {
            claims.Add(new Claim(ClaimEmpresaId, user.EmpresaId.Value.ToString()));
        }

        foreach (var rol in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, rol));
        }

        foreach (var permiso in user.Permisos)
        {
            claims.Add(new Claim(ClaimPermiso, permiso));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
