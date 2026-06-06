using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Web.Controllers;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Scan;

public class ScanControllerTests
{
    private static (ScanController ctrl, IScanService scan, ICurrentUser user, IEmpresaContext empresaContext)
        Build(int? empresaId = 7, bool canView = true, bool canConfirm = true, string tipoUsuario = "EMPRESA")
    {
        var scan = Substitute.For<IScanService>();
        var user = Substitute.For<ICurrentUser>();
        var empresaContext = Substitute.For<IEmpresaContext>();

        user.TipoUsuarioCodigo.Returns(tipoUsuario);
        user.Username.Returns("operador1");
        user.HasPermiso("ScanAI.Ver").Returns(canView);
        user.HasPermiso("ScanAI.Confirmar").Returns(canConfirm);
        empresaContext.CurrentEmpresaId.Returns(empresaId);

        var http = new DefaultHttpContext();
        var ctrl = new ScanController(scan, user, empresaContext)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>()),
        };

        return (ctrl, scan, user, empresaContext);
    }

    [Fact]
    public async Task Index_ConPermisoYEmpresa_CargaBandejaConFiltros()
    {
        var (ctrl, scan, _, _) = Build();
        var page = PagedResult<ScanDocumentoDto>.Create(
            [new ScanDocumentoDto { Id = 5, EmisorNombre = "Proveedor X", EstadoCodigo = "REQUIERE_REVISION" }],
            total: 1, page: 1, pageSize: 20);
        scan.ListAsync(7, Arg.Any<ScanQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<ScanDocumentoDto>>.Ok(page));

        var result = await ctrl.Index("Proveedor", "REQUIERE_REVISION", 1, CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ScanIndexViewModel>().Subject;
        model.Documentos.Total.Should().Be(1);
        model.PuedeConfirmar.Should().BeTrue();
        await scan.Received(1).ListAsync(7, Arg.Is<ScanQuery>(q => q.Search == "Proveedor" && q.EstadoCodigo == "REQUIERE_REVISION"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SinPermiso_DevuelveForbidYNoConsulta()
    {
        var (ctrl, scan, _, _) = Build(canView: false);

        var result = await ctrl.Index(null, null, 1, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await scan.DidNotReceive().ListAsync(Arg.Any<int>(), Arg.Any<ScanQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SuperAdminSinEmpresa_RedirigeASoporte()
    {
        var (ctrl, scan, _, _) = Build(empresaId: null, tipoUsuario: "SUPERADMIN");

        var result = await ctrl.Index(null, null, 1, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ControllerName.Should().Be("Soporte");
        await scan.DidNotReceive().ListAsync(Arg.Any<int>(), Arg.Any<ScanQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Detalle_NoEncontrado_DevuelveNotFound()
    {
        var (ctrl, scan, _, _) = Build();
        scan.GetAsync(7, 99, Arg.Any<CancellationToken>())
            .Returns(Result<ScanDocumentoDto>.Fail("Escaneo no encontrado.", "SCAN_NOT_FOUND"));

        var result = await ctrl.Detalle(99, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Corregir_LlamaServicioYRedirigeADetalle()
    {
        var (ctrl, scan, _, _) = Build();
        scan.CorregirAsync(7, 5, Arg.Any<CorregirScanRequest>(), "operador1", Arg.Any<CancellationToken>())
            .Returns(Result<ScanDocumentoDto>.Ok(new ScanDocumentoDto { Id = 5 }));

        var result = await ctrl.Corregir(5, new CorregirScanRequest { EmisorNombre = "Nuevo" }, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ScanController.Detalle));
        ctrl.TempData["Success"].Should().Be("Campos actualizados.");
        await scan.Received(1).CorregirAsync(7, 5, Arg.Any<CorregirScanRequest>(), "operador1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarGasto_SinConfirmar_DevuelveForbid()
    {
        var (ctrl, scan, _, _) = Build(canConfirm: false);

        var result = await ctrl.RegistrarGasto(5, new CreateProfitGastoRequest { Monto = 10 }, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await scan.DidNotReceive().ConfirmarComoGastoAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CreateProfitGastoRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarGasto_Exitoso_PropagaSuccess()
    {
        var (ctrl, scan, _, _) = Build();
        scan.ConfirmarComoGastoAsync(7, 5, Arg.Any<CreateProfitGastoRequest>(), "operador1", Arg.Any<CancellationToken>())
            .Returns(Result<ScanDocumentoDto>.Ok(new ScanDocumentoDto { Id = 5, EstadoCodigo = "CONFIRMADO" }));

        var result = await ctrl.RegistrarGasto(5, new CreateProfitGastoRequest { Monto = 10 }, CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Success"].Should().Be("Escaneo confirmado como gasto.");
        await scan.Received(1).ConfirmarComoGastoAsync(7, 5, Arg.Any<CreateProfitGastoRequest>(), "operador1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarDteRecibido_Falla_PropagaError()
    {
        var (ctrl, scan, _, _) = Build();
        scan.RegistrarDteRecibidoAsync(7, 5, Arg.Any<RegistrarDteRecibidoRequest>(), "operador1", Arg.Any<CancellationToken>())
            .Returns(Result<ScanDocumentoDto>.Fail("El emisor es obligatorio.", "VALIDATION"));

        var result = await ctrl.RegistrarDteRecibido(5, new RegistrarDteRecibidoRequest { EmisorNombre = "" }, CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Error"].Should().Be("El emisor es obligatorio.");
    }

    [Fact]
    public async Task Rechazar_LlamaServicioConMotivo()
    {
        var (ctrl, scan, _, _) = Build();
        scan.RechazarAsync(7, 5, "Duplicado", "operador1", Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var result = await ctrl.Rechazar(5, "Duplicado", CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Success"].Should().Be("Escaneo rechazado.");
        await scan.Received(1).RechazarAsync(7, 5, "Duplicado", "operador1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archivo_NoEncontrado_DevuelveNotFound()
    {
        var (ctrl, scan, _, _) = Build();
        scan.GetArchivoAsync(7, 5, Arg.Any<CancellationToken>()).Returns((ScanArchivo?)null);

        var result = await ctrl.Archivo(5, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
