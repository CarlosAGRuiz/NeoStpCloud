using System.Security.Cryptography;
using System.Text;
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
    private readonly ICurrentUser? _currentUser;

    public AuthService(
        NeoStpDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IAuditoriaService auditoria,
        IMfaService mfa,
        IOptions<JwtOptions> jwtOptions,
        IOptions<SecurityOptions> securityOptions,
        ILogger<AuthService> logger,
        ICurrentUser? currentUser = null)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _auditoria = auditoria;
        _mfa = mfa;
        _jwtOptions = jwtOptions.Value;
        _lockout = securityOptions.Value.Lockout;
        _logger = logger;
        _currentUser = currentUser;
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

        // Solo vence el bloqueo temporal; uno administrativo no tiene fecha de expiración.
        if (usuario.EstadoCodigo == EstadoCodes.Bloqueado
            && usuario.BloqueadoHasta is { } expiredAt && expiredAt <= DateTime.UtcNow)
        {
            usuario.EstadoCodigo = EstadoCodes.Activo;
            usuario.BloqueadoHasta = null;
            usuario.IntentosFallidos = 0;
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { _db.ChangeTracker.Clear(); return InvalidSession(); }
        }

        if (usuario.EstadoCodigo != EstadoCodes.Activo)
        {
            await AuditAsync(context, usuario, "LOGIN", "FAIL", $"Estado: {usuario.EstadoCodigo}");
            return Result<LoginResponse>.Fail("Usuario inactivo o bloqueado.", "AUTH_USER_INACTIVE");
        }

        if (!_passwordHasher.Verify(request.Password, usuario.PasswordHash))
        {
            await RegisterFailedAttemptAsync(usuario, ct);
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
                await RegisterFailedAttemptAsync(usuario, ct);
                await AuditAsync(context, usuario, "LOGIN", "FAIL", $"Código MFA inválido (intento {usuario.IntentosFallidos})");
                return Result<LoginResponse>.Fail("Código de segundo factor inválido.", "AUTH_MFA_INVALID");
            }
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoLogin = DateTime.UtcNow;

        var resolved = await ResolveUserInfoAsync(usuario, usuario.EmpresaId, ct);
        if (resolved.IsFailure)
            return Result<LoginResponse>.Fail(resolved.Error!, resolved.ErrorCode);
        var purpose = SessionClaims.Full;
        var response = await IssueSessionAsync(usuario, resolved.Value!, purpose, context, ct);
        if (response.IsFailure) return response;
        await AuditAsync(context, usuario, "LOGIN", "OK", "Login exitoso");
        return response;
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
                     && m.Empresa.EstadoCodigo == EmpresaEstados.Activa)
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
        if (context.SessionId is not Guid sessionId)
            return InvalidSession();
        var current = await new AuthSessionService(_db).ValidateAsync(sessionId, ct);
        if (current.IsFailure || current.Value!.Id != userId || current.Value.SessionPurpose != SessionClaims.Full)
            return InvalidSession();

        var usuario = await _db.Usuarios
            .Include(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (usuario is null)
            return Result<LoginResponse>.Fail("Usuario no encontrado.", "USER_NOT_FOUND");
        if (usuario.EstadoCodigo != EstadoCodes.Activo)
            return Result<LoginResponse>.Fail("El usuario no está activo.", "AUTH_USER_DISABLED");

        var resolved = await ResolveUserInfoAsync(usuario, empresaId, ct);
        if (resolved.IsFailure)
            return Result<LoginResponse>.Fail(resolved.Error!, resolved.ErrorCode);
        var purpose = SessionClaims.Full;
        var response = await IssueSessionAsync(usuario, resolved.Value!, purpose, context, ct);
        if (response.IsFailure) return response;
        await AuditAsync(context, usuario, "CAMBIAR_EMPRESA", "OK", $"Empresa activa → {empresaId}");
        return response;
    }

    public async Task<Result<LoginResponse>> LoginExternoAsync(ExternalLoginInfo info, AuthContext context, CancellationToken ct = default)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.Subject) || string.IsNullOrWhiteSpace(info.Proveedor))
            return Result<LoginResponse>.Fail("Información de SSO incompleta.", "SSO_BAD_INPUT");
        if (!SsoProveedores.EsValido(info.Proveedor))
            return Result<LoginResponse>.Fail("Proveedor de SSO no soportado.", "SSO_PROVIDER_INVALID");
        var identity = SsoIdentityPolicy.ValidateIdentity(info);
        if (identity.IsFailure) return Result<LoginResponse>.Fail(identity.Error!, identity.ErrorCode);

        var email = info.Email?.Trim().ToLowerInvariant();

        // 1) Sujeto federado ya vinculado a una cuenta local.
        var usuario = await _db.Usuarios
            .Include(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.SsoProveedor == info.Proveedor && u.SsoIssuer == info.Issuer && u.SsoSubject == info.Subject, ct);

        if (usuario is null)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result<LoginResponse>.Fail("El proveedor no entregó un correo para vincular la cuenta.", "SSO_SIN_CORREO");

            var policy = await SsoIdentityPolicy.ResolveAsync(_db, info, ct: ct);
            if (policy.IsFailure) return Result<LoginResponse>.Fail(policy.Error!, policy.ErrorCode);
            // Email is discovery only, never proof of ownership of an existing account.
            if (await _db.Usuarios.AnyAsync(u => u.EmpresaId == policy.Value!.EmpresaId && u.Email.ToLower() == email, ct))
                return Result<LoginResponse>.Fail("Confirma tu contraseña y segundo factor local para vincular esta cuenta.", "SSO_LINK_REQUIRED");
            var provision = await ProvisionarPorDominioAsync(info, email, ct);
            if (provision.IsFailure) return Result<LoginResponse>.Fail(provision.Error!, provision.ErrorCode);
            usuario = provision.Value!;
            _db.Usuarios.Add(usuario);
        }

        if (usuario.EmpresaId is null)
            return Result<LoginResponse>.Fail("La administración de plataforma requiere acceso local con MFA.", "SSO_PLATFORM_NOT_SUPPORTED");
        var authorization = await SsoIdentityPolicy.ResolveAsync(_db, info, usuario.EmpresaId, ct);
        if (authorization.IsFailure) return Result<LoginResponse>.Fail(authorization.Error!, authorization.ErrorCode);

        if (usuario.EstadoCodigo == EstadoCodes.Bloqueado
            && usuario.BloqueadoHasta is { } expiredAt && expiredAt <= DateTime.UtcNow)
        {
            usuario.EstadoCodigo = EstadoCodes.Activo;
            usuario.BloqueadoHasta = null;
            usuario.IntentosFallidos = 0;
        }
        if (usuario.EstadoCodigo != EstadoCodes.Activo || usuario.BloqueadoHasta > DateTime.UtcNow)
        {
            await AuditAsync(context, usuario, "LOGIN_SSO", "FAIL", $"Estado: {usuario.EstadoCodigo}");
            return Result<LoginResponse>.Fail("Usuario inactivo o bloqueado.", "AUTH_USER_INACTIVE");
        }

        // Persist provisioning/linking before issuing a credential (new SQL IDs must exist).
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { _db.ChangeTracker.Clear(); return InvalidSession(); }
        var resolved = await ResolveUserInfoAsync(usuario, usuario.EmpresaId, ct);
        if (resolved.IsFailure)
            return Result<LoginResponse>.Fail(resolved.Error!, resolved.ErrorCode);
        var purpose = usuario.MfaHabilitado
            ? SessionClaims.MfaVerify
            : SessionClaims.Full;
        if (purpose == SessionClaims.Full)
        {
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;
            usuario.UltimoLogin = DateTime.UtcNow;
        }
        var response = await IssueSessionAsync(usuario, resolved.Value!, purpose, context, ct);
        if (response.IsFailure) return response;
        await AuditAsync(context, usuario, "LOGIN_SSO", "OK", $"SSO {info.Proveedor}");
        return response;
    }

    private async Task<Result<Usuario>> ProvisionarPorDominioAsync(ExternalLoginInfo info, string email, CancellationToken ct)
    {
        var policy = await SsoIdentityPolicy.ResolveAsync(_db, info, ct: ct);
        if (policy.IsFailure) return Result<Usuario>.Fail(policy.Error!, policy.ErrorCode);
        var config = policy.Value!;
        if (!config.AutoProvisionar || config.RolPorDefectoId is null)
            return Result<Usuario>.Fail("No hay una cuenta asociada a este correo. Contacta al administrador.", "SSO_SIN_CUENTA");

        var rol = await _db.Roles
            .Include(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(r => r.Id == config.RolPorDefectoId.Value, ct);
        if (rol is null || !RbacSecurity.CanAssignToTenant(rol, config.EmpresaId))
            return Result<Usuario>.Fail("El rol de SSO no es válido para esta empresa.", "SSO_ROL_INVALIDO");

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
            SsoIssuer = info.Issuer,
            SsoSubject = info.Subject,
            CreatedBy = "SSO",
            Roles = new List<UsuarioRol> { new() { RolId = rol.Id, Rol = rol } },
        };
        return Result<Usuario>.Ok(usuario);
    }

    public async Task<Result<LoginResponse>> VincularExternoAsync(ExternalLoginInfo info, LoginRequest proof, AuthContext context, CancellationToken ct = default)
    {
        var policy = await SsoIdentityPolicy.ResolveAsync(_db, info, ct: ct);
        if (policy.IsFailure) return Result<LoginResponse>.Fail(policy.Error!, policy.ErrorCode);
        var email = info.Email?.Trim().ToLowerInvariant();
        var login = proof.UsernameOrEmail?.Trim();
        var user = await _db.Usuarios.Include(u => u.Roles).ThenInclude(r => r.Rol)
            .ThenInclude(r => r.Permisos).ThenInclude(p => p.Permiso)
            .SingleOrDefaultAsync(u => u.EmpresaId == policy.Value!.EmpresaId && u.Email.ToLower() == email
                && (u.Email == login || u.Username == login), ct);
        if (user is null || string.IsNullOrWhiteSpace(proof.Password))
            return Result<LoginResponse>.Fail("No se pudo verificar la cuenta local.", "AUTH_INVALID_CREDENTIALS");
        if (user.EstadoCodigo != EstadoCodes.Activo || user.BloqueadoHasta > DateTime.UtcNow)
            return Result<LoginResponse>.Fail("Usuario inactivo o bloqueado. Usa el inicio de sesión local.", "AUTH_USER_INACTIVE");
        if (!_passwordHasher.Verify(proof.Password, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(user, ct);
            await AuditAsync(context, user, "SSO_LINK", "FAIL", "Prueba local inválida");
            return Result<LoginResponse>.Fail("No se pudo verificar la cuenta local.", "AUTH_INVALID_CREDENTIALS");
        }
        if (user.MfaHabilitado)
        {
            if (string.IsNullOrWhiteSpace(proof.MfaCode))
                return Result<LoginResponse>.Fail("Introduce tu segundo factor local.", "AUTH_MFA_REQUIRED");
            var mfa = await _mfa.VerificarCodigoLoginAsync(user.Id, proof.MfaCode, ct);
            if (mfa.IsFailure)
            {
                await RegisterFailedAttemptAsync(user, ct);
                return Result<LoginResponse>.Fail("Código de segundo factor inválido.", "AUTH_MFA_INVALID");
            }
        }
        // A linked identity is never silently replaced, even when another identity has the same email.
        if (user.SsoIssuer is not null && (user.SsoIssuer != info.Issuer || user.SsoSubject != info.Subject || user.SsoProveedor != info.Proveedor)
            || await _db.Usuarios.AnyAsync(u => u.Id != user.Id && u.SsoProveedor == info.Proveedor && u.SsoIssuer == info.Issuer && u.SsoSubject == info.Subject, ct))
            return Result<LoginResponse>.Fail("La cuenta ya tiene otra identidad vinculada. Contacta al administrador.", "SSO_LINK_CONFLICT");
        user.SsoProveedor = info.Proveedor;
        user.SsoIssuer = info.Issuer;
        user.SsoSubject = info.Subject;
        user.SecurityStamp = Guid.NewGuid();
        user.IntentosFallidos = 0;
        user.UpdatedAt = user.UltimoLogin = DateTime.UtcNow;
        user.UpdatedBy = "SSO_LINK";
        var resolved = await ResolveUserInfoAsync(user, user.EmpresaId, ct);
        if (resolved.IsFailure) return Result<LoginResponse>.Fail(resolved.Error!, resolved.ErrorCode);
        try
        {
            var response = await IssueSessionAsync(user, resolved.Value!, SessionClaims.Full, context, ct);
            if (response.IsSuccess) await AuditAsync(context, user, "SSO_LINK", "OK", "Identidad vinculada con prueba local");
            return response;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return Result<LoginResponse>.Fail("La vinculación cambió en otra solicitud. Inicia sesión de nuevo.", "SSO_LINK_CONFLICT");
        }
    }

    public async Task<Result<LoginResponse>> RefreshAsync(string refreshToken, AuthContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<LoginResponse>.Fail("Refresh token requerido.", "AUTH_BAD_INPUT");
        }

        var refreshTokenHash = HashRefreshToken(refreshToken);
        var existing = await _db.RefreshTokens
            .Include(t => t.Usuario)
                .ThenInclude(u => u.Roles).ThenInclude(ur => ur.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            // Compatibilidad temporal con sesiones emitidas antes del hashing. Al rotarse,
            // el token nuevo ya queda almacenado exclusivamente como hash.
            .FirstOrDefaultAsync(t => t.Token == refreshTokenHash || t.Token == refreshToken, ct);

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

        // Las filas previas no permiten distinguir empresa seleccionada de empresa principal.
        // Exigir login evita renovar silenciosamente una sesión en el tenant equivocado.
        if (!existing.ContextInitialized)
            return Result<LoginResponse>.Fail("La sesión debe actualizarse. Inicia sesión nuevamente.", "AUTH_REFRESH_CONTEXT_REQUIRED");

        if (existing.SessionId is not Guid sessionId)
            return InvalidSession();
        var resolved = await new AuthSessionService(_db).ValidateAsync(sessionId, ct);
        if (resolved.IsFailure)
            return Result<LoginResponse>.Fail(resolved.Error!, resolved.ErrorCode);
        var userInfo = resolved.Value!;
        if (userInfo.Id != usuario.Id || userInfo.SessionPurpose != SessionClaims.Full
            || userInfo.EmpresaId != existing.ContextEmpresaId)
            return InvalidSession();
        var (accessToken, accessExpires) = _jwt.CreateAccessToken(userInfo);
        var newRefresh = _jwt.CreateRefreshToken();
        var refreshExpires = userInfo.SessionExpiresAt;

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = context.IpAddress;
        existing.RevokedReason = "Replaced";
        existing.ReplacedByToken = HashRefreshToken(newRefresh);

        var replacement = _db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            SessionId = sessionId,
            ContextEmpresaId = userInfo.EmpresaId,
            ContextInitialized = true,
            Token = HashRefreshToken(newRefresh),
            ExpiresAt = refreshExpires,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = context.IpAddress,
        });

        try
        {
            // RevokedAt es token de concurrencia: solo una petición puede rotar esta sesión.
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            replacement.State = EntityState.Detached;
            _db.Entry(existing).State = EntityState.Detached;
            return Result<LoginResponse>.Fail("Refresh token ya utilizado. Inicia sesión nuevamente.", "AUTH_REFRESH_INVALID");
        }
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
        // Revoke the parent session even when its refresh token was already rotated.
        // A Web logout has no refresh token; its session ID comes from the validated cookie.
        var sessionId = context.SessionId;
        if (sessionId is null && !string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = HashRefreshToken(refreshToken);
            sessionId = await _db.RefreshTokens.AsNoTracking()
                .Where(t => t.Token == hash || t.Token == refreshToken)
                .Select(t => t.SessionId).FirstOrDefaultAsync(ct);
        }
        if (sessionId is Guid id)
        {
            var session = await _db.AuthSessions.Include(s => s.Usuario).SingleOrDefaultAsync(s => s.Id == id, ct);
            if (session is not null && session.RevokedAt is null)
            {
                session.RevokedAt = DateTime.UtcNow;
                try
                {
                    await _db.SaveChangesAsync(ct);
                    await AuditAsync(context, session.Usuario, "LOGOUT", "OK", "Sesión revocada");
                }
                catch (DbUpdateConcurrencyException) { _db.Entry(session).State = EntityState.Detached; }
            }
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var refreshTokenHash = HashRefreshToken(refreshToken);
            var existing = await _db.RefreshTokens
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Token == refreshTokenHash || t.Token == refreshToken, ct);

            if (existing is not null && existing.RevokedAt is null
                && (context.SessionId is null || existing.SessionId == context.SessionId))
            {
                existing.RevokedAt = DateTime.UtcNow;
                existing.RevokedByIp = context.IpAddress;
                existing.RevokedReason = "Logout";
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Logout es idempotente si otra petición ya revocó o rotó el token.
                    _db.Entry(existing).State = EntityState.Detached;
                    return Result.Ok();
                }
                if (sessionId is null)
                    await AuditAsync(context, existing.Usuario, "LOGOUT", "OK", "Logout exitoso");
            }
        }

        return Result.Ok();
    }

    public async Task<Result<LoginResponse>> VerifyMfaChallengeAsync(string code, AuthContext context, CancellationToken ct = default)
    {
        if (context.SessionId is not Guid id || string.IsNullOrWhiteSpace(code) || code.Length > 32)
            return InvalidSession();
        var verified = await new AuthSessionService(_db).ValidateAsync(id, ct);
        if (verified.IsFailure || verified.Value!.SessionPurpose != SessionClaims.MfaVerify)
            return InvalidSession();
        var challenge = await _db.AuthSessions.Include(s => s.Usuario)
            .ThenInclude(u => u.Roles).ThenInclude(r => r.Rol).ThenInclude(r => r.Permisos).ThenInclude(p => p.Permiso)
            .SingleAsync(s => s.Id == id, ct);
        if (challenge.RevokedAt is not null) return InvalidSession();
        var usuario = challenge.Usuario;
        var mfa = await _mfa.VerificarCodigoLoginAsync(usuario.Id, code.Trim(), ct);
        if (mfa.IsFailure)
        {
            await RegisterFailedAttemptAsync(usuario, ct);
            await AuditAsync(context, usuario, "MFA_VERIFY", "FAIL", "Código MFA inválido");
            return Result<LoginResponse>.Fail("Código de segundo factor inválido.", "AUTH_MFA_INVALID");
        }
        var resolved = await ResolveUserInfoAsync(usuario, challenge.EmpresaId, ct);
        if (resolved.IsFailure) return Result<LoginResponse>.Fail(resolved.Error!, resolved.ErrorCode);

        challenge.RevokedAt = DateTime.UtcNow;
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoLogin = DateTime.UtcNow;
        try
        {
            // Session consumption and issuance share one EF SaveChanges transaction.
            var response = await IssueSessionAsync(usuario, resolved.Value!, SessionClaims.Full, context, ct);
            if (response.IsFailure) return response;
            await AuditAsync(context, usuario, "MFA_VERIFY", "OK", "Segundo factor verificado");
            return response;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return InvalidSession();
        }
    }

    private async Task<Result<LoginResponse>> IssueSessionAsync(Usuario usuario, UserInfo info, string purpose, AuthContext context, CancellationToken ct)
    {
        var expires = purpose == SessionClaims.Full
            ? DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays) : DateTime.UtcNow.AddMinutes(10);
        var session = AuthSessionService.Create(usuario, info, purpose, expires);
        _db.AuthSessions.Add(session);
        var refresh = string.Empty;
        if (purpose == SessionClaims.Full)
        {
            refresh = _jwt.CreateRefreshToken();
            _db.RefreshTokens.Add(new RefreshToken
            {
                UsuarioId = usuario.Id, SessionId = session.Id,
                ContextEmpresaId = info.EmpresaId, ContextInitialized = true,
                Token = HashRefreshToken(refresh), ExpiresAt = expires,
                CreatedByIp = context.IpAddress
            });
        }
        var (token, tokenExpires) = _jwt.CreateAccessToken(info);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { _db.ChangeTracker.Clear(); return InvalidSession(); }
        return Result<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = token, AccessTokenExpiresAt = tokenExpires,
            RefreshToken = refresh, RefreshTokenExpiresAt = expires, User = info,
            MfaEnrollmentRequired = purpose == SessionClaims.MfaEnroll,
            MfaVerificationRequired = purpose == SessionClaims.MfaVerify
        });
    }

    private static Result<LoginResponse> InvalidSession() => Result<LoginResponse>.Fail(
        "Tu sesión venció o cambió su autorización. Inicia sesión nuevamente.", "AUTH_SESSION_INVALID");

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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

        if (usuario.EstadoCodigo != EstadoCodes.Activo)
            return Result<UserInfo>.Fail("Usuario inactivo o bloqueado.", "AUTH_USER_INACTIVE");
        if (_currentUser is not null
            && (!_currentUser.IsAuthenticated || _currentUser.UserId != userId))
            return Result<UserInfo>.Fail("Sesión inválida.", "AUTH_INVALID_CONTEXT");

        if (_currentUser?.SessionId is Guid sessionId)
        {
            var session = await new AuthSessionService(_db).ValidateAsync(sessionId, ct);
            if (session.IsSuccess && session.Value!.Id != userId)
                return Result<UserInfo>.Fail("Sesión inválida.", "AUTH_INVALID_CONTEXT");
            return session;
        }

        return await ResolveUserInfoAsync(usuario, _currentUser is null ? usuario.EmpresaId : _currentUser.EmpresaId, ct);
    }

    private async Task RegisterFailedAttemptAsync(Usuario usuario, CancellationToken ct)
    {
        // CAS retry: concurrent wrong codes must not overwrite the account counter.
        for (var attempt = 0; ; attempt++)
        {
            if (usuario.EstadoCodigo != EstadoCodes.Activo || usuario.BloqueadoHasta > DateTime.UtcNow) return;
            usuario.IntentosFallidos++;
            if (_lockout.MaxFailedAttempts > 0 && usuario.IntentosFallidos >= _lockout.MaxFailedAttempts)
            {
                usuario.BloqueadoHasta = DateTime.UtcNow.AddMinutes(_lockout.LockoutMinutes);
                usuario.EstadoCodigo = EstadoCodes.Bloqueado;
                usuario.SecurityStamp = Guid.NewGuid();
            }
            try { await _db.SaveChangesAsync(ct); return; }
            catch (DbUpdateConcurrencyException) when (attempt < 10)
            {
                await _db.Entry(usuario).ReloadAsync(ct);
            }
        }
    }

    private Task<Result<UserInfo>> ResolveUserInfoAsync(Usuario usuario, int? empresaId, CancellationToken ct)
        => SessionUserInfoFactory.ResolveAsync(_db, usuario, empresaId, ct);

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
