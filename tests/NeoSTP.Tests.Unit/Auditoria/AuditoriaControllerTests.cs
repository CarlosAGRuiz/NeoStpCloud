using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Web.Controllers;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Auditoria;

public class AuditoriaControllerTests
{
    private static (AuditoriaController ctrl, IAuditoriaQueryService svc, ICurrentUser user, IEmpresaContext empresa)
        Build(int? empresaId = 7, bool canView = true, string tipoUsuario = "EMPRESA")
    {
        var svc = Substitute.For<IAuditoriaQueryService>();
        var user = Substitute.For<ICurrentUser>();
        var empresa = Substitute.For<IEmpresaContext>();

        user.TipoUsuarioCodigo.Returns(tipoUsuario);
        user.HasPermiso("Core.Auditoria.Ver").Returns(canView);
        empresa.CurrentEmpresaId.Returns(empresaId);
        svc.ListAsync(Arg.Any<AuditoriaQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<AuditoriaDto>.Create(Array.Empty<AuditoriaDto>(), 0, 1, 30));
        svc.GetModulosAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "DTE" });

        var http = new DefaultHttpContext();
        var ctrl = new AuditoriaController(svc, user, empresa)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>()),
        };
        return (ctrl, svc, user, empresa);
    }

    [Fact]
    public async Task Index_SinPermiso_DevuelveForbid()
    {
        var (ctrl, svc, _, _) = Build(canView: false);

        var result = await ctrl.Index(new AuditoriaFiltro(), 1, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await svc.DidNotReceive().ListAsync(Arg.Any<AuditoriaQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_EmpresaUser_FiltraPorSuEmpresa()
    {
        var (ctrl, svc, _, _) = Build(empresaId: 7);

        var result = await ctrl.Index(new AuditoriaFiltro { Modulo = "DTE" }, 1, CancellationToken.None);

        result.Should().BeOfType<ViewResult>();
        await svc.Received(1).ListAsync(Arg.Is<AuditoriaQuery>(q => q.EmpresaId == 7 && q.Modulo == "DTE"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_EmpresaUserSinEmpresa_RedirigeAHome()
    {
        var (ctrl, svc, _, _) = Build(empresaId: null, tipoUsuario: "EMPRESA");

        var result = await ctrl.Index(new AuditoriaFiltro(), 1, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ControllerName.Should().Be("Home");
        await svc.DidNotReceive().ListAsync(Arg.Any<AuditoriaQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SuperAdminSinSoporte_ConsultaTodas()
    {
        var (ctrl, svc, _, _) = Build(empresaId: null, tipoUsuario: "SUPERADMIN");

        var result = await ctrl.Index(new AuditoriaFiltro(), 1, CancellationToken.None);

        result.Should().BeOfType<ViewResult>();
        await svc.Received(1).ListAsync(Arg.Is<AuditoriaQuery>(q => q.EmpresaId == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_DevuelveCsvConFilas()
    {
        var (ctrl, svc, _, _) = Build(empresaId: 7);
        svc.ExportAsync(Arg.Any<AuditoriaQuery>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditoriaDto>
            {
                new() { Id = 1, EmpresaId = 7, Username = "u", Modulo = "DTE", Accion = "EMITIR", Resultado = "OK", Detalle = "linea con \"comillas\"" },
            });

        var result = await ctrl.Export(new AuditoriaFiltro(), CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        csv.Should().Contain("Fecha,Empresa,Usuario,Modulo,Accion");
        csv.Should().Contain("EMITIR");
        csv.Should().Contain("\"\"comillas\"\""); // escape CSV
    }

    [Fact]
    public async Task Export_SinPermiso_DevuelveForbid()
    {
        var (ctrl, svc, _, _) = Build(canView: false);

        var result = await ctrl.Export(new AuditoriaFiltro(), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await svc.DidNotReceive().ExportAsync(Arg.Any<AuditoriaQuery>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
