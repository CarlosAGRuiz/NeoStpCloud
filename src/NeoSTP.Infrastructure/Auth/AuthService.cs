using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private const string AuditModule = "AUTH";

    private readonly NeoStpDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IAuditoriaService _auditoria;
    private readonly IMfaService _mfa;
    private readonly JwtOptions _jwtOptions;
    private readonly LockoutOptions _lockout;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        NeoStpDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IAuditoriaService auditoria,
        IMfaService mfa,
        IOptions<JwtOptions> jwtOptions,
        IOptions<SecurityOptions> securityOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _auditoria = auditoria;
        _mfa = mfa;
        _jwtOptions = jwtOptions.Value;
        _lockout = securityOptions.Value.Lockout;
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
            if (_lockout.MaxFailedAttempts > 0 && usuario.IntentosFallidos >= _lockout.MaxFailedAttempts)
            {
                usuario.BloqueadoHasta = DateTime.UtcNow.AddMinutes(_lockout.LockoutMinutes);
                usuario.EstadoCodigo = EstadoCodes.Bloqueado;
            }
            await _db.SaveChangesAsync(ct);
            await AuditAsync(context, usuario, "LOGIN", "FAIL", $"Password inválido (intento {usuario.IntentosFallidos})");
            return Result<LoginResponse>.Fail("Usuario o contraseña incorrectos.", "AUTH_INVALID_CREDENTIALS");
        }

        // Segundo factor (TOTP). Si está habilitado, exige código válido.
        if (usuario.MfaHabilitado)
        {
            if (string.IsNullOrWhiteSpace(request.MfaCode))
            {
                await AuditAsync(context, usuario, "LOGIN", "MFA_REQUIRED", "Falta código MFA");
                return Result<LoginResponse>.Fail("Se requiere el código de segundo factor.", "AUTH_MFA_REQUIRED");
            }

            var mfaResult = await _mfa.VerificarCodigoLoginAsync(usuario.Id, request.MfaCode, ct);
            if (mfaResult.IsFailure)
            {
                await AuditAsync(context, usuario, "LOGIN", "FAIL", "Código MFA inválido");
                return Result<LoginResponse>.Fail("Código de segundo factor inválido.", "AUTH_MFA_INVALID");
            }
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoLogin = DateTime.UtcNow;

        // SuperAdmin sin MFA: login permitido pero debe enrolar el segundo factor.
        var mfaEnrollmentRequired = usuario.TipoUsuarioCodigo == "SUPERADMIN" && !usuario.MfaHabilitado;

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
            MfaEnrollmentRequired = mfaEnrollmentRequired,
        });
    }

    public async Task<Result<IReadOnlyList<EmpresaDisponibleDto>>> ListarEmpresasDisponiblesAsync(int userId, CancellationToken ct = default)
    {
        var usuario = await _db.Usuarios.AsNoTracking()
            .Include(u => u.Empresa)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (usuario is null)
            return Result<IReadOnlyList<EmpresaDisponibleDto>>.Fail("Usuario no encontrado.", "USER_NOT_FOUND");

        var lista = new List<EmpresaDisponibleDto>();
        if (usuario.EmpresaId is not null && usuario.Empresa is not null)
        {
            lista.Add(new EmpresaDisponibleDto
            {
                EmpresaId = usuario.EmpresaId.Value,
                Nombre = usuario.Empresa.NombreComercial ?? usuario.Empresa.RazonSocial,
                EsPrincipal = true,
                RolNombre = null,
            });
        }

        var membresias = await _db.UsuarioEmpresas.AsNoTracking()
            .Include(m => m.Empresa)
            .Include(m => m.Rol)
            .Where(m => m.UsuarioId == userId && m.EstadoCodigo == "ACTIVO"
                     && m.Empresa.EstadoCodigo == "ACTIVA")
            .OrderBy(m => m.Empresa.RazonSocial)
            .ToListAsync(ct);
        lista.AddRange(membresias.Select(m => new EmpresaDisponibleDto
        {
            EmpresaId = m.EmpresaId,
            Nombre = m.Empresa.NombreComercial ?? m.Empresa.RazonSocial,
            EsPrincipal = false,
            RolNombre = m.Rol.Nombre,
        }));

        return Result<IReadOnlyList<EmpresaDisponibleDto>>.Ok(lista);
    }

    public async Task<Result<LoginResponse>> CambiarEmpresaAsync(int userId, int empresaId, AuthContext context, CancellationToken ct = default)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (usuario is null)
            return Result<LoginResponse>.Fail("Usuario no encontrado.", "USER_NOT_FOUND");
        if (usuario.EstadoCodigo != EstadoCodes.Activo)
            return Result<LoginResponse>.Fail("El usuario no está activo.", "AUTH_USER_DISABLED");

        UserInfo userInfo;
        if (usuario.EmpresaId == empresaId)
        {
            // Volver a la empresa principal: roles y permisos propios.
            userInfo = ToUserInfo(usuario);
        }
        else
        {
            var membresia = await _db.UsuarioEmpresas.AsNoTracking()
                .Include(m => m.Empresa)
                .Include(m => m.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
                .FirstOrDefaultAsync(m => m.UsuarioId == userId && m.EmpresaId == empresaId
                                       && m.EstadoCodigo == "ACTIVO", ct);
            if (membresia is null)
                return Result<LoginResponse>.Fail("No tienes acceso a esa empresa.", "EMPRESA_NO_MEMBRESIA");
            if (membresia.Empresa.EstadoCodigo != "ACTIVA")
                return Result<LoginResponse>.Fail("La empresa está suspendida o inactiva.", "EMPRESA_SUSPENDIDA");

            userInfo = ToUserInfo(usuario);
            userInfo.EmpresaId = empresaId;
            userInfo.Roles = new[] { membresia.Rol.Codigo };
            userInfo.Permisos = membresia.Rol.Permisos.Select(rp => rp.Permiso.Codigo).Distinct().ToList();
        }

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
        await AuditAsync(context, usuario, "CAMBIAR_EMPRESA", "OK", $"Empresa activa → {empresaId}");

        return Result<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpires,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = refreshExpires,
            User = userInfo,
        });
    }

    public async Task<Result<LoginResponse>> LoginExternoAsync(ExternalLoginInfo info, AuthContext context, CancellationToken ct = default)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.Subject) || string.IsNullOrWhiteSpace(info.Proveedor))
            return Result<LoginResponse>.Fail("Información de SSO incompleta.", "SSO_BAD_INPUT");
        if (!SsoProveedores.EsValido(info.Proveedor))
            return Result<LoginResponse>.Fail("Proveedor de SSO no soportado.", "SSO_PROVIDER_INVALID");

        var email = info.Email?.Trim().ToLowerInvariant();

        // 1) Sujeto federado ya vinculado a una cuenta local.
        var usuario = await _db.Usuarios
            .Include(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.SsoProveedor == info.Proveedor && u.SsoSubject == info.Subject, ct);

        if (usuario is null)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result<LoginResponse>.Fail("El proveedor no entregó un correo para vincular la cuenta.", "SSO_SIN_CORREO");

            // 2) Cuenta local con ese correo → vincular la identidad federada.
            usuario = await _db.Usuarios
                .Include(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
            if (usuario is not null)
            {
                usuario.SsoProveedor = info.Proveedor;
                usuario.SsoSubject = info.Subject;
                usuario.UpdatedAt = DateTime.UtcNow;
                usuario.UpdatedBy = "SSO";
            }
            else
            {
                // 3) Sin cuenta local: auto-aprovisionar según la config de la empresa dueña del dominio.
                var provision = await ProvisionarPorDominioAsync(info, email, ct);
                if (provision.IsFailure)
                    return Result<LoginResponse>.Fail(provision.Error!, provision.ErrorCode);
                usuario = provision.Value!;
                _db.Usuarios.Add(usuario);
            }
        }

        if (usuario.EstadoCodigo != EstadoCodes.Activo)
        {
            await AuditAsync(context, usuario, "LOGIN_SSO", "FAIL", $"Estado: {usuario.EstadoCodigo}");
            return Result<LoginResponse>.Fail("Usuario inactivo o bloqueado.", "AUTH_USER_INACTIVE");
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
        await AuditAsync(context, usuario, "LOGIN_SSO", "OK", $"SSO {info.Proveedor}");

        return Result<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpires,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = refreshExpires,
            User = userInfo,
        });
    }

    private async Task<Result<Usuario>> ProvisionarPorDominioAsync(ExternalLoginInfo info, string email, CancellationToken ct)
    {
        var arroba = email.IndexOf('@');
        if (arroba < 0 || arroba == email.Length - 1)
            return Result<Usuario>.Fail("Correo de SSO inválido.", "SSO_SIN_CORREO");
        var dominio = email[(arroba + 1)..];

        var config = await _db.EmpresaSso
            .Include(c => c.Empresa)
            .FirstOrDefaultAsync(c => c.DominioCorreo == dominio && c.Habilitado, ct);
        if (config is null)
            return Result<Usuario>.Fail("No hay una cuenta asociada a este correo. Contacta al administrador.", "SSO_SIN_CUENTA");
        if (!string.Equals(config.ProveedorCodigo, info.Proveedor, StringComparison.Ordinal))
            return Result<Usuario>.Fail("El proveedor de SSO no coincide con el configurado para tu empresa.", "SSO_PROVEEDOR_NO_COINCIDE");
        if (!string.IsNullOrWhiteSpace(config.TenantIdExterno)
            && !string.Equals(config.TenantIdExterno, info.TenantIdExterno, StringComparison.OrdinalIgnoreCase))
            return Result<Usuario>.Fail("Tu directorio corporativo no está autorizado para esta empresa.", "SSO_TENANT_NO_COINCIDE");
        if (config.Empresa is null || config.Empresa.EstadoCodigo != "ACTIVA")
            return Result<Usuario>.Fail("La empresa está suspendida o inactiva.", "EMPRESA_SUSPENDIDA");
        if (!config.AutoProvisionar || config.RolPorDefectoId is null)
            return Result<Usuario>.Fail("No hay una cuenta asociada a este correo. Contacta al administrador.", "SSO_SIN_CUENTA");

        var rol = await _db.Roles
            .Include(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(r => r.Id == config.RolPorDefectoId.Value, ct);
        if (rol is null)
            return Result<Usuario>.Fail("El rol por defecto de SSO no existe.", "SSO_ROL_INVALIDO");

        var usuario = new Usuario
        {
            EmpresaId = config.EmpresaId,
            Username = email,
            Email = email,
            NombreCompleto = string.IsNullOrWhiteSpace(info.NombreCompleto) ? email : info.NombreCompleto.Trim(),
            // Contraseña aleatoria inutilizable: la cuenta solo entra por SSO.
            PasswordHash = _passwordHasher.Hash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),
            TipoUsuarioCodigo = "OPERADOR",
            EstadoCodigo = EstadoCodes.Activo,
            SsoProveedor = info.Proveedor,
            SsoSubject = info.Subject,
            CreatedBy = "SSO",
            Roles = new List<UsuarioRol> { new() { RolId = rol.Id, Rol = rol } },
        };
        return Result<Usuario>.Ok(usuario);
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
