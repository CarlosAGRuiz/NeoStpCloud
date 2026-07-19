using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Usuarios;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Auth;

/// <summary>E1 — membresías multi-empresa: gestión de miembros y cambio de empresa activa.</summary>
public class UsuarioEmpresaTests
{
    private const int EmpresaContador = 110; // empresa principal del contador
    private const int EmpresaCliente = 111;  // empresa que lo invita

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"ue-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaContador, Nit = "C1", RazonSocial = "Despacho Contable", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaCliente, Nit = "C2", RazonSocial = "Cliente SA", NombreComercial = "Cliente", EstadoCodigo = "ACTIVA" });

        db.Permisos.Add(new Permiso { Id = 950, Codigo = "Reportes.Ver", Modulo = "NEOBI", Descripcion = "ver" });
        var rolContador = new Rol { Id = 60, EmpresaId = EmpresaCliente, Codigo = "CONTADOR_EXT", Nombre = "Contador externo", Activo = true };
        db.Roles.Add(rolContador);
        db.RolPermisos.Add(new RolPermiso { RolId = 60, PermisoId = 950, CreatedAt = DateTime.UtcNow });

        db.Usuarios.Add(new Usuario
        {
            Id = 10, EmpresaId = EmpresaContador, Username = "contador", Email = "conta@despacho.com",
            PasswordHash = "h", NombreCompleto = "Con Tador", TipoUsuarioCodigo = "ADMIN", EstadoCodigo = "ACTIVO",
        });
        db.SaveChanges();
        return db;
    }

    private static UsuarioEmpresaService NewMiembros(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static AuthService NewAuth(NeoStpDbContext db)
    {
        var jwt = Substitute.For<IJwtTokenService>();
        jwt.CreateAccessToken(Arg.Any<UserInfo>()).Returns(("token", DateTime.UtcNow.AddHours(1)));
        jwt.CreateRefreshToken().Returns(Guid.NewGuid().ToString());
        return new AuthService(db, Substitute.For<IPasswordHasher>(), jwt,
            Substitute.For<IAuditoriaService>(), Substitute.For<NeoSTP.Application.Ops.IMfaService>(),
            Options.Create(new JwtOptions { Key = "k", Issuer = "i", Audience = "a" }),
            Options.Create(new SecurityOptions()),
            Substitute.For<ILogger<AuthService>>());
    }

    private static AuthContext Ctx() => new() { IpAddress = "127.0.0.1" };

    [Fact]
    public async Task Agregar_PorEmail_CreaMembresia_YDuplicadoFalla()
    {
        var db = NewDb(); var svc = NewMiembros(db);

        var r = await svc.AgregarAsync(EmpresaCliente,
            new AgregarMiembroRequest { EmailOUsername = "conta@despacho.com", RolId = 60 }, "admin");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.RolNombre.Should().Be("Contador externo");

        (await svc.AgregarAsync(EmpresaCliente,
            new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 60 }, "admin"))
            .ErrorCode.Should().Be("MIEMBRO_DUPLICADO");
    }

    [Fact]
    public async Task Agregar_ASuPropiaEmpresa_ORolAjeno_Falla()
    {
        var db = NewDb(); var svc = NewMiembros(db);

        (await svc.AgregarAsync(EmpresaContador,
            new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 60 }, "admin"))
            .ErrorCode.Should().Be("MIEMBRO_ES_PROPIO");

        db.Roles.Add(new Rol { Id = 61, EmpresaId = 999, Codigo = "OTRO", Nombre = "Otro", Activo = true });
        await db.SaveChangesAsync();
        (await svc.AgregarAsync(EmpresaCliente,
            new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 61 }, "admin"))
            .ErrorCode.Should().Be("ROLE_NOT_FOUND");
    }

    [Fact]
    public async Task Quitar_Revoca_YReAgregarReactiva()
    {
        var db = NewDb(); var svc = NewMiembros(db);
        await svc.AgregarAsync(EmpresaCliente, new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 60 }, "a");

        (await svc.QuitarAsync(EmpresaCliente, 10, "a")).IsSuccess.Should().BeTrue();
        (await svc.ListarAsync(EmpresaCliente)).Value.Should().BeEmpty();

        (await svc.AgregarAsync(EmpresaCliente, new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 60 }, "a"))
            .IsSuccess.Should().BeTrue();
        (await db.UsuarioEmpresas.CountAsync()).Should().Be(1); // reactivó, no duplicó
    }

    [Fact]
    public async Task ListarEmpresasDisponibles_IncluyePrincipalYMembresias()
    {
        var db = NewDb();
        await NewMiembros(db).AgregarAsync(EmpresaCliente, new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 60 }, "a");
        var auth = NewAuth(db);

        var r = await auth.ListarEmpresasDisponiblesAsync(10);

        r.Value!.Should().HaveCount(2);
        r.Value.Single(e => e.EsPrincipal).Nombre.Should().Be("Despacho Contable");
        r.Value.Single(e => !e.EsPrincipal).RolNombre.Should().Be("Contador externo");
    }

    [Fact]
    public async Task CambiarEmpresa_EmitePermisosDelRolDeEsaEmpresa()
    {
        var db = NewDb();
        await NewMiembros(db).AgregarAsync(EmpresaCliente, new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 60 }, "a");
        var auth = NewAuth(db);

        var r = await auth.CambiarEmpresaAsync(10, EmpresaCliente, Ctx());

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.User.EmpresaId.Should().Be(EmpresaCliente);
        r.Value.User.Roles.Should().BeEquivalentTo(new[] { "CONTADOR_EXT" });
        r.Value.User.Permisos.Should().BeEquivalentTo(new[] { "Reportes.Ver" });
    }

    [Fact]
    public async Task CambiarEmpresa_SinMembresia_OEmpresaSuspendida_Falla()
    {
        var db = NewDb();
        var auth = NewAuth(db);

        (await auth.CambiarEmpresaAsync(10, EmpresaCliente, Ctx())).ErrorCode.Should().Be("EMPRESA_NO_MEMBRESIA");

        await NewMiembros(db).AgregarAsync(EmpresaCliente, new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 60 }, "a");
        (await db.Empresas.FirstAsync(e => e.Id == EmpresaCliente)).EstadoCodigo = "SUSPENDIDA";
        await db.SaveChangesAsync();

        (await auth.CambiarEmpresaAsync(10, EmpresaCliente, Ctx())).ErrorCode.Should().Be("EMPRESA_SUSPENDIDA");
    }

    [Fact]
    public async Task CambiarEmpresa_DeVueltaALaPrincipal_RestauraSusPermisos()
    {
        var db = NewDb();
        await NewMiembros(db).AgregarAsync(EmpresaCliente, new AgregarMiembroRequest { EmailOUsername = "contador", RolId = 60 }, "a");
        var auth = NewAuth(db);

        var r = await auth.CambiarEmpresaAsync(10, EmpresaContador, Ctx());

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.User.EmpresaId.Should().Be(EmpresaContador);
        r.Value.User.Roles.Should().NotContain("CONTADOR_EXT");
    }
}
