using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Auth;

public class AuthServiceTests
{
    private const string SeededPassword = "Strong#Password!2026";

    private static (AuthService service, NeoStpDbContext db, IJwtTokenService jwt, IAuditoriaService audit) BuildService()
    {
        var dbName = $"neostp-auth-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new NeoStpDbContext(options);

        var hasher = new BcryptPasswordHasher();

        // Seed user
        var usuario = new Usuario
        {
            Id = 1,
            Username = "tester",
            Email = "tester@neostp.local",
            NombreCompleto = "Tester",
            PasswordHash = hasher.Hash(SeededPassword),
            TipoUsuarioCodigo = "OPERADOR",
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = DateTime.UtcNow,
        };
        db.Usuarios.Add(usuario);
        db.SaveChanges();

        var jwt = Substitute.For<IJwtTokenService>();
        jwt.CreateAccessToken(Arg.Any<UserInfo>()).Returns(("fake.jwt.token", DateTime.UtcNow.AddHours(1)));
        jwt.CreateRefreshToken().Returns(_ => Guid.NewGuid().ToString("N"));

        var audit = Substitute.For<IAuditoriaService>();

        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "test", Audience = "test",
            Key = "0123456789012345678901234567890123",
            ExpiryMinutes = 60, RefreshTokenExpiryDays = 14,
        });

        var svc = new AuthService(db, hasher, jwt, audit, jwtOptions, NullLogger<AuthService>.Instance);
        return (svc, db, jwt, audit);
    }

    [Fact]
    public async Task LoginAsync_ReturnsOk_WhenCredentialsAreValid()
    {
        var (svc, db, _, audit) = BuildService();

        var result = await svc.LoginAsync(
            new LoginRequest { UsernameOrEmail = "tester", Password = SeededPassword },
            new AuthContext { IpAddress = "127.0.0.1", TraceId = "t1" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.User.Username.Should().Be("tester");

        (await db.RefreshTokens.CountAsync()).Should().Be(1);
        await audit.Received().RegistrarAsync(
            Arg.Is<AuditoriaEvent>(e => e.Accion == "LOGIN" && e.Resultado == "OK"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ReturnsFail_WhenPasswordIsWrong()
    {
        var (svc, db, _, audit) = BuildService();

        var result = await svc.LoginAsync(
            new LoginRequest { UsernameOrEmail = "tester", Password = "wrong-pass" },
            new AuthContext());

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");

        var u = await db.Usuarios.FirstAsync();
        u.IntentosFallidos.Should().Be(1);
        u.EstadoCodigo.Should().Be(EstadoCodes.Activo);

        await audit.Received().RegistrarAsync(
            Arg.Is<AuditoriaEvent>(e => e.Accion == "LOGIN" && e.Resultado == "FAIL"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_LocksUser_AfterFiveFailedAttempts()
    {
        var (svc, db, _, _) = BuildService();

        for (var i = 0; i < 5; i++)
        {
            await svc.LoginAsync(
                new LoginRequest { UsernameOrEmail = "tester", Password = "wrong" },
                new AuthContext());
        }

        var u = await db.Usuarios.FirstAsync();
        u.IntentosFallidos.Should().Be(5);
        u.EstadoCodigo.Should().Be(EstadoCodes.Bloqueado);
        u.BloqueadoHasta.Should().NotBeNull();
        u.BloqueadoHasta!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_ReturnsLocked_WhenUserIsBlocked()
    {
        var (svc, db, _, _) = BuildService();
        var u = await db.Usuarios.FirstAsync();
        u.BloqueadoHasta = DateTime.UtcNow.AddMinutes(10);
        u.EstadoCodigo = EstadoCodes.Bloqueado;
        await db.SaveChangesAsync();

        var result = await svc.LoginAsync(
            new LoginRequest { UsernameOrEmail = "tester", Password = SeededPassword },
            new AuthContext());

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AUTH_USER_LOCKED");
    }

    [Fact]
    public async Task LoginAsync_ResetsCounter_OnSuccessAfterFailures()
    {
        var (svc, db, _, _) = BuildService();
        var u = await db.Usuarios.FirstAsync();
        u.IntentosFallidos = 3;
        await db.SaveChangesAsync();

        var result = await svc.LoginAsync(
            new LoginRequest { UsernameOrEmail = "tester", Password = SeededPassword },
            new AuthContext());

        result.IsSuccess.Should().BeTrue();
        var refreshed = await db.Usuarios.FirstAsync();
        refreshed.IntentosFallidos.Should().Be(0);
        refreshed.UltimoLogin.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAsync_RotatesRefreshToken()
    {
        var (svc, db, _, _) = BuildService();
        var login = await svc.LoginAsync(
            new LoginRequest { UsernameOrEmail = "tester", Password = SeededPassword },
            new AuthContext { IpAddress = "1.1.1.1" });

        var oldRefresh = login.Value!.RefreshToken;
        var refreshResult = await svc.RefreshAsync(oldRefresh, new AuthContext { IpAddress = "2.2.2.2" });

        refreshResult.IsSuccess.Should().BeTrue();
        refreshResult.Value!.RefreshToken.Should().NotBe(oldRefresh);

        var rotated = await db.RefreshTokens.FirstAsync(t => t.Token == oldRefresh);
        rotated.RevokedAt.Should().NotBeNull();
        rotated.ReplacedByToken.Should().Be(refreshResult.Value.RefreshToken);
        rotated.RevokedReason.Should().Be("Replaced");

        (await db.RefreshTokens.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task LogoutAsync_RevokesRefreshToken()
    {
        var (svc, db, _, _) = BuildService();
        var login = await svc.LoginAsync(
            new LoginRequest { UsernameOrEmail = "tester", Password = SeededPassword },
            new AuthContext());

        await svc.LogoutAsync(login.Value!.RefreshToken, new AuthContext { IpAddress = "9.9.9.9" });

        var t = await db.RefreshTokens.FirstAsync();
        t.RevokedAt.Should().NotBeNull();
        t.RevokedReason.Should().Be("Logout");
    }
}
