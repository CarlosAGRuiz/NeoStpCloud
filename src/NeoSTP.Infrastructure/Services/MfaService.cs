using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Ops;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Auth;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Gestión del segundo factor TOTP por usuario. Ver <see cref="IMfaService"/>.
/// Secreto cifrado con DataProtection; códigos de recuperación guardados como hash SHA-256.
/// </summary>
public class MfaService : IMfaService
{
    private const string Issuer = "NeoSTP Cloud";
    private const int RecoveryCount = 10;
    private const string AuditModule = "HARDENING";

    private readonly NeoStpDbContext _db;
    private readonly ITotpService _totp;
    private readonly ISecretProtector _protector;
    private readonly IAuditoriaService _auditoria;

    public MfaService(NeoStpDbContext db, ITotpService totp, ISecretProtector protector, IAuditoriaService auditoria)
    {
        _db = db;
        _totp = totp;
        _protector = protector;
        _auditoria = auditoria;
    }

    public async Task<Result<MfaEnrollDto>> IniciarEnrolamientoAsync(int userId, CancellationToken ct = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null)
            return Result<MfaEnrollDto>.Fail("Usuario no encontrado.", "AUTH_USER_NOT_FOUND");

        if (u.MfaHabilitado)
            return Result<MfaEnrollDto>.Fail("MFA ya está activo. Para cambiarlo, deshabilítalo con tu código actual.", "MFA_ALREADY_ENABLED");

        var secret = _totp.GenerarSecreto();
        u.MfaSecretoCifrado = _protector.Protect(secret);
        u.MfaHabilitado = false;      // queda pendiente hasta confirmar
        u.MfaConfirmadoAt = null;
        if (!await SaveMfaAsync(u, ct))
            return Result<MfaEnrollDto>.Fail(ConcurrentMessage, "MFA_CONCURRENT_CHANGE");

        return Result<MfaEnrollDto>.Ok(new MfaEnrollDto
        {
            Secret = secret,
            OtpAuthUri = _totp.BuildOtpAuthUri(secret, u.Email, Issuer),
        });
    }

    public async Task<Result<MfaConfirmDto>> ConfirmarEnrolamientoAsync(int userId, string code, AuthContext? ctx = null, CancellationToken ct = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null)
            return Result<MfaConfirmDto>.Fail("Usuario no encontrado.", "AUTH_USER_NOT_FOUND");
        if (u.MfaHabilitado)
            return Result<MfaConfirmDto>.Fail("MFA ya está confirmado.", "MFA_ALREADY_ENABLED");
        if (string.IsNullOrWhiteSpace(u.MfaSecretoCifrado))
            return Result<MfaConfirmDto>.Fail("No hay enrolamiento iniciado.", "MFA_NO_ENROLLMENT");

        var secret = _protector.Unprotect(u.MfaSecretoCifrado);
        if (!_totp.Validar(secret, code))
            return Result<MfaConfirmDto>.Fail("Código inválido.", "AUTH_MFA_INVALID");

        var recovery = GenerarRecoveryCodes();
        u.MfaRecoveryCodesJson = JsonSerializer.Serialize(recovery.Select(Hash).ToList());
        u.MfaHabilitado = true;
        u.SecurityStamp = Guid.NewGuid();
        u.MfaConfirmadoAt = DateTime.UtcNow;
        if (!await SaveMfaAsync(u, ct))
            return Result<MfaConfirmDto>.Fail(ConcurrentMessage, "MFA_CONCURRENT_CHANGE");

        await Audit(ctx, u.Id, u.Username, u.EmpresaId, "MFA_ENABLE", "OK", "Segundo factor activado");
        return Result<MfaConfirmDto>.Ok(new MfaConfirmDto { RecoveryCodes = recovery });
    }

    public async Task<Result> DeshabilitarAsync(int userId, string code, AuthContext? ctx = null, CancellationToken ct = default)
    {
        var u = await _db.Usuarios.Include(x => x.Roles).ThenInclude(x => x.Rol)
            .FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null)
            return Result.Fail("Usuario no encontrado.", "AUTH_USER_NOT_FOUND");
        if (await RbacSecurity.IsMfaRequiredUserAsync(_db, u, ct))
            return Result.Fail("El segundo factor es obligatorio para usuarios administradores.", "MFA_REQUIRED_FOR_ADMIN");
        if (!u.MfaHabilitado || string.IsNullOrWhiteSpace(u.MfaSecretoCifrado))
            return Result.Fail("MFA no está habilitado.", "MFA_NOT_ENABLED");

        var secret = _protector.Unprotect(u.MfaSecretoCifrado);
        if (!_totp.Validar(secret, code) && !ConsumirRecovery(u, code))
            return Result.Fail("Código inválido.", "AUTH_MFA_INVALID");

        u.MfaHabilitado = false;
        u.MfaSecretoCifrado = null;
        u.MfaConfirmadoAt = null;
        u.MfaRecoveryCodesJson = null;
        u.SecurityStamp = Guid.NewGuid();
        if (!await SaveMfaAsync(u, ct))
            return Result.Fail(ConcurrentMessage, "MFA_CONCURRENT_CHANGE");

        await Audit(ctx, u.Id, u.Username, u.EmpresaId, "MFA_DISABLE", "OK", "Segundo factor desactivado");
        return Result.Ok();
    }

    public async Task<Result> VerificarCodigoLoginAsync(int userId, string code, CancellationToken ct = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null)
            return Result.Fail("Usuario no encontrado.", "AUTH_USER_NOT_FOUND");
        if (!u.MfaHabilitado)
            return Result.Ok(); // nada que verificar
        if (string.IsNullOrWhiteSpace(u.MfaSecretoCifrado))
            return Result.Fail("No se puede validar el segundo factor. Contacta a soporte.", "MFA_CONFIGURATION_INVALID");

        var secret = _protector.Unprotect(u.MfaSecretoCifrado);
        if (_totp.Validar(secret, code))
            return Result.Ok();

        if (ConsumirRecovery(u, code))
        {
            if (!await SaveMfaAsync(u, ct))
                return Result.Fail(ConcurrentMessage, "MFA_CONCURRENT_CHANGE");
            return Result.Ok();
        }

        return Result.Fail("Código MFA inválido.", "AUTH_MFA_INVALID");
    }

    // -- helpers ----------------------------------------------------------

    private const string ConcurrentMessage = "La configuración MFA cambió en otra solicitud. Vuelve a intentarlo con un código vigente.";

    private async Task<bool> SaveMfaAsync(Domain.Core.Seguridad.Usuario user, CancellationToken ct)
    {
        user.MfaVersion = Guid.NewGuid();
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Never return the losing secret/recovery set or leave its changes pending.
            await _db.Entry(user).ReloadAsync(ct);
            return false;
        }
    }

    private bool ConsumirRecovery(Domain.Core.Seguridad.Usuario u, string code)
    {
        if (string.IsNullOrWhiteSpace(u.MfaRecoveryCodesJson) || string.IsNullOrWhiteSpace(code))
            return false;

        var hashes = JsonSerializer.Deserialize<List<string>>(u.MfaRecoveryCodesJson) ?? new();
        var target = Hash(code);
        var idx = hashes.FindIndex(h => CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(h), Encoding.ASCII.GetBytes(target)));
        if (idx < 0)
            return false;

        hashes.RemoveAt(idx);
        u.MfaRecoveryCodesJson = JsonSerializer.Serialize(hashes);
        return true;
    }

    private static List<string> GenerarRecoveryCodes()
    {
        var codes = new List<string>(RecoveryCount);
        for (var i = 0; i < RecoveryCount; i++)
        {
            // 10 caracteres hex en bloques de 5: XXXXX-XXXXX
            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToUpperInvariant();
            codes.Add($"{raw[..5]}-{raw[5..]}");
        }
        return codes;
    }

    private static string Hash(string code)
    {
        var normal = code.Replace("-", "").Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normal)));
    }

    private Task Audit(AuthContext? ctx, int userId, string username, int? empresaId, string accion, string resultado, string detalle)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            UsuarioId = userId,
            Username = username,
            Modulo = AuditModule,
            Accion = accion,
            Resultado = resultado,
            Detalle = detalle,
            IpAddress = ctx?.IpAddress,
            UserAgent = ctx?.UserAgent,
            TraceId = ctx?.TraceId,
        });
}
