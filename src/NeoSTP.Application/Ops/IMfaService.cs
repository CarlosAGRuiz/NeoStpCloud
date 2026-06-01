using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Ops;

/// <summary>Datos para enrolar el segundo factor (se muestran una sola vez).</summary>
public sealed record MfaEnrollDto
{
    public string Secret { get; init; } = null!;
    public string OtpAuthUri { get; init; } = null!;
}

/// <summary>Resultado de confirmar el enrolamiento: códigos de recuperación en claro (una vez).</summary>
public sealed record MfaConfirmDto
{
    public IReadOnlyList<string> RecoveryCodes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Gestión del segundo factor (TOTP) por usuario. El secreto se almacena cifrado;
/// los códigos de recuperación se guardan hasheados. Obligatorio para SuperAdmin.
/// </summary>
public interface IMfaService
{
    /// <summary>Genera y persiste (cifrado) un secreto pendiente de confirmar; devuelve el URI para QR.</summary>
    Task<Result<MfaEnrollDto>> IniciarEnrolamientoAsync(int userId, CancellationToken ct = default);

    /// <summary>Valida el primer código TOTP, activa MFA y entrega los códigos de recuperación.</summary>
    Task<Result<MfaConfirmDto>> ConfirmarEnrolamientoAsync(int userId, string code, AuthContext? ctx = null, CancellationToken ct = default);

    /// <summary>Deshabilita MFA tras validar un código vigente.</summary>
    Task<Result> DeshabilitarAsync(int userId, string code, AuthContext? ctx = null, CancellationToken ct = default);

    /// <summary>
    /// Verifica un código de login (TOTP o de recuperación). Consume el código de
    /// recuperación si fue usado. Devuelve fallo con código <c>AUTH_MFA_INVALID</c>.
    /// </summary>
    Task<Result> VerificarCodigoLoginAsync(int userId, string code, CancellationToken ct = default);
}
