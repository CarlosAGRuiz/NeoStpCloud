using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private const string AuditModule = "AUTH";
    private const int MaxIntentosFallidos = 5;
    private static readonly TimeSpan BloqueoDuracion = TimeSpan.FromMinutes(15);

    private readonly NeoStpDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IAuditoriaService _auditoria;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        NeoStpDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IAuditoriaService auditoria,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _auditoria = auditoria;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, AuthContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<LoginResponse>.Fail("Usuario y contraseña son obligatorios.", "AUTH_BAD_INPUT");
        }

        var input = request.UsernameOrEmail.Trim();

        var usuario = await _db.Usuarios
            .Include(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Username == input || u.Email == input, ct);

        if (usuario is null)
        {
            await AuditAsync(context, null, input, "LOGIN", "FAIL", "Usuario no encontrado");
            return Result<LoginResponse>.Fail("Usuario o contraseña incorrectos.", "AUTH_INVALID_CREDENTIALS");
        }

        if (usuario.BloqueadoHasta is { } bloqueoHasta && bloqueoHasta > DateTime.UtcNow)
        {
            await AuditAsync(context, usuario, "LOGIN", "FAIL", $"Bloqueado hasta {bloqueoHasta:O}");
            return Result<LoginResponse>.Fail("Usuario bloqueado temporalmente. Intenta más tarde.", "AUTH_USER_LOCKED");
        }

        if (usuario.EstadoCodigo != EstadoCodes.Activo)
        {
            await AuditAsync(context, usuario, "LOGIN", "FAIL", $"Estado: {usuario.EstadoCodigo}");
            return Result<LoginResponse>.Fail("Usuario inactivo o bloqueado.", "AUTH_USER_INACTIVE");
        }

        if (!_passwordHasher.Verify(request.Password, usuario.PasswordHash))
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= MaxIntentosFallidos)
            {
                usuario.BloqueadoHasta = DateTime.UtcNow.Add(BloqueoDuracion);
                usuario.EstadoCodigo = EstadoCodes.Bloqueado;
            }
            await _db.SaveChangesAsync(ct);
            await AuditAsync(context, usuario, "LOGIN", "FAIL", $"Password inválido (intento {usuario.IntentosFallidos})");
            return Result<LoginResponse>.Fail("Usuario o contraseña incorrectos.", "AUTH_INVALID_CREDENTIALS");
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoLogin = DateTime.UtcNow;

        var userInfo = ToUserInfo(usuario);
        var (accessToken, accessExpires) = _jwt.CreateAccessToken(userInfo);
        var refreshTokenValue = _jwt.CreateRefreshToken();
        var refreshExpires = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = refreshTokenValue,
            ExpiresAt = refreshExpires,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = context.IpAddress,
        });

        await _db.SaveChangesAsync(ct);
        await AuditAsync(context, usuario, "LOGIN", "OK", "Login exitoso");

        return Result<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpires,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = refreshExpires,
            User = userInfo,
        });
    }

    public async Task<Result<LoginResponse>> RefreshAsync(string refreshToken, AuthContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<LoginResponse>.Fail("Refresh token requerido.", "AUTH_BAD_INPUT");
        }

        var existing = await _db.RefreshTokens
            .Include(t => t.Usuario)
                .ThenInclude(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(t => t.Token == refreshToken, ct);

        if (existing is null || !existing.IsActive)
        {
            await AuditAsync(context, existing?.Usuario, "REFRESH", "FAIL", "Refresh token inválido o expirado");
            return Result<LoginResponse>.Fail("Refresh token inválido o expirado.", "AUTH_REFRESH_INVALID");
        }

        var usuario = existing.Usuario;
        if (usuario.EstadoCodigo != EstadoCodes.Activo)
        {
            await AuditAsync(context, usuario, "REFRESH", "FAIL", $"Usuario en estado {usuario.EstadoCodigo}");
            return Result<LoginResponse>.Fail("Usuario inactivo.", "AUTH_USER_INACTIVE");
        }

        var userInfo = ToUserInfo(usuario);
        var (accessToken, accessExpires) = _jwt.CreateAccessToken(userInfo);
        var newRefresh = _jwt.CreateRefreshToken();
        var refreshExpires = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays);

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = context.IpAddress;
        existing.RevokedReason = "Replaced";
        existing.ReplacedByToken = newRefresh;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = newRefresh,
            ExpiresAt = refreshExpires,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = context.IpAddress,
        });

        await _db.SaveChangesAsync(ct);
        await AuditAsync(context, usuario, "REFRESH", "OK", "Refresh token rotado");

        return Result<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpires,
            RefreshToken = newRefresh,
            RefreshTokenExpiresAt = refreshExpires,
            User = userInfo,
        });
    }

    public async Task<Result> LogoutAsync(string? refreshToken, AuthContext context, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var existing = await _db.RefreshTokens
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Token == refreshToken, ct);

            if (existing is not null && existing.RevokedAt is null)
            {
                existing.RevokedAt = DateTime.UtcNow;
                existing.RevokedByIp = context.IpAddress;
                existing.RevokedReason = "Logout";
                await _db.SaveChangesAsync(ct);
                await AuditAsync(context, existing.Usuario, "LOGOUT", "OK", "Logout exitoso");
            }
        }

        return Result.Ok();
    }

    public async Task<Result<UserInfo>> GetCurrentUserInfoAsync(int userId, CancellationToken ct = default)
    {
        var usuario = await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (usuario is null)
        {
            return Result<UserInfo>.Fail("Usuario no encontrado.", "AUTH_USER_NOT_FOUND");
        }

        return Result<UserInfo>.Ok(ToUserInfo(usuario));
    }

    private static UserInfo ToUserInfo(Usuario u) => new()
    {
        Id = u.Id,
        EmpresaId = u.EmpresaId,
        Username = u.Username,
        Email = u.Email,
        NombreCompleto = u.NombreCompleto,
        TipoUsuarioCodigo = u.TipoUsuarioCodigo,
        UltimoLogin = u.UltimoLogin,
        Roles = u.Roles.Select(ur => ur.Rol.Codigo).ToList(),
        Permisos = u.Roles
            .SelectMany(ur => ur.Rol.Permisos.Select(rp => rp.Permiso.Codigo))
            .Distinct()
            .ToList(),
    };

    private Task AuditAsync(AuthContext ctx, Usuario? user, string accion, string resultado, string? detalle)
        => AuditAsync(ctx, user?.Id, user?.Username, accion, resultado, detalle, user?.EmpresaId);

    private Task AuditAsync(AuthContext ctx, int? userId, string? username, string accion, string resultado, string? detalle, int? empresaId = null)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            UsuarioId = userId,
            Username = username,
            Modulo = AuditModule,
            Accion = accion,
            Resultado = resultado,
            Detalle = detalle,
            IpAddress = ctx.IpAddress,
            UserAgent = ctx.UserAgent,
            TraceId = ctx.TraceId,
        });
}
