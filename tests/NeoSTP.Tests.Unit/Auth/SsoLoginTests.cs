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
        mfa.VerificarCodigoLoginAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => (string)x[1] == "123456" ? NeoSTP.Application.Common.Result.Ok() : NeoSTP.Application.Common.Result.Fail("Inválido"));
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
        SsoProveedor = ssoProv, SsoSubject = ssoSub, SsoIssuer = ssoProv is null ? null : SsoTestIdentity.Issuer,
    };

    private static void SeedConfig(NeoStpDbContext db, bool autoProvisionar, string? tenant = null, string proveedor = SsoProveedores.Entra)
    {
        db.EmpresaSso.Add(new EmpresaSso
        {
            EmpresaId = Empresa, ProveedorCodigo = proveedor, Habilitado = true,
            DominioCorreo = "contoso.com", TenantIdExterno = tenant ?? SsoTestIdentity.Tenant,
            AutoProvisionar = autoProvisionar, RolPorDefectoId = autoProvisionar ? RolOperador : null,
        });
        db.SaveChanges();
    }

    private static ExternalLoginInfo Info(string sub, string? email, string? tenant = null, string proveedor = SsoProveedores.Entra) => new()
    {
        Proveedor = proveedor, Subject = sub, Email = email, NombreCompleto = "Ada Lovelace", TenantIdExterno = tenant ?? SsoTestIdentity.Tenant,
        Issuer = proveedor == SsoProveedores.Google ? "https://accounts.google.com" : $"https://login.microsoftonline.com/{tenant ?? SsoTestIdentity.Tenant}/v2.0",
        EmailVerified = true, HostedDomain = "contoso.com",
    };

    [Fact]
    public async Task SujetoYaVinculado_IniciaSesion()
    {
        var (svc, db, hasher) = Build();
        db.Usuarios.Add(NuevoUsuario(hasher, 10, "ada@contoso.com", SsoProveedores.Entra, "sub-abc"));
        SeedConfig(db, false);
        db.SaveChanges();

        var r = await svc.LoginExternoAsync(Info("sub-abc", "ada@contoso.com"), new AuthContext());

        r.IsSuccess.Should().BeTrue();
        r.Value!.User.Id.Should().Be(10);
        r.Value.AccessToken.Should().NotBeNullOrEmpty();
        (await db.RefreshTokens.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CuentaLocalPorCorreo_ExigePruebaLocalSinModificarLaCuenta()
    {
        var (svc, db, hasher) = Build();
        db.Usuarios.Add(NuevoUsuario(hasher, 11, "grace@contoso.com"));
        SeedConfig(db, false);
        db.SaveChanges();

        var r = await svc.LoginExternoAsync(Info("sub-new", "GRACE@contoso.com"), new AuthContext());

        r.ErrorCode.Should().Be("SSO_LINK_REQUIRED");
        var u = await db.Usuarios.FirstAsync(x => x.Id == 11);
        u.SsoProveedor.Should().BeNull();
        u.SsoSubject.Should().BeNull();
        db.AuthSessions.Should().BeEmpty();
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
        SeedConfig(db, autoProvisionar: true);

        var r = await svc.LoginExternoAsync(Info("sub-3", "x@contoso.com", tenant: "22222222-2222-2222-2222-222222222222"), new AuthContext());

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

    [Theory]
    [InlineData("x", true)]
    [InlineData("incorrecta", false)]
    public async Task VinculacionExplicita_ExigePasswordLocal(string password, bool valid)
    {
        var (svc, db, hasher) = Build();
        SeedConfig(db, false);
        db.Usuarios.Add(NuevoUsuario(hasher, 11, "grace@contoso.com"));
        await db.SaveChangesAsync();
        var result = await svc.VincularExternoAsync(Info("explicit", "grace@contoso.com"),
            new LoginRequest { UsernameOrEmail = "grace@contoso.com", Password = password }, new());
        result.IsSuccess.Should().Be(valid);
        var user = await db.Usuarios.SingleAsync();
        if (valid)
        {
            user.SsoIssuer.Should().Be(SsoTestIdentity.Issuer);
            user.SecurityStamp.Should().NotBeEmpty();
            (await svc.LoginExternoAsync(Info("explicit", "grace@contoso.com"), new())).IsSuccess.Should().BeTrue();
        }
        else { user.SsoSubject.Should().BeNull(); db.AuthSessions.Should().BeEmpty(); }
    }

    [Fact]
    public async Task VinculacionExplicita_NoReemplazaOtroSujeto()
    {
        var (svc, db, hasher) = Build(); SeedConfig(db, false);
        db.Usuarios.Add(NuevoUsuario(hasher, 11, "grace@contoso.com", "ENTRA", "original"));
        await db.SaveChangesAsync();
        var result = await svc.VincularExternoAsync(Info("replacement", "grace@contoso.com"),
            new LoginRequest { UsernameOrEmail = "grace@contoso.com", Password = "x" }, new());
        result.ErrorCode.Should().Be("SSO_LINK_CONFLICT");
        (await db.Usuarios.SingleAsync()).SsoSubject.Should().Be("original");
    }

    [Theory]
    [InlineData(false, "contoso.com")]
    [InlineData(true, "attacker.test")]
    [InlineData(true, null)]
    public async Task Google_NoAprovisionaSinDominioWorkspaceVerificado(bool verified, string? hostedDomain)
    {
        var (svc, db, _) = Build(); SeedConfig(db, true, proveedor: "GOOGLE");
        var info = Info("google-sub", "grace@contoso.com", proveedor: "GOOGLE");
        info.EmailVerified = verified; info.HostedDomain = hostedDomain;
        (await svc.LoginExternoAsync(info, new())).ErrorCode.Should().Be("SSO_DOMAIN_UNVERIFIED");
        db.Usuarios.Should().BeEmpty();
    }

    [Fact]
    public async Task Google_WorkspaceVerificadoPuedeAprovisionar()
    {
        var (svc, db, _) = Build(); SeedConfig(db, true, proveedor: "GOOGLE");
        (await svc.LoginExternoAsync(Info("google-sub", "grace@contoso.com", proveedor: "GOOGLE"), new())).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("directory")]
    [InlineData("issuer")]
    [InlineData("legacy")]
    public async Task SujetoExistente_NoOmitePoliticaNiIssuer(string change)
    {
        var (svc, db, hasher) = Build(); SeedConfig(db, false);
        var user = NuevoUsuario(hasher, 11, "grace@contoso.com", "ENTRA", "existing");
        db.Usuarios.Add(user);
        var info = Info("existing", "grace@contoso.com");
        switch (change)
        {
            case "disabled": (await db.EmpresaSso.SingleAsync()).Habilitado = false; break;
            case "directory": (await db.EmpresaSso.SingleAsync()).TenantIdExterno = Guid.NewGuid().ToString(); break;
            case "issuer": info.Issuer = "https://attacker.test"; break;
            case "legacy": user.SsoIssuer = null; break;
        }
        await db.SaveChangesAsync();
        (await svc.LoginExternoAsync(info, new())).IsFailure.Should().BeTrue();
        db.AuthSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task VinculacionNoBuscaCuentaEnOtraEmpresa()
    {
        var (svc, db, hasher) = Build(); SeedConfig(db, false);
        var user = NuevoUsuario(hasher, 11, "grace@contoso.com"); user.EmpresaId = 987;
        db.Usuarios.Add(user); await db.SaveChangesAsync();
        (await svc.VincularExternoAsync(Info("sub", "grace@contoso.com"),
            new LoginRequest { UsernameOrEmail = user.Email, Password = "x" }, new())).IsFailure.Should().BeTrue();
        user.SsoSubject.Should().BeNull();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("wrong", false)]
    [InlineData("123456", true)]
    public async Task VinculacionRequiereMfaLocalSiEstaHabilitado(string? code, bool valid)
    {
        var (svc, db, hasher) = Build(); SeedConfig(db, false);
        var user = NuevoUsuario(hasher, 11, "grace@contoso.com"); user.MfaHabilitado = true;
        db.Usuarios.Add(user); await db.SaveChangesAsync();
        var result = await svc.VincularExternoAsync(Info("sub", user.Email),
            new LoginRequest { UsernameOrEmail = user.Email, Password = "x", MfaCode = code }, new());
        result.IsSuccess.Should().Be(valid);
        if (!valid) { user.SsoSubject.Should().BeNull(); db.AuthSessions.Should().BeEmpty(); }
    }
}
