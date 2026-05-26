using NeoSTP.Application.Auth.Dtos;

namespace NeoSTP.Application.Auth.Abstractions;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(UserInfo user);
    string CreateRefreshToken();
}
