using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Integration;

/// <summary>
/// Sprint 20.5 — smoke crítico de hardening que compone varios servicios reales
/// sobre un mismo DbContext: defaults seguros (fail-open) y ciclo MFA completo.
/// </summary>
public class HardeningSmokeTests
{
    private sealed class PassthroughProtector : ISecretProtector
    {
        public string Protect(string p) => p;
        public string Unprotect(string c) => c;
        public string? ProtectOrNull(string? p) => p;
        public string? UnprotectOrNull(string? c) => c;
    }

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"smoke-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task DefaultsSeguros_SinReglas_PermitenOperar()
    {
        using var db = NewDb();
        var audit = Substitute.For<IAuditoriaService>();

        var quotas = new ApiQuotaService(db, audit, NullLogger<ApiQuotaService>.Instance);
        var allowlist = new AdminIpAllowlistService(db, audit);

        // Sin cuotas configuradas: se permite.
        var decision = await quotas.EvaluarAsync(new QuotaContext { EmpresaId = 1, UsuarioId = 1, Modulo = "NEODTE" });
        decision.Allowed.Should().BeTrue();

        // Lista blanca vacía: cualquier IP de SuperAdmin permitida (fail-open).
        (await allowlist.EstaPermitidaAsync("203.0.113.99")).Should().BeTrue();
    }

    [Fact]
    public async Task CicloMfaCompleto_EnrolarConfirmarVerificar()
    {
        using var db = NewDb();
        db.Usuarios.Add(new Usuario
        {
            Id = 1, Username = "superadmin", Email = "sa@neostp.local",
            NombreCompleto = "SA", PasswordHash = "x", TipoUsuarioCodigo = "SUPERADMIN",
        });
        await db.SaveChangesAsync();

        var totp = new TotpService();
        var mfa = new MfaService(db, totp, new PassthroughProtector(), Substitute.For<IAuditoriaService>());

        var enroll = await mfa.IniciarEnrolamientoAsync(1);
        enroll.IsSuccess.Should().BeTrue();

        var code = totp.GenerarCodigo(enroll.Value!.Secret, DateTimeOffset.UtcNow);
        var confirm = await mfa.ConfirmarEnrolamientoAsync(1, code);
        confirm.IsSuccess.Should().BeTrue();
        confirm.Value!.RecoveryCodes.Should().HaveCount(10);

        var loginCode = totp.GenerarCodigo(enroll.Value!.Secret, DateTimeOffset.UtcNow);
        (await mfa.VerificarCodigoLoginAsync(1, loginCode)).IsSuccess.Should().BeTrue();
    }
}
