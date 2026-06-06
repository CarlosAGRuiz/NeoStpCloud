using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Web.Controllers;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Cobranza;

public class CobrosControllerTests
{
    private static (CobrosController ctrl, ICobranzaService cobranza, ICobroQrService qr, ICurrentUser user, IEmpresaContext empresaContext)
        Build(int? empresaId = 7, bool canView = true, bool canManage = true, string tipoUsuario = "EMPRESA")
    {
        var cobranza = Substitute.For<ICobranzaService>();
        var qr = Substitute.For<ICobroQrService>();
        var user = Substitute.For<ICurrentUser>();
        var empresaContext = Substitute.For<IEmpresaContext>();

        user.TipoUsuarioCodigo.Returns(tipoUsuario);
        user.Username.Returns("cajero1");
        user.HasPermiso("Cobros.Ver").Returns(canView);
        user.HasPermiso("Cobros.Gestionar").Returns(canManage);
        empresaContext.CurrentEmpresaId.Returns(empresaId);

        var http = new DefaultHttpContext();
        var ctrl = new CobrosController(cobranza, qr, user, empresaContext)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>()),
        };

        return (ctrl, cobranza, qr, user, empresaContext);
    }

    [Fact]
    public async Task Index_ConPermisoYCempresa_CargaResumenPendientesYCuentas()
    {
        var (ctrl, cobranza, qr, _, _) = Build();
        var pendientes = PagedResult<CobroPendienteDto>.Create(
            [new CobroPendienteDto { DteDocumentoId = 10, ClienteNombre = "Cliente A", Saldo = 25 }],
            total: 1,
            page: 1,
            pageSize: 20);
        cobranza.GetResumenAsync(7, Arg.Any<CancellationToken>())
            .Returns(new CobranzaResumenDto { TotalPendiente = 25, FacturasPendientes = 1 });
        cobranza.GetPendientesAsync(7, Arg.Any<CobranzaQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<CobroPendienteDto>>.Ok(pendientes));
        qr.ListarCuentasAsync(7, Arg.Any<CancellationToken>())
            .Returns([new CuentaCobroDto { Id = 3, Nombre = "Banco", EstadoCodigo = "ACTIVO" }]);

        var result = await ctrl.Index("Cliente", soloVencidas: true, page: 1, CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<CobrosIndexViewModel>().Subject;
        model.Resumen.TotalPendiente.Should().Be(25);
        model.Pendientes.Total.Should().Be(1);
        model.CuentasCobro.Should().ContainSingle(c => c.Id == 3);
        await cobranza.Received(1).GetResumenAsync(7, Arg.Any<CancellationToken>());
        await cobranza.Received(1).GetPendientesAsync(7, Arg.Is<CobranzaQuery>(q => q.Search == "Cliente" && q.SoloVencidas), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SinPermiso_DevuelveForbidYNoConsultaServicios()
    {
        var (ctrl, cobranza, qr, _, _) = Build(canView: false);

        var result = await ctrl.Index(null, false, 1, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        await cobranza.DidNotReceive().GetResumenAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await qr.DidNotReceive().ListarCuentasAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SuperAdminSinEmpresa_RedirigeASoporte()
    {
        var (ctrl, cobranza, _, _, _) = Build(empresaId: null, tipoUsuario: "SUPERADMIN");

        var result = await ctrl.Index(null, false, 1, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ControllerName.Should().Be("Soporte");
        redirect.ActionName.Should().Be("Index");
        ctrl.TempData["Error"].Should().Be("Cobros opera dentro de una empresa. Selecciona una en modo soporte primero.");
        await cobranza.DidNotReceive().GetResumenAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarPago_Falla_PropagaErrorEnTempData()
    {
        var (ctrl, cobranza, _, _, _) = Build();
        cobranza.RegistrarPagoAsync(7, 44, Arg.Any<RegistrarPagoRequest>(), "cajero1", Arg.Any<CancellationToken>())
            .Returns(Result<PagoClienteDto>.Fail("Monto invalido", "VALIDATION"));

        var result = await ctrl.RegistrarPago(44, new RegistrarPagoRequest { Monto = 100 }, returnUrl: null, CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Error"].Should().Be("Monto invalido");
        await cobranza.Received(1).RegistrarPagoAsync(7, 44, Arg.Any<RegistrarPagoRequest>(), "cajero1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmarYAnularPago_ExigenGestionYUsanEmpresaActual()
    {
        var (ctrl, cobranza, _, _, _) = Build();
        cobranza.ConfirmarPagoAsync(7, 8, "cajero1", Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        cobranza.AnularPagoAsync(7, 9, "cajero1", Arg.Any<CancellationToken>())
            .Returns(Result.Fail("No se puede anular", "VALIDATION"));

        var confirmar = await ctrl.ConfirmarPago(8, returnUrl: null, CancellationToken.None);
        confirmar.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Success"].Should().Be("Pago confirmado.");

        var anular = await ctrl.AnularPago(9, returnUrl: null, CancellationToken.None);
        anular.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Error"].Should().Be("No se puede anular");
        await cobranza.Received(1).ConfirmarPagoAsync(7, 8, "cajero1", Arg.Any<CancellationToken>());
        await cobranza.Received(1).AnularPagoAsync(7, 9, "cajero1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearCuenta_LlamaQrServiceConEmpresaActual()
    {
        var (ctrl, _, qr, _, _) = Build();
        var req = new CrearCuentaCobroRequest { Tipo = "TRANSFERENCIA", Nombre = "BAC" };
        qr.CrearCuentaAsync(7, req, "cajero1", Arg.Any<CancellationToken>())
            .Returns(Result<CuentaCobroDto>.Ok(new CuentaCobroDto { Id = 1, Nombre = "BAC" }));

        var result = await ctrl.CrearCuenta(req, CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Success"].Should().Be("Cuenta de cobro creada.");
        await qr.Received(1).CrearCuentaAsync(7, req, "cajero1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerarQr_Falla_RedirigeConError()
    {
        var (ctrl, _, qr, _, _) = Build();
        qr.GenerarQrAsync(7, Arg.Any<GenerarQrCobroRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<CobroQrDto>.Fail("No hay una cuenta activa.", "CUENTA_NOT_FOUND"));

        var result = await ctrl.GenerarQr(new GenerarQrCobroRequest { Monto = 10 }, null, false, 1, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        ctrl.TempData["Error"].Should().Be("No hay una cuenta activa.");
    }
}
