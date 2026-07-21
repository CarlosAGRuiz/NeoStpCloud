using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Auth;

public class SsoLoginTests
{
    private const int Empresa = 1;
    private const int RolOperador = 502;

    private static (AuthService svc, NeoStpDbContext db, BcryptPasswordHasher hasher) Build()
    {
        var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"sso-{Guid.NewGuid()}").Options);
        var hasher = new BcryptPasswordHasher();

        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "E1", RazonSocial = "Contoso", EstadoCodigo = "ACTIVA" });
        var permiso = new Permiso { Id = 402, Codigo = "Pos.Ver", Modulo = "NEOPOS", Descripcion = "Ver POS" };
        db.Permisos.Add(permiso);
        db.Roles.Add(new Rol
        {
            Id = RolOperador, Codigo = "OPERADOR", Nombre = "Operador",
            Permisos = new List<RolPermiso> { new() { RolId = RolOperador, PermisoId = 402, Permiso = permiso } },
        });
        db.SaveChanges();

        var jwt = Substitute.For<IJwtTokenService>();
        jwt.CreateAccessToken(Arg.Any<UserInfo>()).Returns(("fake.jwt.token", DateTime.UtcNow.AddHours(1)));
        jwt.CreateRefreshToken().Returns(_ => Guid.NewGuid().ToString("N"));
        var audit = Substitute.For<IAuditoriaService>();
        var mfa = Substitute.For<NeoSTP.Application.Ops.IMfaService>();
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "test", Audience = "test", Key = "0123456789012345678901234567890123",
            ExpiryMinutes = 60, RefreshTokenExpiryDays = 14,
        });
        var svc = new AuthService(db, hasher, jwt, audit, mfa, jwtOptions,
            Options.Create(new SecurityOptions()), NullLogger<AuthService>.Instance);
        return (svc, db, hasher);
    }

    private static Usuario NuevoUsuario(BcryptPasswordHasher hasher, int id, string email, string? ssoProv = null, string? ssoSub = null) => new()
    {
        Id = id, EmpresaId = Empresa, Username = email, Email = email, NombreCompleto = "Usuario",
        PasswordHash = hasher.Hash("x"), TipoUsuarioCodigo = "OPERADOR", EstadoCodigo = EstadoCodes.Activo,
        SsoProveedor = ssoProv, SsoSubject = ssoSub,
    };

    private static void SeedConfig(NeoStpDbContext db, bool autoProvisionar, string? tenant = null, string proveedor = SsoProveedores.Entra)
    {
        db.EmpresaSso.Add(new EmpresaSso
        {
            EmpresaId = Empresa, ProveedorCodigo = proveedor, Habilitado = true,
            DominioCorreo = "contoso.com", TenantIdExterno = tenant,
            AutoProvisionar = autoProvisionar, RolPorDefectoId = autoProvisionar ? RolOperador : null,
        });
        db.SaveChanges();
    }

    private static ExternalLoginInfo Info(string sub, string? email, string? tenant = null, string proveedor = SsoProveedores.Entra) => new()
    {
        Proveedor = proveedor, Subject = sub, Email = email, NombreCompleto = "Ada Lovelace", TenantIdExterno = tenant,
    };

    [Fact]
    public async Task SujetoYaVinculado_IniciaSesion()
    {
        var (svc, db, hasher) = Build();
        db.Usuarios.Add(NuevoUsuario(hasher, 10, "ada@contoso.com", SsoProveedores.Entra, "sub-abc"));
        db.SaveChanges();

        var r = await svc.LoginExternoAsync(Info("sub-abc", "ada@contoso.com"), new AuthContext());

        r.IsSuccess.Should().BeTrue();
        r.Value!.User.Id.Should().Be(10);
        r.Value.AccessToken.Should().NotBeNullOrEmpty();
        (await db.RefreshTokens.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CuentaLocalPorCorreo_SeVincula()
    {
        var (svc, db, hasher) = Build();
        db.Usuarios.Add(NuevoUsuario(hasher, 11, "grace@contoso.com"));
        db.SaveChanges();

        var r = await svc.LoginExternoAsync(Info("sub-new", "GRACE@contoso.com"), new AuthContext());

        r.IsSuccess.Should().BeTrue();
        r.Value!.User.Id.Should().Be(11);
        var u = await db.Usuarios.FirstAsync(x => x.Id == 11);
        u.SsoProveedor.Should().Be(SsoProveedores.Entra);
        u.SsoSubject.Should().Be("sub-new");
    }

    [Fact]
    public async Task SinCuenta_ConAutoProvision_CreaUsuarioConRol()
    {
        var (svc, db, _) = Build();
        SeedConfig(db, autoProvisionar: true);

        var r = await svc.LoginExternoAsync(Info("sub-xyz", "nuevo@contoso.com"), new AuthContext());

        r.IsSuccess.Should().BeTrue();
        r.Value!.User.EmpresaId.Should().Be(Empresa);
        r.Value.User.Permisos.Should().Contain("Pos.Ver");
        var creado = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == "nuevo@contoso.com");
        creado.Should().NotBeNull();
        creado!.SsoSubject.Should().Be("sub-xyz");
        creado.Roles.Should().ContainSingle();
    }

    [Fact]
    public async Task SinCuentaNiConfig_Falla()
    {
        var (svc, _, _) = Build();

        var r = await svc.LoginExternoAsync(Info("sub-1", "desconocido@otra.com"), new AuthContext());

        r.ErrorCode.Should().Be("SSO_SIN_CUENTA");
    }

    [Fact]
    public async Task DominioConfiguradoSinAutoProvision_Falla()
    {
        var (svc, db, _) = Build();
        SeedConfig(db, autoProvisionar: false);

        var r = await svc.LoginExternoAsync(Info("sub-2", "nadie@contoso.com"), new AuthContext());

        r.ErrorCode.Should().Be("SSO_SIN_CUENTA");
    }

    [Fact]
    public async Task TenantNoCoincide_Falla()
    {
        var (svc, db, _) = Build();
        SeedConfig(db, autoProvisionar: true, tenant: "tenant-corporativo");

        var r = await svc.LoginExternoAsync(Info("sub-3", "x@contoso.com", tenant: "tenant-ajeno"), new AuthContext());

        r.ErrorCode.Should().Be("SSO_TENANT_NO_COINCIDE");
    }

    [Fact]
    public async Task ProveedorInvalido_Falla()
    {
        var (svc, _, _) = Build();

        var r = await svc.LoginExternoAsync(Info("sub-4", "x@contoso.com", proveedor: "FACEBOOK"), new AuthContext());

        r.ErrorCode.Should().Be("SSO_PROVIDER_INVALID");
    }

    [Fact]
    public async Task NuevoSujetoSinCorreo_Falla()
    {
        var (svc, _, _) = Build();

        var r = await svc.LoginExternoAsync(Info("sub-5", email: null), new AuthContext());

        r.ErrorCode.Should().Be("SSO_SIN_CORREO");
    }
}
