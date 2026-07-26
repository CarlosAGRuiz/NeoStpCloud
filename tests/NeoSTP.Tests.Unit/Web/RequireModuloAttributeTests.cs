using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;
using NeoSTP.Web.Auth;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Web;

/// <summary>
/// El menú oculta los módulos no contratados, pero ocultar no es bloquear: antes de este
/// filtro, escribir la URL daba acceso completo a módulos que el plan no incluye.
/// </summary>
public class RequireModuloAttributeTests
{
    private const int Empresa = 42;

    private static AuthorizationFilterContext BuildContext(
        string? tipoUsuario, int? empresaId, LicenciaDto? licencia, bool autenticado = true)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(autenticado);
        currentUser.TipoUsuarioCodigo.Returns(tipoUsuario);

        var empresaContext = Substitute.For<IEmpresaContext>();
        empresaContext.CurrentEmpresaId.Returns(empresaId);

        var licencias = Substitute.For<ILicenciaResolver>();
        licencias.ResolveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(licencia);

        var services = new ServiceCollection();
        services.AddSingleton(currentUser);
        services.AddSingleton(empresaContext);
        services.AddSingleton(licencias);

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static LicenciaDto Licencia(bool vigente, params (string Codigo, bool Activo)[] modulos) => new()
    {
        EmpresaId = Empresa,
        EmpresaNombre = "Demo",
        EmpresaEstado = "ACTIVA",
        PlanNombre = "Starter",
        Vigente = vigente,
        Modulos = modulos.Select(m => new EmpresaModuloDto
        {
            Codigo = m.Codigo, Nombre = m.Codigo, Activo = m.Activo,
        }).ToList(),
    };

    [Fact]
    public async Task ModuloIncluido_DejaPasar()
    {
        var ctx = BuildContext("ADMIN", Empresa, Licencia(true, ("NEOPOS", true)));

        await new RequireModuloAttribute("NEOPOS").OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task ModuloNoIncluido_MuestraPantallaDeUpsell()
    {
        var ctx = BuildContext("ADMIN", Empresa, Licencia(true, ("NEODTE", true)));

        await new RequireModuloAttribute("NEOPOS").OnAuthorizationAsync(ctx);

        var vista = ctx.Result.Should().BeOfType<ViewResult>().Subject;
        vista.ViewName.Should().Be("ModuloNoIncluido");
        vista.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        vista.ViewData["ModuloNombre"].Should().Be("Punto de venta");
        vista.ViewData["PlanActual"].Should().Be("Starter");
    }

    [Fact]
    public async Task ModuloDesactivado_Bloquea()
    {
        var ctx = BuildContext("ADMIN", Empresa, Licencia(true, ("NEOPOS", false)));

        await new RequireModuloAttribute("NEOPOS").OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task LicenciaVencida_Bloquea()
    {
        var ctx = BuildContext("ADMIN", Empresa, Licencia(false, ("NEOPOS", true)));

        await new RequireModuloAttribute("NEOPOS").OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task SuperAdmin_NoSeBloquea()
    {
        var ctx = BuildContext("SUPERADMIN", null, null);

        await new RequireModuloAttribute("NEOPOS").OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task SinEmpresaEnContexto_Bloquea()
    {
        var ctx = BuildContext("ADMIN", null, null);

        await new RequireModuloAttribute("NEOPOS").OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task SinSesion_DejaActuarAlLogin()
    {
        var ctx = BuildContext(null, null, null, autenticado: false);

        await new RequireModuloAttribute("NEOPOS").OnAuthorizationAsync(ctx);

        // No fija resultado: el filtro de autenticación redirige al login.
        ctx.Result.Should().BeNull();
    }

    [Theory]
    [InlineData("NEOPOS", "Punto de venta")]
    [InlineData("INVENTARIO", "Inventario")]
    [InlineData("NEOAGENDA", "Agenda de citas")]
    [InlineData("NEOCONNECT", "NeoConnect (API)")]
    public void Catalogo_DescribeLosModulosVendibles(string codigo, string nombreEsperado)
    {
        var info = ModuloCatalogo.Describir(codigo);

        info.Nombre.Should().Be(nombreEsperado);
        info.Descripcion.Should().NotBeNullOrWhiteSpace();
        info.Planes.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Catalogo_ModuloDesconocido_NoRompe()
    {
        var info = ModuloCatalogo.Describir("NO_EXISTE");

        info.Nombre.Should().Be("NO_EXISTE");
        info.Descripcion.Should().NotBeNullOrWhiteSpace();
    }
}
