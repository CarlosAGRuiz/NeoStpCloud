using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Auth.Abstractions;

public class AuthContext
{
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? TraceId { get; set; }
}

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, AuthContext context, CancellationToken ct = default);
    Task<Result<LoginResponse>> RefreshAsync(string refreshToken, AuthContext context, CancellationToken ct = default);
    Task<Result> LogoutAsync(string? refreshToken, AuthContext context, CancellationToken ct = default);
    Task<Result<UserInfo>> GetCurrentUserInfoAsync(int userId, CancellationToken ct = default);
}
