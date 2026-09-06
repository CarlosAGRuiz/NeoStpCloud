using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Unit.Auth;

public class PersistedSessionTests
{
    private sealed class Fixture : IDisposable
    {
        public NeoStpDbContext Db { get; } = new(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Usuario User { get; }
        public UserInfo Info { get; }
        public AuthSession Session { get; }
        public Fixture(string purpose = SessionClaims.Full)
        {
            var global = purpose == SessionClaims.MfaEnroll;
            var role = new Rol { Id = 1, EmpresaId = global ? null : 10, Codigo = global ? "SUPERADMIN" : "OPERADOR", Nombre = "Test", Activo = true, EsSistema = global };
            role.Permisos.Add(new RolPermiso { Permiso = new Permiso { Id = 1, Codigo = "Reportes.Ver", Modulo = "NEOBI", Descripcion = "Ver" } });
            Db.Empresas.Add(new Empresa { Id = 10, Nit = "TEST", RazonSocial = "Test", EstadoCodigo = "ACTIVA" });
            User = new Usuario
            {
                Id = 1, EmpresaId = global ? null : 10, Username = "test", Email = "test@example.test", NombreCompleto = "Test",
                PasswordHash = "test-only-hash", TipoUsuarioCodigo = global ? "SUPERADMIN" : "OPERADOR",
                MfaHabilitado = purpose == SessionClaims.MfaVerify,
                Roles = new List<UsuarioRol> { new() { Rol = role } }
            };
            Db.Usuarios.Add(User);
            Info = SessionUserInfoFactory.FromUser(User);
            Session = AuthSessionService.Create(User, Info, purpose, DateTime.UtcNow.AddHours(1));
            Db.AuthSessions.Add(Session);
            Db.SaveChanges();
        }
        public ClaimsPrincipal Principal() => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(SessionClaims.Id, Session.Id.ToString()),
            new Claim(SessionClaims.Purpose, Info.SessionPurpose),
            new Claim("tipo_usuario", Info.TipoUsuarioCodigo)
        }.Concat(Info.EmpresaId is int id ? new[] { new Claim("empresa_id", id.ToString()) } : Array.Empty<Claim>())
         .Concat(Info.Roles.Select(r => new Claim(ClaimTypes.Role, r)))
         .Concat(Info.Permisos.Select(p => new Claim("permiso", p))), "Test"));
        public void Dispose() => Db.Dispose();
    }

    [Theory]
    [InlineData(SessionClaims.Full)]
    [InlineData(SessionClaims.MfaEnroll)]
    [InlineData(SessionClaims.MfaVerify)]
    public async Task ValidPersistedSession_AcceptsExactClaims(string purpose)
    {
        using var f = new Fixture(purpose);
        (await new AuthSessionService(f.Db).ValidatePrincipalAsync(f.Principal())).IsSuccess.Should().BeTrue();
        if (purpose != SessionClaims.Full)
        {
            f.Info.Roles.Should().BeEmpty();
            f.Info.Permisos.Should().BeEmpty();
            f.Info.EmpresaId.Should().BeNull();
            f.Info.TipoUsuarioCodigo.Should().Be("OPERADOR");
        }
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("expiry")]
    [InlineData("blocked")]
    [InlineData("password")]
    [InlineData("stamp")]
    [InlineData("permission")]
    [InlineData("role")]
    [InlineData("company")]
    [InlineData("mfa")]
    public async Task NextValidation_RejectsChangedSessionOrAuthorization(string change)
    {
        using var f = new Fixture();
        switch(change)
        {
            case "logout": f.Session.RevokedAt = DateTime.UtcNow; break;
            case "expiry": f.Session.ExpiresAt = DateTime.UtcNow.AddSeconds(-1); break;
            case "blocked": f.User.EstadoCodigo = "BLOQUEADO"; break;
            case "password": f.User.PasswordHash = "changed"; break;
            case "stamp": f.User.SecurityStamp = Guid.NewGuid(); break;
            case "permission": f.User.Roles.Single().Rol.Permisos.Clear(); break;
            case "role": f.User.Roles.Single().Rol.Activo = false; break;
            case "company": f.Db.Empresas.Local.Single().EstadoCodigo = "SUSPENDIDA"; break;
            case "mfa": f.User.MfaHabilitado = true; break;
        }
        await f.Db.SaveChangesAsync();
        (await new AuthSessionService(f.Db).ValidatePrincipalAsync(f.Principal())).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(SessionClaims.Id, "")]
    [InlineData(SessionClaims.Purpose, "MFA_ENROLL")]
    [InlineData("empresa_id", "999")]
    [InlineData("tipo_usuario", "SUPERADMIN")]
    [InlineData(ClaimTypes.NameIdentifier, "2")]
    [InlineData(ClaimTypes.Role, "SUPERADMIN")]
    [InlineData("permiso", "SuperAdmin.Planes.Administrar")]
    public async Task AlteredOrLegacyClaims_AreRejected(string type, string value)
    {
        using var f = new Fixture();
        var principal = f.Principal();
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach(var claim in identity.FindAll(type).ToList()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(type, value));
        (await new AuthSessionService(f.Db).ValidatePrincipalAsync(principal)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Enrollment_StartDoesNotInvalidateChallenge_ButConfirmationDoes()
    {
        using var f = new Fixture(SessionClaims.MfaEnroll);
        f.User.MfaSecretoCifrado = "test-only-new-secret";
        await f.Db.SaveChangesAsync();
        (await new AuthSessionService(f.Db).ValidatePrincipalAsync(f.Principal())).IsSuccess.Should().BeTrue();
        f.User.MfaHabilitado = true;
        await f.Db.SaveChangesAsync();
        (await new AuthSessionService(f.Db).ValidatePrincipalAsync(f.Principal())).IsFailure.Should().BeTrue();
    }
}
