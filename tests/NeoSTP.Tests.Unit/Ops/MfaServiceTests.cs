using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>
/// Sprint 20.3 — enrolamiento, confirmación, verificación de login (TOTP y códigos
/// de recuperación) y deshabilitación del segundo factor.
/// </summary>
public class MfaServiceTests
{
    /// <summary>Protector identidad para pruebas (no cifra realmente).</summary>
    private sealed class PassthroughProtector : ISecretProtector
    {
        public string Protect(string p) => p;
        public string Unprotect(string c) => c;
        public string? ProtectOrNull(string? p) => p;
        public string? UnprotectOrNull(string? c) => c;
    }

    private readonly TotpService _totp = new();

    private (MfaService svc, NeoStpDbContext db) Build()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"mfa-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Usuarios.Add(new Usuario
        {
            Id = 1, Username = "admin", Email = "admin@neostp.local",
            NombreCompleto = "Admin", PasswordHash = "x", TipoUsuarioCodigo = "SUPERADMIN",
        });
        db.SaveChanges();
        var audit = Substitute.For<IAuditoriaService>();
        return (new MfaService(db, _totp, new PassthroughProtector(), audit), db);
    }

    [Fact]
    public async Task IniciarEnrolamiento_GeneraSecretoYDejaMfaPendiente()
    {
        var (svc, db) = Build();
        var r = await svc.IniciarEnrolamientoAsync(1);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Secret.Should().NotBeNullOrEmpty();
        r.Value.OtpAuthUri.Should().Contain("otpauth://");

        var u = await db.Usuarios.AsNoTracking().SingleAsync();
        u.MfaSecretoCifrado.Should().NotBeNullOrEmpty();
        u.MfaHabilitado.Should().BeFalse("aún no se confirma el enrolamiento");
    }

    [Fact]
    public async Task ConfirmarEnrolamiento_CodigoValido_ActivaMfaYEntregaRecoveryCodes()
    {
        var (svc, db) = Build();
        var enroll = await svc.IniciarEnrolamientoAsync(1);
        var code = _totp.GenerarCodigo(enroll.Value!.Secret, DateTimeOffset.UtcNow);

        var r = await svc.ConfirmarEnrolamientoAsync(1, code);

        r.IsSuccess.Should().BeTrue();
        r.Value!.RecoveryCodes.Should().HaveCount(10);
        var u = await db.Usuarios.AsNoTracking().SingleAsync();
        u.MfaHabilitado.Should().BeTrue();
        u.MfaConfirmadoAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConfirmarEnrolamiento_CodigoInvalido_Falla()
    {
        var (svc, _) = Build();
        await svc.IniciarEnrolamientoAsync(1);

        var r = await svc.ConfirmarEnrolamientoAsync(1, "000000");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("AUTH_MFA_INVALID");
    }

    [Fact]
    public async Task VerificarCodigoLogin_TotpValido_Ok()
    {
        var (svc, _) = Build();
        var enroll = await svc.IniciarEnrolamientoAsync(1);
        var code = _totp.GenerarCodigo(enroll.Value!.Secret, DateTimeOffset.UtcNow);
        await svc.ConfirmarEnrolamientoAsync(1, code);

        var code2 = _totp.GenerarCodigo(enroll.Value!.Secret, DateTimeOffset.UtcNow);
        var r = await svc.VerificarCodigoLoginAsync(1, code2);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task VerificarCodigoLogin_RecoveryCode_OkYSeConsume()
    {
        var (svc, _) = Build();
        var enroll = await svc.IniciarEnrolamientoAsync(1);
        var code = _totp.GenerarCodigo(enroll.Value!.Secret, DateTimeOffset.UtcNow);
        var confirm = await svc.ConfirmarEnrolamientoAsync(1, code);
        var recovery = confirm.Value!.RecoveryCodes[0];

        var first = await svc.VerificarCodigoLoginAsync(1, recovery);
        first.IsSuccess.Should().BeTrue();

        // El mismo código de recuperación no debe servir dos veces.
        var second = await svc.VerificarCodigoLoginAsync(1, recovery);
        second.IsFailure.Should().BeTrue();
        second.ErrorCode.Should().Be("AUTH_MFA_INVALID");
    }

    [Fact]
    public async Task VerificarCodigoLogin_SinMfa_DevuelveOk()
    {
        var (svc, _) = Build();
        // Usuario nunca enroló MFA.
        var r = await svc.VerificarCodigoLoginAsync(1, "whatever");
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Deshabilitar_CodigoValido_LimpiaSecreto()
    {
        var (svc, db) = Build();
        var enroll = await svc.IniciarEnrolamientoAsync(1);
        var code = _totp.GenerarCodigo(enroll.Value!.Secret, DateTimeOffset.UtcNow);
        await svc.ConfirmarEnrolamientoAsync(1, code);

        var code2 = _totp.GenerarCodigo(enroll.Value!.Secret, DateTimeOffset.UtcNow);
        var r = await svc.DeshabilitarAsync(1, code2);

        r.IsSuccess.Should().BeTrue();
        var u = await db.Usuarios.AsNoTracking().SingleAsync();
        u.MfaHabilitado.Should().BeFalse();
        u.MfaSecretoCifrado.Should().BeNull();
        u.MfaRecoveryCodesJson.Should().BeNull();
    }
}
