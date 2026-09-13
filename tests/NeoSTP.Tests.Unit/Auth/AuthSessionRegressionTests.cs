using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Auth;

public class AuthSessionRegressionTests
{
    private sealed class Fixture : IDisposable
    {
        public DbContextOptions<NeoStpDbContext> Options { get; } = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"auth-session-{Guid.NewGuid()}").Options;
        public NeoStpDbContext Db { get; }
        public IJwtTokenService Jwt { get; } = Substitute.For<IJwtTokenService>();
        public IPasswordHasher Hasher { get; } = Substitute.For<IPasswordHasher>();
        public IMfaService Mfa { get; } = Substitute.For<IMfaService>();
        public Usuario User => Db.Usuarios.Local.Single();
        public Rol MemberRole => Db.Roles.Local.Single(r => r.Id == 2);

        public Fixture()
        {
            Db = new NeoStpDbContext(Options);
            Db.Empresas.AddRange(
                new Empresa { Id = 101, Nit = "TEST-A", RazonSocial = "Principal", EstadoCodigo = "ACTIVA" },
                new Empresa { Id = 202, Nit = "TEST-B", RazonSocial = "Invitante", EstadoCodigo = "ACTIVA" });
            var homeRole = new Rol { Id = 1, EmpresaId = 101, Codigo = "ADMIN_EMPRESA", Nombre = "Admin", Activo = true };
            var memberRole = new Rol { Id = 2, EmpresaId = 202, Codigo = "CONTADOR", Nombre = "Contador", Activo = true };
            homeRole.Permisos.Add(new RolPermiso { Permiso = new Permiso { Id = 1, Codigo = "Usuarios.Editar", Modulo = "CORE", Descripcion = "Editar" } });
            memberRole.Permisos.Add(new RolPermiso { Permiso = new Permiso { Id = 2, Codigo = "Reportes.Ver", Modulo = "NEOBI", Descripcion = "Ver" } });
            Db.Roles.AddRange(homeRole, memberRole);
            Db.Usuarios.Add(new Usuario
            {
                Id = 1, EmpresaId = 101, Username = "tester", Email = "tester@example.test",
                NombreCompleto = "Tester", PasswordHash = "hash", TipoUsuarioCodigo = "ADMIN", EstadoCodigo = EstadoCodes.Activo,
                MfaHabilitado = true, MfaSecretoCifrado = "synthetic-test-secret",
                Roles = new List<UsuarioRol> { new() { Rol = homeRole } }
            });
            Db.UsuarioEmpresas.Add(new UsuarioEmpresa { UsuarioId = 1, EmpresaId = 202, RolId = 2, EstadoCodigo = EstadoCodes.Activo });
            SsoTestIdentity.Configure(Db, 101);
            Db.SaveChanges();
            Hasher.Verify("valid", "hash").Returns(true);
            Mfa.VerificarCodigoLoginAsync(1, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(x => (string)x[1] == "123456" ? Result.Ok() : Result.Fail("Inválido", "AUTH_MFA_INVALID"));
            Jwt.CreateAccessToken(Arg.Any<UserInfo>()).Returns(("test.jwt", DateTime.UtcNow.AddHours(1)));
            Jwt.CreateRefreshToken().Returns(_ => Guid.NewGuid().ToString("N"));
        }

        public AuthService Service(ICurrentUser? actor = null, NeoStpDbContext? db = null) =>
            new(db ?? Db, Hasher, Jwt, Substitute.For<IAuditoriaService>(), Mfa,
                Microsoft.Extensions.Options.Options.Create(new JwtOptions { RefreshTokenExpiryDays = 14 }),
                Microsoft.Extensions.Options.Options.Create(new SecurityOptions()),
                NullLogger<AuthService>.Instance, actor);

        public Task<Result<LoginResponse>> Login(string password = "valid", string? mfa = "123456") =>
            Service().LoginAsync(new LoginRequest { UsernameOrEmail = "tester", Password = password, MfaCode = mfa }, new AuthContext());

        public async Task<AuthContext> Context() => new() { SessionId = (await Login()).Value!.User.SessionId };

        public ICurrentUser Actor(int? empresaId)
        {
            var actor = Substitute.For<ICurrentUser>();
            actor.IsAuthenticated.Returns(true);
            actor.UserId.Returns(1);
            actor.EmpresaId.Returns(empresaId);
            return actor;
        }

        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task ExpiredTemporaryLock_AllowsValidLogin()
    {
        using var f = new Fixture();
        f.User.EstadoCodigo = EstadoCodes.Bloqueado;
        f.User.BloqueadoHasta = DateTime.UtcNow.AddMinutes(-5);
        f.User.IntentosFallidos = 5;
        await f.Db.SaveChangesAsync();

        (await f.Login()).IsSuccess.Should().BeTrue();
        f.User.EstadoCodigo.Should().Be(EstadoCodes.Activo);
        f.User.IntentosFallidos.Should().Be(0);
        f.User.BloqueadoHasta.Should().BeNull();
    }

    [Fact]
    public async Task ExpiredTemporaryLock_WrongPasswordStartsNewWindow()
    {
        using var f = new Fixture();
        f.User.EstadoCodigo = EstadoCodes.Bloqueado;
        f.User.BloqueadoHasta = DateTime.UtcNow.AddMinutes(-5);
        f.User.IntentosFallidos = 5;
        await f.Db.SaveChangesAsync();

        (await f.Login("wrong")).ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
        f.User.IntentosFallidos.Should().Be(1);
        f.User.EstadoCodigo.Should().Be(EstadoCodes.Activo);
    }

    [Theory]
    [InlineData("BLOQUEADO", false)]
    [InlineData("INACTIVO", false)]
    [InlineData("INACTIVO", true)]
    public async Task AdministrativeOrInactiveState_IsNeverAutomaticallyUnlocked(string state, bool expired)
    {
        using var f = new Fixture();
        f.User.EstadoCodigo = state;
        f.User.BloqueadoHasta = expired ? DateTime.UtcNow.AddMinutes(-5) : null;
        await f.Db.SaveChangesAsync();

        (await f.Login()).ErrorCode.Should().Be("AUTH_USER_INACTIVE");
        f.User.EstadoCodigo.Should().Be(state);
        f.Jwt.DidNotReceive().CreateAccessToken(Arg.Any<UserInfo>());
    }

    [Fact]
    public async Task FiveInvalidMfaCodes_LockUser_AndCorrectCodeCannotBypassLock()
    {
        using var f = new Fixture();
        f.User.MfaHabilitado = true;
        await f.Db.SaveChangesAsync();
        for (var i = 0; i < 5; i++)
            (await f.Login(mfa: "wrong")).ErrorCode.Should().Be("AUTH_MFA_INVALID");

        (await f.Login(mfa: "123456")).ErrorCode.Should().Be("AUTH_USER_LOCKED");
        f.User.IntentosFallidos.Should().Be(5);
        f.User.EstadoCodigo.Should().Be(EstadoCodes.Bloqueado);
        f.User.BloqueadoHasta.Should().BeAfter(DateTime.UtcNow);
        (await f.Db.RefreshTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PasswordAndMfaFailures_ShareCounter_AndSuccessResetsIt()
    {
        using var f = new Fixture();
        f.User.MfaHabilitado = true;
        await f.Db.SaveChangesAsync();
        await f.Login("wrong");
        await f.Login(mfa: "wrong");
        f.User.IntentosFallidos.Should().Be(2);
        (await f.Login(mfa: "123456")).IsSuccess.Should().BeTrue();
        f.User.IntentosFallidos.Should().Be(0);
    }

    [Fact]
    public async Task MissingMfa_DoesNotIssueSession_OrResetFailures()
    {
        using var f = new Fixture();
        f.User.MfaHabilitado = true;
        f.User.IntentosFallidos = 3;
        await f.Db.SaveChangesAsync();

        (await f.Login(mfa: "")).ErrorCode.Should().Be("AUTH_MFA_REQUIRED");
        f.User.IntentosFallidos.Should().Be(3);
        f.Jwt.DidNotReceive().CreateAccessToken(Arg.Any<UserInfo>());
    }

    [Fact]
    public async Task SwitchingAndRepeatedRefresh_KeepCompanyAndMembershipPermissions()
    {
        using var f = new Fixture();
        var service = f.Service();
        var login = await service.CambiarEmpresaAsync(1, 202, await f.Context());
        for (var i = 0; i < 3; i++)
        {
            login.IsSuccess.Should().BeTrue(login.Error);
            login.Value!.User.EmpresaId.Should().Be(202);
            login.Value.User.Roles.Should().BeEquivalentTo("CONTADOR");
            login.Value.User.TipoUsuarioCodigo.Should().Be("OPERADOR");
            login.Value.User.Permisos.Should().BeEquivalentTo("Reportes.Ver");
            login = await service.RefreshAsync(login.Value.RefreshToken, new AuthContext());
        }
        login.IsSuccess.Should().BeTrue(login.Error);
        f.Db.RefreshTokens.Should().OnlyContain(t => t.ContextInitialized);
        f.Db.RefreshTokens.Where(t => t.ContextEmpresaId == 202).Should().HaveCount(4);
    }

    [Fact]
    public async Task TwoSessions_KeepTheirOwnCompany()
    {
        using var f = new Fixture();
        var home = await f.Login();
        var other = await f.Service().CambiarEmpresaAsync(1, 202, new AuthContext { SessionId = home.Value!.User.SessionId });
        (await f.Service().RefreshAsync(home.Value!.RefreshToken, new AuthContext())).Value!.User.EmpresaId.Should().Be(101);
        (await f.Service().RefreshAsync(other.Value!.RefreshToken, new AuthContext())).Value!.User.EmpresaId.Should().Be(202);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyRefresh_RequiresNewLogin_EvenForGlobalUser(bool global)
    {
        using var f = new Fixture();
        if (global) f.User.EmpresaId = null;
        f.Db.RefreshTokens.Add(new RefreshToken { UsuarioId = 1, Token = "legacy", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await f.Db.SaveChangesAsync();

        (await f.Service().RefreshAsync("legacy", new AuthContext())).ErrorCode.Should().Be("AUTH_REFRESH_CONTEXT_REQUIRED");
        f.Jwt.DidNotReceive().CreateAccessToken(Arg.Any<UserInfo>());
        (await f.Db.RefreshTokens.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("revoked", "EMPRESA_NO_MEMBRESIA")]
    [InlineData("deleted", "EMPRESA_NO_MEMBRESIA")]
    [InlineData("inactive-role", "EMPRESA_NO_MEMBRESIA")]
    [InlineData("foreign-role", "EMPRESA_NO_MEMBRESIA")]
    [InlineData("reserved-role", "EMPRESA_NO_MEMBRESIA")]
    [InlineData("platform-permission", "EMPRESA_NO_MEMBRESIA")]
    [InlineData("suspended", "EMPRESA_SUSPENDIDA")]
    public async Task RefreshAndMe_RevalidateMembershipAndCompany(string change, string error)
    {
        using var f = new Fixture();
        var login = await f.Service().CambiarEmpresaAsync(1, 202, await f.Context());
        switch (change)
        {
            case "revoked": f.Db.UsuarioEmpresas.Local.Single().EstadoCodigo = "INACTIVO"; break;
            case "deleted": f.Db.UsuarioEmpresas.Remove(f.Db.UsuarioEmpresas.Local.Single()); break;
            case "inactive-role": f.MemberRole.Activo = false; break;
            case "foreign-role": f.MemberRole.EmpresaId = 101; break;
            case "reserved-role": f.MemberRole.Codigo = "SUPERADMIN"; break;
            case "platform-permission": f.MemberRole.Permisos.Single().Permiso.Codigo = "SuperAdmin.Planes.Administrar"; break;
            case "suspended": f.Db.Empresas.Local.Single(e => e.Id == 202).EstadoCodigo = "SUSPENDIDA"; break;
        }
        await f.Db.SaveChangesAsync();
        f.Jwt.ClearReceivedCalls();

        (await f.Service().RefreshAsync(login.Value!.RefreshToken, new AuthContext())).ErrorCode.Should().Be(error);
        (await f.Service(f.Actor(202)).GetCurrentUserInfoAsync(1)).ErrorCode.Should().Be(error);
        f.Jwt.DidNotReceive().CreateAccessToken(Arg.Any<UserInfo>());
        (await f.Db.RefreshTokens.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Me_UsesSelectedCompany_NotHome()
    {
        using var f = new Fixture();
        var result = await f.Service(f.Actor(202)).GetCurrentUserInfoAsync(1);
        result.Value!.EmpresaId.Should().Be(202);
        result.Value.Permisos.Should().BeEquivalentTo("Reportes.Ver");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Me_RejectsAnonymousOrDifferentActor(bool authenticated)
    {
        using var f = new Fixture();
        var actor = f.Actor(202);
        actor.IsAuthenticated.Returns(authenticated);
        if (authenticated) actor.UserId.Returns(99);
        (await f.Service(actor).GetCurrentUserInfoAsync(1)).ErrorCode.Should().Be("AUTH_INVALID_CONTEXT");
    }

    [Fact]
    public async Task Refresh_RejectsSuspendedHomeCompany()
    {
        using var f = new Fixture();
        var login = await f.Login();
        f.Db.Empresas.Local.Single(e => e.Id == 101).EstadoCodigo = "SUSPENDIDA";
        await f.Db.SaveChangesAsync();
        (await f.Service().RefreshAsync(login.Value!.RefreshToken, new AuthContext())).ErrorCode.Should().Be("EMPRESA_SUSPENDIDA");
    }

    [Fact]
    public async Task Refresh_ReusedTokenIsRejected()
    {
        using var f = new Fixture();
        var login = await f.Login();
        (await f.Service().RefreshAsync(login.Value!.RefreshToken, new AuthContext())).IsSuccess.Should().BeTrue();
        (await f.Service().RefreshAsync(login.Value.RefreshToken, new AuthContext())).ErrorCode.Should().Be("AUTH_REFRESH_INVALID");
    }

    [Fact]
    public async Task ConcurrentRefresh_StaleContextCannotRotateSameTokenTwice()
    {
        using var f = new Fixture();
        var login = await f.Login();
        using var staleDb = new NeoStpDbContext(f.Options);
        await staleDb.RefreshTokens.LoadAsync();
        (await f.Service().RefreshAsync(login.Value!.RefreshToken, new AuthContext())).IsSuccess.Should().BeTrue();
        (await f.Service(db: staleDb).RefreshAsync(login.Value.RefreshToken, new AuthContext()))
            .ErrorCode.Should().Be("AUTH_REFRESH_INVALID");
        f.Db.Model.FindEntityType(typeof(RefreshToken))!.FindProperty(nameof(RefreshToken.RevokedAt))!
            .IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_RequiresNewLogin_WhenPermissionsChange()
    {
        using var f = new Fixture();
        var login = await f.Service().CambiarEmpresaAsync(1, 202, await f.Context());
        f.MemberRole.Permisos.Clear();
        await f.Db.SaveChangesAsync();
        var refreshed = await f.Service().RefreshAsync(login.Value!.RefreshToken, new AuthContext());
        refreshed.ErrorCode.Should().Be("AUTH_SESSION_INVALID");
    }

    [Fact]
    public async Task TenantLogin_DoesNotMintPlatformOrForeignClaims_FromLegacyAssignments()
    {
        using var f = new Fixture();
        f.User.TipoUsuarioCodigo = "SUPERADMIN";
        var platformRole = f.Db.Roles.Add(new Rol { Id = 3, Codigo = "SUPERADMIN", Nombre = "Global", EsSistema = true, Activo = true }).Entity;
        f.Db.UsuarioRoles.Add(new UsuarioRol { UsuarioId = 1, Rol = platformRole });
        f.User.Roles.Add(new UsuarioRol { Rol = f.MemberRole });
        await f.Db.SaveChangesAsync();

        var login = await f.Login();
        login.IsSuccess.Should().BeTrue();
        login.Value!.User.TipoUsuarioCodigo.Should().Be("OPERADOR");
        login.Value.User.Roles.Should().BeEquivalentTo("ADMIN_EMPRESA");
        login.Value.User.Permisos.Should().NotContain("Reportes.Ver");
    }

    [Fact]
    public async Task PlatformLogin_PreservesLegitimateGlobalRole()
    {
        using var f = new Fixture();
        f.User.EmpresaId = null;
        f.User.TipoUsuarioCodigo = "SUPERADMIN";
        f.User.MfaHabilitado = true;
        var platformRole = f.Db.Roles.Add(new Rol { Id = 3, Codigo = "SUPERADMIN", Nombre = "Global", EsSistema = true, Activo = true }).Entity;
        f.Db.UsuarioRoles.Add(new UsuarioRol { UsuarioId = 1, Rol = platformRole });
        await f.Db.SaveChangesAsync();
        var login = await f.Login(mfa: "123456");
        login.Value!.User.TipoUsuarioCodigo.Should().Be("SUPERADMIN");
        login.Value.User.Roles.Should().BeEquivalentTo("SUPERADMIN");
        (await f.Service().RefreshAsync(login.Value.RefreshToken, new AuthContext())).Value!.User.EmpresaId.Should().BeNull();
    }

    [Fact]
    public async Task Logout_AfterConcurrentRevocation_IsIdempotent()
    {
        using var f = new Fixture();
        var login = await f.Login();
        using var staleDb = new NeoStpDbContext(f.Options);
        await staleDb.RefreshTokens.LoadAsync();

        (await f.Service().LogoutAsync(login.Value!.RefreshToken, new AuthContext())).IsSuccess.Should().BeTrue();
        (await f.Service(db: staleDb).LogoutAsync(login.Value.RefreshToken, new AuthContext())).IsSuccess.Should().BeTrue();
        (await f.Service().RefreshAsync(login.Value.RefreshToken, new AuthContext())).ErrorCode.Should().Be("AUTH_REFRESH_INVALID");
    }

    [Fact]
    public async Task IssuedSession_LinksRefresh_AndLogoutWithoutRefreshRevokesOnlyThatSession()
    {
        using var f = new Fixture();
        var first = (await f.Login()).Value!;
        var second = (await f.Login()).Value!;
        first.User.SessionId.Should().NotBeEmpty();
        f.Db.RefreshTokens.Should().Contain(t => t.SessionId == first.User.SessionId);
        (await new AuthSessionService(f.Db).ValidateAsync(first.User.SessionId)).IsSuccess.Should().BeTrue();
        await f.Service().LogoutAsync(null, new AuthContext { SessionId = first.User.SessionId });
        (await new AuthSessionService(f.Db).ValidateAsync(first.User.SessionId)).ErrorCode.Should().Be("AUTH_SESSION_INVALID");
        (await f.Service().RefreshAsync(first.RefreshToken, new AuthContext())).IsFailure.Should().BeTrue();
        (await f.Service().RefreshAsync(second.RefreshToken, new AuthContext())).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutUsingAlreadyRotatedRefresh_RevokesTheReplacementSession()
    {
        using var f = new Fixture();
        var first = (await f.Login()).Value!;
        var rotated = (await f.Service().RefreshAsync(first.RefreshToken, new AuthContext())).Value!;
        rotated.User.SessionId.Should().Be(first.User.SessionId);
        rotated.RefreshTokenExpiresAt.Should().Be(first.RefreshTokenExpiresAt);
        await f.Service().LogoutAsync(first.RefreshToken, new AuthContext());
        (await f.Service().RefreshAsync(rotated.RefreshToken, new AuthContext())).ErrorCode.Should().Be("AUTH_SESSION_INVALID");
    }

    [Fact]
    public async Task LogoutCannotTargetAnotherSessionByChangingBodyToken()
    {
        using var f = new Fixture();
        var first = (await f.Login()).Value!;
        var other = (await f.Login()).Value!;
        await f.Service().LogoutAsync(other.RefreshToken, new AuthContext { SessionId = first.User.SessionId });
        (await new AuthSessionService(f.Db).ValidateAsync(first.User.SessionId)).IsFailure.Should().BeTrue();
        (await f.Service().RefreshAsync(other.RefreshToken, new AuthContext())).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SwitchCompany_RejectsMissingSessionOrDifferentUser()
    {
        using var f = new Fixture();
        (await f.Service().CambiarEmpresaAsync(1, 202, new AuthContext())).ErrorCode.Should().Be("AUTH_SESSION_INVALID");
        (await f.Service().CambiarEmpresaAsync(99, 202, await f.Context())).ErrorCode.Should().Be("AUTH_SESSION_INVALID");
    }

    [Fact]
    public async Task PlatformWithoutMfa_OnlyReceivesShortEnrollmentSession()
    {
        using var f = new Fixture();
        f.User.EmpresaId = null;
        f.User.TipoUsuarioCodigo = "SUPERADMIN";
        f.User.MfaHabilitado = false;
        f.User.MfaSecretoCifrado = null;
        var role = f.Db.Roles.Add(new Rol { Codigo = "SUPERADMIN", Nombre = "Global", EsSistema = true, Activo = true }).Entity;
        f.User.Roles.Add(new UsuarioRol { Rol = role });
        await f.Db.SaveChangesAsync();
        var result = (await f.Login()).Value!;
        result.MfaEnrollmentRequired.Should().BeTrue();
        result.MfaVerificationRequired.Should().BeFalse();
        result.User.SessionPurpose.Should().Be(SessionClaims.MfaEnroll);
        result.User.Roles.Should().BeEmpty();
        result.User.Permisos.Should().BeEmpty();
        result.User.TipoUsuarioCodigo.Should().Be("OPERADOR");
        result.RefreshToken.Should().BeEmpty();
        f.Db.RefreshTokens.Should().BeEmpty();
        result.User.SessionExpiresAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(11));
        (await new AuthSessionService(f.Db).ValidateAsync(result.User.SessionId)).IsSuccess.Should().BeTrue();
        (await f.Service().CambiarEmpresaAsync(1, 202, new AuthContext { SessionId = result.User.SessionId }))
            .ErrorCode.Should().Be("AUTH_SESSION_INVALID");
        (await f.Service().VerifyMfaChallengeAsync("123456", new AuthContext { SessionId = result.User.SessionId }))
            .ErrorCode.Should().Be("AUTH_SESSION_INVALID");
    }

    [Fact]
    public async Task AdministrativeMembershipWithoutMfa_OnlyReceivesEnrollmentSession()
    {
        using var f = new Fixture();
        f.User.TipoUsuarioCodigo = "OPERADOR";
        f.User.MfaHabilitado = false;
        f.User.MfaSecretoCifrado = null;
        f.Db.Roles.Local.Single(r => r.Id == 1).Codigo = "OPERADOR";
        f.MemberRole.Codigo = "ADMIN";
        f.MemberRole.EsSistema = true;
        f.MemberRole.EmpresaId = null;
        await f.Db.SaveChangesAsync();

        var result = (await f.Login(mfa: null)).Value!;

        result.MfaEnrollmentRequired.Should().BeTrue();
        result.User.SessionPurpose.Should().Be(SessionClaims.MfaEnroll);
        result.User.Roles.Should().BeEmpty();
        result.RefreshToken.Should().BeEmpty();
        (await new AuthSessionService(f.Db).ValidateAsync(result.User.SessionId)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PromotionToAdministrativeMembership_InvalidatesExistingNonMfaSession()
    {
        using var f = new Fixture();
        f.User.TipoUsuarioCodigo = "OPERADOR";
        f.User.MfaHabilitado = false;
        f.User.MfaSecretoCifrado = null;
        f.Db.Roles.Local.Single(r => r.Id == 1).Codigo = "OPERADOR";
        await f.Db.SaveChangesAsync();
        var session = (await f.Login(mfa: null)).Value!.User.SessionId;

        f.MemberRole.Codigo = "ADMIN";
        f.MemberRole.EsSistema = true;
        f.MemberRole.EmpresaId = null;
        await f.Db.SaveChangesAsync();

        (await new AuthSessionService(f.Db).ValidateAsync(session)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SsoWithMfa_RequiresOneUseChallenge_AndDoesNotResetFailuresUntilVerified()
    {
        using var f = new Fixture();
        f.User.MfaHabilitado = true;
        f.User.SsoProveedor = SsoProveedores.Entra;
        f.User.SsoSubject = "fixture-subject";
        f.User.SsoIssuer = SsoTestIdentity.Issuer;
        f.User.IntentosFallidos = 2;
        await f.Db.SaveChangesAsync();
        var info = SsoTestIdentity.Info();
        var challenge = (await f.Service().LoginExternoAsync(info, new AuthContext())).Value!;
        challenge.MfaVerificationRequired.Should().BeTrue();
        challenge.RefreshToken.Should().BeEmpty();
        challenge.User.Roles.Should().BeEmpty();
        challenge.User.EmpresaId.Should().BeNull();
        f.User.IntentosFallidos.Should().Be(2);
        var context = new AuthContext { SessionId = challenge.User.SessionId };
        (await f.Service().VerifyMfaChallengeAsync("wrong", context)).ErrorCode.Should().Be("AUTH_MFA_INVALID");
        f.User.IntentosFallidos.Should().Be(3);
        var complete = (await f.Service().VerifyMfaChallengeAsync("123456", context)).Value!;
        complete.User.SessionPurpose.Should().Be(SessionClaims.Full);
        complete.User.EmpresaId.Should().Be(101);
        complete.User.Roles.Should().Contain("ADMIN_EMPRESA");
        complete.RefreshToken.Should().NotBeEmpty();
        f.User.IntentosFallidos.Should().Be(0);
        (await f.Service().VerifyMfaChallengeAsync("123456", context)).ErrorCode.Should().Be("AUTH_SESSION_INVALID");
        (await new AuthSessionService(f.Db).ValidateAsync(challenge.User.SessionId)).IsFailure.Should().BeTrue();
        (await new AuthSessionService(f.Db).ValidateAsync(complete.User.SessionId)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatedSsoCannotResetMfaFailureCounterOrBypassLockout()
    {
        using var f = new Fixture();
        f.User.MfaHabilitado = true;
        f.User.SsoProveedor = SsoProveedores.Entra;
        f.User.SsoSubject = "fixture-subject";
        f.User.SsoIssuer = SsoTestIdentity.Issuer;
        await f.Db.SaveChangesAsync();
        var info = SsoTestIdentity.Info();
        for (var i = 0; i < 5; i++)
        {
            var challenge = (await f.Service().LoginExternoAsync(info, new AuthContext())).Value!;
            (await f.Service().VerifyMfaChallengeAsync("wrong", new AuthContext { SessionId = challenge.User.SessionId }))
                .ErrorCode.Should().Be("AUTH_MFA_INVALID");
        }
        (await f.Service().LoginExternoAsync(info, new AuthContext())).ErrorCode.Should().Be("AUTH_USER_INACTIVE");
        f.Db.RefreshTokens.Should().BeEmpty();
        f.User.SecurityStamp.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TemporaryLockoutPermanentlyInvalidatesPreviouslyIssuedSession()
    {
        using var f = new Fixture();
        var login = (await f.Login()).Value!;
        for (var i = 0; i < 5; i++) await f.Login("wrong");
        f.User.BloqueadoHasta = DateTime.UtcNow.AddMinutes(-1);
        await f.Db.SaveChangesAsync();
        (await f.Login()).IsSuccess.Should().BeTrue();
        (await f.Service().RefreshAsync(login.RefreshToken, new AuthContext())).ErrorCode.Should().Be("AUTH_SESSION_INVALID");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sso_OnlyExpiredTemporaryLockCanBeReleased(bool temporary)
    {
        using var f = new Fixture();
        f.User.MfaHabilitado = true;
        f.User.SsoProveedor = SsoProveedores.Entra;
        f.User.SsoSubject = "fixture-subject";
        f.User.SsoIssuer = SsoTestIdentity.Issuer;
        f.User.EstadoCodigo = "BLOQUEADO";
        f.User.BloqueadoHasta = temporary ? DateTime.UtcNow.AddMinutes(-1) : null;
        await f.Db.SaveChangesAsync();
        var result = await f.Service().LoginExternoAsync(SsoTestIdentity.Info(), new AuthContext());
        if (temporary) result.Value!.User.SessionPurpose.Should().Be(SessionClaims.MfaVerify);
        else result.ErrorCode.Should().Be("AUTH_USER_INACTIVE");
    }
}
