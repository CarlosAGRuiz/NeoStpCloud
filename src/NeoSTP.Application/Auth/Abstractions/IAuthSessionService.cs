using System.Security.Claims;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Auth.Abstractions;

public interface IAuthSessionService
{
    Task<Result<UserInfo>> ValidatePrincipalAsync(ClaimsPrincipal principal, CancellationToken ct = default);
}
