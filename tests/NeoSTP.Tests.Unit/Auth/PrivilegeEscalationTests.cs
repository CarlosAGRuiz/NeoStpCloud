using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Api.Auth;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Roles.Dtos;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NeoSTP.Web.Auth;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Auth;

public class PrivilegeEscalationTests
{
    private const int Company = 11;
    private const int Other = 22;
    private const int GlobalAdmin = 500;
    private const int CompanyAdmin = 501;
    private const int TenantRole = 600;
    private const int TenantReserved = 601;
    private const int UnsafeRole = 602;
    private const int OtherRole = 603;
    private const int InactiveRole = 604;

    private static NeoStpDbContext Db()
    {
        var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"privilege-{Guid.NewGuid()}").Options);
        db.Permisos.AddRange(
            new Permiso { Id = 1, Codigo = "Core.Clientes.Ver", Modulo = "CORE", Descripcion = "Read clients" },
            new Permiso { Id = 2, Codigo = "SuperAdmin.Planes.Administrar", Modulo = "ADMIN", Descripcion = "Global plans" },
            new Permiso { Id = 3, Codigo = "Ops.Hardening.Administrar", Modulo = "ADMIN", Descripcion = "Operations" });
        db.Roles.AddRange(
            new Rol { Id = GlobalAdmin, Codigo = "SUPERADMIN", Nombre = "Platform", EsSistema = true },
            new Rol { Id = CompanyAdmin, Codigo = "ADMIN", Nombre = "Company admin", EsSistema = true },
            new Rol { Id = TenantRole, EmpresaId = Company, Codigo = "CUSTOM", Nombre = "Custom" },
            new Rol { Id = TenantReserved, EmpresaId = Company, Codigo = "SUPERADMIN", Nombre = "Legacy reserved" },
            new Rol { Id = UnsafeRole, EmpresaId = Company, Codigo = "LEGACY", Nombre = "Legacy unsafe" },
            new Rol { Id = OtherRole, EmpresaId = Other, Codigo = "OTHER", Nombre = "Other tenant" },
            new Rol { Id = InactiveRole, EmpresaId = Company, Codigo = "INACTIVE", Nombre = "Inactive", Activo = false });
        db.RolPermisos.AddRange(
            new RolPermiso { RolId = CompanyAdmin, PermisoId = 1 },
            new RolPermiso { RolId = TenantRole, PermisoId = 1 },
            new RolPermiso { RolId = GlobalAdmin, PermisoId = 2 },
            new RolPermiso { RolId = UnsafeRole, PermisoId = 2 });
        db.Usuarios.AddRange(
            new Usuario { Id = 10, EmpresaId = Company, Username = "local", Email = "local@example.invalid", NombreCompleto = "Local", PasswordHash = "synthetic" },
            new Usuario { Id = 20, EmpresaId = Other, Username = "external", Email = "external@example.invalid", NombreCompleto = "External", PasswordHash = "synthetic" });
        db.UsuarioRoles.Add(new UsuarioRol { UsuarioId = 10, RolId = TenantRole });
        db.SaveChanges();
        return db;
    }

    private static ICurrentUser Actor(bool platform = false)
    {
        var actor = Substitute.For<ICurrentUser>();
        actor.IsAuthenticated.Returns(true);
        actor.UserId.Returns(99);
        actor.EmpresaId.Returns(platform ? null : Company);
        actor.Username.Returns("audit-admin");
        actor.TipoUsuarioCodigo.Returns(platform ? "SUPERADMIN" : "ADMIN");
        actor.IsInRole("SUPERADMIN").Returns(platform);
        return actor;
    }

    private static UsuariosService Users(NeoStpDbContext db, ICurrentUser? actor)
    {
        var policy = Substitute.For<IPasswordPolicy>();
        policy.Validate(Arg.Any<string>()).Returns(Result.Ok());
        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns("synthetic-hash");
        return new UsuariosService(db, hasher, Substitute.For<IAuditoriaService>(), policy, currentUser: actor);
    }

    private static CreateUsuarioRequest NewUser(string type = "OPERADOR", params int[] roles) => new()
    {
        Username = "new-user", Email = "new@example.invalid", NombreCompleto = "New",
        Password = "Test-only#2026", TipoUsuarioCodigo = type, RoleIds = roles
    };

    private static UpdateUsuarioRequest Update(string type = "OPERADOR", params int[] roles) => new()
    {
        Email = "changed@example.invalid", NombreCompleto = "Changed", TipoUsuarioCodigo = type,
        EstadoCodigo = "ACTIVO", RoleIds = roles
    };

    [Theory]
    [InlineData("SUPERADMIN", CompanyAdmin)]
    [InlineData(" superadmin ", CompanyAdmin)]
    [InlineData("OPERADOR", GlobalAdmin)]
    [InlineData("OPERADOR", TenantReserved)]
    [InlineData("OPERADOR", UnsafeRole)]
    [InlineData("OPERADOR", OtherRole)]
    [InlineData("OPERADOR", InactiveRole)]
    [InlineData("OPERADOR", 99999)]
    public async Task TenantCannotCreatePrivilegedOrInvalidUser_NoPartialWrites(string type, int role)
    {
        using var db = Db();
        var result = await Users(db, Actor()).CreateAsync(Company, NewUser(type, role), "audit");
        result.ErrorCode.Should().Be("FORBIDDEN");
        (await db.Usuarios.CountAsync()).Should().Be(2);
        db.ChangeTracker.Entries<Usuario>().Should().NotContain(e => e.State == EntityState.Added);
    }

    [Theory]
    [InlineData("SUPERADMIN", TenantRole)]
    [InlineData("OPERADOR", GlobalAdmin)]
    [InlineData("OPERADOR", UnsafeRole)]
    [InlineData("OPERADOR", OtherRole)]
    public async Task TenantCannotEscalateOnUpdate_ExistingDataAndRolesRemain(string type, int role)
    {
        using var db = Db();
        var result = await Users(db, Actor()).UpdateAsync(Company, 10, Update(type, role), "audit");
        result.ErrorCode.Should().Be("FORBIDDEN");
        var user = await db.Usuarios.Include(u => u.Roles).SingleAsync(u => u.Id == 10);
        user.Email.Should().Be("local@example.invalid");
        user.TipoUsuarioCodigo.Should().Be("OPERADOR");
        user.Roles.Select(r => r.RolId).Should().Equal(TenantRole);
    }

    [Fact]
    public async Task PlatformScopeCannotPromoteTenantUserToGlobalType()
    {
        using var db = Db();
        var result = await Users(db, Actor(true)).UpdateAsync(null, 10, Update("SUPERADMIN", CompanyAdmin), "audit");
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Theory]
    [InlineData(CompanyAdmin)]
    [InlineData(TenantRole)]
    public async Task LegitimateTenantUserCreationStillWorks(int role)
    {
        using var db = Db();
        var result = await Users(db, Actor()).CreateAsync(Company, NewUser("ADMIN", role), "audit");
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.EmpresaId.Should().Be(Company);
        result.Value.RoleIds.Should().Equal(role);
    }

    [Fact]
    public async Task OnlyTrustedPlatformActorCanCreateGlobalUser()
    {
        using var db = Db();
        (await Users(db, Actor()).CreateAsync(null, NewUser("SUPERADMIN", GlobalAdmin), "audit"))
            .ErrorCode.Should().Be("FORBIDDEN");
        (await Users(db, null).CreateAsync(null, NewUser("SUPERADMIN", GlobalAdmin), "forged-superadmin"))
            .ErrorCode.Should().Be("FORBIDDEN");
        (await Users(db, Actor(true)).CreateAsync(null, NewUser("SUPERADMIN", GlobalAdmin), "audit"))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TenantCannotAddressOtherTenantOrGlobalUsers()
    {
        using var db = Db();
        var users = Users(db, Actor());
        (await users.UpdateAsync(null, 20, Update("ADMIN", CompanyAdmin), "audit")).IsFailure.Should().BeTrue();
        (await users.BloquearAsync(Other, 20, "audit")).IsFailure.Should().BeTrue();
        (await users.GetListAsync(null, new PagedQuery())).ErrorCode.Should().Be("FORBIDDEN");
    }

    [Theory]
    [InlineData("SUPERADMIN", 1)]
    [InlineData(" superadmin ", 1)]
    [InlineData("ADMIN_CUSTOM", 2)]
    [InlineData("ADMIN_CUSTOM", 3)]
    [InlineData("ADMIN_CUSTOM", 99999)]
    public async Task TenantCannotCreateReservedRoleOrGlobalPermissions(string code, int permission)
    {
        using var db = Db();
        var count = await db.Roles.CountAsync();
        var svc = new RolesService(db, Substitute.For<IAuditoriaService>(), Actor());
        var result = await svc.CreateAsync(Company, new CreateRolRequest
        {
            Codigo = code, Nombre = "Attempt", PermisoIds = new[] { permission }
        }, "audit");
        result.ErrorCode.Should().Be("FORBIDDEN");
        (await db.Roles.CountAsync()).Should().Be(count);
    }

    [Fact]
    public async Task FailedRoleUpdateDoesNotRemovePreviousPermissions()
    {
        using var db = Db();
        var svc = new RolesService(db, Substitute.For<IAuditoriaService>(), Actor());
        var result = await svc.UpdateAsync(Company, TenantRole, new UpdateRolRequest
        {
            Nombre = "Escalated", Activo = true, PermisoIds = new[] { 2 }
        }, "audit");
        result.ErrorCode.Should().Be("FORBIDDEN");
        (await db.Roles.SingleAsync(r => r.Id == TenantRole)).Nombre.Should().Be("Custom");
        (await db.RolPermisos.Where(p => p.RolId == TenantRole).Select(p => p.PermisoId).ToListAsync()).Should().Equal(1);
    }

    [Fact]
    public async Task SafeCustomRoleCanBeCreatedAndEdited()
    {
        using var db = Db();
        var svc = new RolesService(db, Substitute.For<IAuditoriaService>(), Actor());
        var created = await svc.CreateAsync(Company, new CreateRolRequest
        {
            Codigo = "SALES", Nombre = "Sales", PermisoIds = new[] { 1 }
        }, "audit");
        created.IsSuccess.Should().BeTrue(created.Error);
        var updated = await svc.UpdateAsync(Company, created.Value!.Id, new UpdateRolRequest
        {
            Nombre = "Sales renamed", Activo = true, PermisoIds = Array.Empty<int>()
        }, "audit");
        updated.IsSuccess.Should().BeTrue(updated.Error);
        updated.Value!.PermisoIds.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantCannotModifyGlobalCustomRole()
    {
        using var db = Db();
        db.Roles.Add(new Rol { Id = 700, Codigo = "GLOBAL", Nombre = "Global" });
        await db.SaveChangesAsync();
        var svc = new RolesService(db, Substitute.For<IAuditoriaService>(), Actor());
        (await svc.UpdateAsync(Company, 700, new UpdateRolRequest { Nombre = "Changed", Activo = true }, "audit"))
            .ErrorCode.Should().Be("ROLE_NOT_FOUND");
    }

    [Fact]
    public async Task TenantRoleAndPermissionListsDoNotOfferPlatformAccess()
    {
        using var db = Db();
        var svc = new RolesService(db, Substitute.For<IAuditoriaService>(), Actor());
        var roles = (await svc.GetListAsync(Company)).Value!;
        roles.Select(r => r.Id).Should().Contain(CompanyAdmin).And.NotContain(new[] { GlobalAdmin, TenantReserved, UnsafeRole, OtherRole });
        var permissions = (await svc.GetPermisosAsync()).Value!;
        permissions.Select(p => p.Id).Should().Equal(1);
    }

    [Theory]
    [InlineData(GlobalAdmin)]
    [InlineData(TenantReserved)]
    [InlineData(UnsafeRole)]
    [InlineData(OtherRole)]
    [InlineData(InactiveRole)]
    public async Task MembershipCannotProvideGlobalOrForeignRole(int role)
    {
        using var db = Db();
        var svc = new UsuarioEmpresaService(db, Substitute.For<IAuditoriaService>());
        var result = await svc.AgregarAsync(Company, new AgregarMiembroRequest
        {
            EmailOUsername = "external", RolId = role
        }, "audit");
        result.ErrorCode.Should().Be("ROLE_NOT_FOUND");
        (await db.UsuarioEmpresas.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(GlobalAdmin)]
    [InlineData(TenantReserved)]
    [InlineData(UnsafeRole)]
    [InlineData(OtherRole)]
    [InlineData(InactiveRole)]
    public async Task SsoProvisioningConfigurationCannotAssignPrivilegedRole(int role)
    {
        using var db = Db();
        var svc = new SsoConfigService(db, Substitute.For<IAuditoriaService>());
        var result = await svc.GuardarAsync(Company, new GuardarEmpresaSsoRequest
        {
            ProveedorCodigo = "ENTRA", DominioCorreo = "example.invalid", Habilitado = true,
            AutoProvisionar = true, RolPorDefectoId = role
        }, "audit");
        result.IsFailure.Should().BeTrue();
        (await db.EmpresaSso.CountAsync()).Should().Be(0);
    }

    private static ClaimsPrincipal Principal(string type, int? company, string role, string? permission = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "10"), new("tipo_usuario", type), new(ClaimTypes.Role, role)
        };
        if (company is int e) claims.Add(new("empresa_id", e.ToString()));
        if (permission is not null) claims.Add(new("permiso", permission));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Theory]
    [InlineData("ADMIN", Company, "SUPERADMIN")]
    [InlineData("SUPERADMIN", Company, "SUPERADMIN")]
    [InlineData("ADMIN", null, "SUPERADMIN")]
    [InlineData("SUPERADMIN", null, "CUSTOM")]
    public async Task RoleNameOrExplicitGlobalPermissionDoesNotGrantPlatformAuthorization(string type, int? company, string role)
    {
        var requirement = new PermisoRequirement("SuperAdmin.Planes.Administrar");
        var ctx = new AuthorizationHandlerContext(new[] { requirement },
            Principal(type, company, role, requirement.Codigo), null);
        await new PermisoAuthorizationHandler().HandleAsync(ctx);
        ctx.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task LegitimatePlatformAndTenantPermissionsRemainUsable()
    {
        var global = new PermisoRequirement("SuperAdmin.Planes.Administrar");
        var globalCtx = new AuthorizationHandlerContext(new[] { global }, Principal("SUPERADMIN", null, "SUPERADMIN"), null);
        await new PermisoAuthorizationHandler().HandleAsync(globalCtx);
        globalCtx.HasSucceeded.Should().BeTrue();
        var tenant = new PermisoRequirement("Core.Clientes.Ver");
        var tenantCtx = new AuthorizationHandlerContext(new[] { tenant }, Principal("ADMIN", Company, "ADMIN", tenant.Codigo), null);
        await new PermisoAuthorizationHandler().HandleAsync(tenantCtx);
        tenantCtx.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WebAndApiCurrentUserRejectTenantPlatformClaims(bool web)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext
        {
            User = Principal("SUPERADMIN", Company, "SUPERADMIN", "Ops.Hardening.Administrar")
        }};
        ICurrentUser current = web ? new CookieCurrentUser(accessor) : new CurrentUserAccessor(accessor);
        current.TipoUsuarioCodigo.Should().Be("OPERADOR");
        current.IsInRole("SUPERADMIN").Should().BeFalse();
        current.HasPermiso("Ops.Hardening.Administrar").Should().BeFalse();
    }

    [Fact]
    public async Task AdministrativeBlockClearsTemporaryExpiry()
    {
        using var db = Db();
        var user = await db.Usuarios.SingleAsync(u => u.Id == 10);
        user.BloqueadoHasta = DateTime.UtcNow.AddMinutes(5);
        await db.SaveChangesAsync();
        (await Users(db, Actor()).BloquearAsync(Company, 10, "audit")).IsSuccess.Should().BeTrue();
        user.BloqueadoHasta.Should().BeNull();
        user.EstadoCodigo.Should().Be("BLOQUEADO");
    }

    [Fact]
    public async Task ApiReturnsForbiddenForPrivilegeEscalation()
    {
        using var db = Db();
        var actor = Actor();
        var controller = new NeoSTP.Api.Controllers.UsuariosController(Users(db, actor), actor)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var action = await controller.Create(NewUser("OPERADOR", GlobalAdmin), CancellationToken.None);
        action.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InactiveTenantRoleCanStillBeViewedAndReactivated()
    {
        using var db = Db();
        var svc = new RolesService(db, Substitute.For<IAuditoriaService>(), Actor());
        (await svc.GetByIdAsync(Company, InactiveRole)).IsSuccess.Should().BeTrue();
        var update = await svc.UpdateAsync(Company, InactiveRole, new UpdateRolRequest
        {
            Nombre = "Reactivated", Activo = true, PermisoIds = new[] { 1 }
        }, "audit");
        update.IsSuccess.Should().BeTrue(update.Error);
        (await Users(db, Actor()).CreateAsync(Company, NewUser("OPERADOR", InactiveRole), "audit"))
            .IsSuccess.Should().BeTrue();
    }


    [Theory]
    [InlineData(TenantRole)]
    [InlineData(CompanyAdmin)]
    public async Task LegitimateUserUpdatePreservesOrReplacesRoleWithoutDuplicateTracking(int role)
    {
        using var db = Db();
        var result = await Users(db, Actor()).UpdateAsync(Company, 10, Update("ADMIN", role), "audit");
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Email.Should().Be("changed@example.invalid");
        result.Value.RoleIds.Should().Equal(role);
    }

}
