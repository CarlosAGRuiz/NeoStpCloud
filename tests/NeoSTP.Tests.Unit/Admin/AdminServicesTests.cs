using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas.Dtos;
using NeoSTP.Application.Roles.Dtos;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Admin;

/// <summary>
/// Entrega 2 (blindaje del núcleo) — cobertura de los servicios administrativos
/// que no tenían tests directos: usuarios, roles, planes y empresas.
/// </summary>
public class AdminServicesTests
{
    private const int Empresa = 95;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"admin-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new NeoSTP.Domain.Core.Empresas.Empresa
        {
            Id = Empresa, Nit = "0614-000000-001-1", RazonSocial = "Admin Test", EstadoCodigo = "ACTIVA",
        });
        db.SaveChanges();
        return db;
    }

    private static IPasswordPolicy PolicyOk()
    {
        var p = Substitute.For<IPasswordPolicy>();
        p.Validate(Arg.Any<string?>()).Returns(Result.Ok());
        return p;
    }

    private static IPasswordHasher Hasher()
    {
        var h = Substitute.For<IPasswordHasher>();
        h.Hash(Arg.Any<string>()).Returns(c => $"HASH:{c.Arg<string>()}");
        h.Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(c => c.ArgAt<string>(1) == $"HASH:{c.ArgAt<string>(0)}");
        return h;
    }

    private static UsuariosService NewUsuarios(NeoStpDbContext db, IPasswordPolicy? policy = null)
        => new(db, Hasher(), Substitute.For<IAuditoriaService>(), policy ?? PolicyOk());

    private static RolesService NewRoles(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static EmpresasService NewEmpresas(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    // ─── UsuariosService ────────────────────────────────────────────────────────

    [Fact]
    public async Task Usuario_Create_HasheaPassword_YNormalizaTipo()
    {
        var db = NewDb();
        var svc = NewUsuarios(db);

        var r = await svc.CreateAsync(Empresa, new CreateUsuarioRequest
        {
            Username = "operador1", Email = "op@x.com", Password = "Fuerte!123",
            NombreCompleto = "Operador Uno", TipoUsuarioCodigo = "tipo-raro",
        }, "tester");

        r.IsSuccess.Should().BeTrue(r.Error);
        var persistido = await db.Usuarios.AsNoTracking().FirstAsync();
        persistido.PasswordHash.Should().Be("HASH:Fuerte!123");
        persistido.TipoUsuarioCodigo.Should().Be("OPERADOR"); // tipo desconocido cae a OPERADOR
    }

    [Fact]
    public async Task Usuario_Create_Duplicado_Falla()
    {
        var db = NewDb();
        var svc = NewUsuarios(db);
        CreateUsuarioRequest Req() => new()
        {
            Username = "op", Email = "op@x.com", Password = "Fuerte!123", NombreCompleto = "Op",
        };

        (await svc.CreateAsync(Empresa, Req(), "t")).IsSuccess.Should().BeTrue();
        (await svc.CreateAsync(Empresa, Req(), "t")).ErrorCode.Should().Be("USER_DUPLICATE");
    }

    [Fact]
    public async Task Usuario_Create_PasswordDebil_Validation()
    {
        var policy = Substitute.For<IPasswordPolicy>();
        policy.Validate(Arg.Any<string?>())
            .Returns(Result.Fail("Contraseña débil.", "PWD_WEAK", new[] { "Mínimo 12 caracteres." }));
        var svc = NewUsuarios(NewDb(), policy);

        var r = await svc.CreateAsync(Empresa, new CreateUsuarioRequest
        {
            Username = "op", Email = "op@x.com", Password = "123", NombreCompleto = "Op",
        }, "t");

        r.ErrorCode.Should().Be("VALIDATION");
        r.ValidationErrors.Should().Contain(e => e.Contains("12 caracteres"));
    }

    [Fact]
    public async Task Usuario_GetList_AislaPorEmpresa()
    {
        var db = NewDb();
        db.Empresas.Add(new NeoSTP.Domain.Core.Empresas.Empresa { Id = 96, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        await db.SaveChangesAsync();
        var svc = NewUsuarios(db);
        await svc.CreateAsync(Empresa, new CreateUsuarioRequest { Username = "a", Email = "a@x.com", Password = "P!", NombreCompleto = "A" }, "t");
        await svc.CreateAsync(96, new CreateUsuarioRequest { Username = "b", Email = "b@x.com", Password = "P!", NombreCompleto = "B" }, "t");

        var deEmpresa = await svc.GetListAsync(Empresa, new PagedQuery());
        var globales = await svc.GetListAsync(null, new PagedQuery());

        deEmpresa.Value!.Total.Should().Be(1);
        globales.Value!.Total.Should().Be(2); // scope null = SuperAdmin ve todos
    }

    // ─── RolesService ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Rol_Create_ConPermisos_YDuplicadoFalla()
    {
        var db = NewDb();
        db.Permisos.Add(new Permiso { Id = 900, Codigo = "X.Ver", Modulo = "X", Descripcion = "ver" });
        await db.SaveChangesAsync();
        var svc = NewRoles(db);

        var r = await svc.CreateAsync(Empresa, new CreateRolRequest
        {
            Codigo = "vendedor", Nombre = "Vendedor", PermisoIds = new[] { 900 },
        }, "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.Codigo.Should().Be("VENDEDOR");
        r.Value.PermisoCodigos.Should().Contain("X.Ver");

        (await svc.CreateAsync(Empresa, new CreateRolRequest { Codigo = "VENDEDOR", Nombre = "Otro" }, "t"))
            .ErrorCode.Should().Be("ROLE_DUPLICATE");
    }

    [Fact]
    public async Task Rol_DeSistema_NoSeModifica()
    {
        var db = NewDb();
        db.Roles.Add(new Rol { Id = 50, Codigo = "ADMIN", Nombre = "Admin", EsSistema = true, Activo = true });
        await db.SaveChangesAsync();
        var svc = NewRoles(db);

        var r = await svc.UpdateAsync(Empresa, 50, new UpdateRolRequest { Nombre = "Hackeado", Activo = true }, "t");

        r.ErrorCode.Should().Be("ROLE_SYSTEM");
    }

    [Fact]
    public async Task Rol_Update_ReemplazaPermisos()
    {
        var db = NewDb();
        db.Permisos.AddRange(
            new Permiso { Id = 901, Codigo = "A.Ver", Modulo = "A", Descripcion = "a" },
            new Permiso { Id = 902, Codigo = "B.Ver", Modulo = "B", Descripcion = "b" });
        await db.SaveChangesAsync();
        var svc = NewRoles(db);
        var creado = await svc.CreateAsync(Empresa, new CreateRolRequest { Codigo = "R1", Nombre = "R1", PermisoIds = new[] { 901 } }, "t");

        var r = await svc.UpdateAsync(Empresa, creado.Value!.Id,
            new UpdateRolRequest { Nombre = "R1", Activo = true, PermisoIds = new[] { 902 } }, "t");

        r.Value!.PermisoCodigos.Should().BeEquivalentTo(new[] { "B.Ver" });
    }

    // ─── PlanesService ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Planes_GetList_SoloActivos_OrdenadosPorPrecio()
    {
        var db = NewDb();
        db.Planes.AddRange(
            new Plan { Id = 300, Codigo = "CARO", Nombre = "Caro", PrecioMensual = 100m, Activo = true },
            new Plan { Id = 301, Codigo = "BARATO", Nombre = "Barato", PrecioMensual = 10m, Activo = true },
            new Plan { Id = 302, Codigo = "MUERTO", Nombre = "Inactivo", PrecioMensual = 5m, Activo = false });
        await db.SaveChangesAsync();

        var r = await new PlanesService(db).GetListAsync();

        r.Value!.Select(p => p.Codigo).Should().ContainInOrder("BARATO", "CARO");
        r.Value.Should().NotContain(p => p.Codigo == "MUERTO");
    }

    // ─── EmpresasService ────────────────────────────────────────────────────────

    [Fact]
    public async Task Empresa_Create_NitDuplicado_Falla()
    {
        var db = NewDb();
        var svc = NewEmpresas(db);

        var r = await svc.CreateAsync(new CreateEmpresaRequest
        {
            Nit = "0614-000000-001-1", // ya sembrada en NewDb
            RazonSocial = "Clon S.A.",
        }, "t");

        r.ErrorCode.Should().Be("EMPRESA_DUPLICATE");
    }

    [Fact]
    public async Task Empresa_Create_SinNitORazon_Validation()
    {
        var svc = NewEmpresas(NewDb());

        var r = await svc.CreateAsync(new CreateEmpresaRequest { Nit = " ", RazonSocial = "" }, "t");

        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Empresa_GetList_ConScope_SoloVeSuEmpresa()
    {
        var db = NewDb();
        db.Empresas.Add(new NeoSTP.Domain.Core.Empresas.Empresa { Id = 97, Nit = "OTRA", RazonSocial = "Otra", EstadoCodigo = "ACTIVA" });
        await db.SaveChangesAsync();
        var svc = NewEmpresas(db);

        var scoped = await svc.GetListAsync(Empresa, new PagedQuery());

        scoped.Value!.Items.Should().OnlyContain(e => e.Id == Empresa);
    }
}
