using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Auth;

public class SsoConfigServiceTests
{
    private const int Empresa = 1;
    private const int OtraEmpresa = 2;
    private const int RolOperador = 502;

    private static SsoConfigService Build(out NeoStpDbContext db)
    {
        db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"ssocfg-{Guid.NewGuid()}").Options);
        db.Empresas.AddRange(
            new Empresa { Id = Empresa, Nit = "E1", RazonSocial = "Contoso", EstadoCodigo = "ACTIVA" },
            new Empresa { Id = OtraEmpresa, Nit = "E2", RazonSocial = "Fabrikam", EstadoCodigo = "ACTIVA" });
        db.Roles.Add(new Rol { Id = RolOperador, Codigo = "OPERADOR", Nombre = "Operador" });
        db.SaveChanges();
        return new SsoConfigService(db, Substitute.For<IAuditoriaService>());
    }

    private static GuardarEmpresaSsoRequest Req(string dominio = "contoso.com", bool autoProv = false, int? rol = null) => new()
    {
        ProveedorCodigo = SsoProveedores.Entra,
        Habilitado = true,
        DominioCorreo = dominio,
        AutoProvisionar = autoProv,
        RolPorDefectoId = rol,
    };

    [Fact]
    public async Task Get_SinConfig_RetornaNoConfigurado()
    {
        var svc = Build(out _);
        var r = await svc.GetAsync(Empresa);
        r.Value!.Configurado.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_Nueva_PersisteYNormalizaDominio()
    {
        var svc = Build(out var db);

        var r = await svc.GuardarAsync(Empresa, Req(dominio: "Contoso.COM"), "admin");

        r.IsSuccess.Should().BeTrue();
        r.Value!.DominioCorreo.Should().Be("contoso.com");
        (await db.EmpresaSso.CountAsync(x => x.EmpresaId == Empresa)).Should().Be(1);
    }

    [Fact]
    public async Task Guardar_DominioInvalido_Falla()
    {
        var svc = Build(out _);
        var r = await svc.GuardarAsync(Empresa, Req(dominio: "contoso"), "admin");
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Guardar_ProveedorInvalido_Falla()
    {
        var svc = Build(out _);
        var req = Req();
        req.ProveedorCodigo = "OKTA";
        var r = await svc.GuardarAsync(Empresa, req, "admin");
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Guardar_AutoProvisionSinRol_Falla()
    {
        var svc = Build(out _);
        var r = await svc.GuardarAsync(Empresa, Req(autoProv: true, rol: null), "admin");
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Guardar_AutoProvisionConRolValido_Ok()
    {
        var svc = Build(out _);
        var r = await svc.GuardarAsync(Empresa, Req(autoProv: true, rol: RolOperador), "admin");
        r.IsSuccess.Should().BeTrue();
        r.Value!.AutoProvisionar.Should().BeTrue();
        r.Value.RolPorDefectoNombre.Should().Be("Operador");
    }

    [Fact]
    public async Task Guardar_DominioEnUsoPorOtraEmpresa_Falla()
    {
        var svc = Build(out var db);
        db.EmpresaSso.Add(new EmpresaSso
        {
            EmpresaId = OtraEmpresa, ProveedorCodigo = SsoProveedores.Entra,
            Habilitado = true, DominioCorreo = "contoso.com",
        });
        db.SaveChanges();

        var r = await svc.GuardarAsync(Empresa, Req(dominio: "contoso.com"), "admin");

        r.ErrorCode.Should().Be("SSO_DOMINIO_EN_USO");
    }

    [Fact]
    public async Task Guardar_Actualiza_ConfigExistente()
    {
        var svc = Build(out var db);
        await svc.GuardarAsync(Empresa, Req(dominio: "contoso.com"), "admin");

        var r = await svc.GuardarAsync(Empresa, Req(dominio: "contoso.io"), "admin");

        r.IsSuccess.Should().BeTrue();
        r.Value!.DominioCorreo.Should().Be("contoso.io");
        (await db.EmpresaSso.CountAsync(x => x.EmpresaId == Empresa)).Should().Be(1);
    }
}
