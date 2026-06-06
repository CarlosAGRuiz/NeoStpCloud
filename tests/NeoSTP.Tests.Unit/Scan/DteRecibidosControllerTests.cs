using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Web.Controllers;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Scan;

public class DteRecibidosControllerTests
{
    private static (DteRecibidosController ctrl, IDteRecibidoService svc, ICurrentUser user, IEmpresaContext empresaContext)
        Build(int? empresaId = 7, bool canView = true, string tipoUsuario = "EMPRESA")
    {
        var svc = Substitute.For<IDteRecibidoService>();
        var user = Substitute.For<ICurrentUser>();
        var empresaContext = Substitute.For<IEmpresaContext>();

        user.TipoUsuarioCodigo.Returns(tipoUsuario);
        user.HasPermiso("ScanAI.Ver").Returns(canView);
        empresaContext.CurrentEmpresaId.Returns(empresaId);

        var http = new DefaultHttpContext();
        var ctrl = new DteRecibidosController(svc, user, empresaContext)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>()),
        };
        return (ctrl, svc, user, empresaContext);
    }

    [Fact]
    public async Task Index_ConPermiso_CargaListadoConFiltros()
    {
        var (ctrl, svc, _, _) = Build();
        var page = PagedResult<DteRecibidoDto>.Create(
            [new DteRecibidoDto { Id = 1, EmisorNombre = "Acme", Total = 100 }], total: 1, page: 1, pageSize: 20);
        svc.ListAsync(7, Arg.Any<DteRecibidoQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<DteRecibidoDto>>.Ok(page));

        var result = await ctrl.Index("Acme", new DateOnly(2026, 1, 1), null, 1, CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<DteRecibidosIndexViewModel>().Subject;
        model.Recibidos.Total.Should().Be(1);
        await svc.Received(1).ListAsync(7, Arg.Is<DteRecibidoQuery>(q => q.Search == "Acme" && q.Desde == new DateOnly(2026, 1, 1)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SinPermiso_DevuelveForbid()
    {
        var (ctrl, svc, _, _) = Build(canView: false);

        var result = await ctrl.Index(null, null, null, 1, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await svc.DidNotReceive().ListAsync(Arg.Any<int>(), Arg.Any<DteRecibidoQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SuperAdminSinEmpresa_RedirigeASoporte()
    {
        var (ctrl, svc, _, _) = Build(empresaId: null, tipoUsuario: "SUPERADMIN");

        var result = await ctrl.Index(null, null, null, 1, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ControllerName.Should().Be("Soporte");
        await svc.DidNotReceive().ListAsync(Arg.Any<int>(), Arg.Any<DteRecibidoQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Detalle_NoEncontrado_DevuelveNotFound()
    {
        var (ctrl, svc, _, _) = Build();
        svc.GetAsync(7, 99, Arg.Any<CancellationToken>())
            .Returns(Result<DteRecibidoDto>.Fail("DTE recibido no encontrado.", "RECIBIDO_NOT_FOUND"));

        var result = await ctrl.Detalle(99, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Detalle_Existente_DevuelveVista()
    {
        var (ctrl, svc, _, _) = Build();
        svc.GetAsync(7, 5, Arg.Any<CancellationToken>())
            .Returns(Result<DteRecibidoDto>.Ok(new DteRecibidoDto { Id = 5, EmisorNombre = "Acme" }));

        var result = await ctrl.Detalle(5, CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeOfType<DteRecibidoDto>().Which.Id.Should().Be(5);
    }
}
