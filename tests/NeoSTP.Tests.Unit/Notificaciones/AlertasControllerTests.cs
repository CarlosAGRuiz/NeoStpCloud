using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Web.Controllers;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Notificaciones;

public class AlertasControllerTests
{
    private static (AlertasController ctrl, IAlertaService alertas, IAlertaGeneracionService gen, ICurrentUser user, IEmpresaContext empresaContext)
        Build(int? empresaId = 7, int? userId = 99, string tipoUsuario = "EMPRESA")
    {
        var alertas = Substitute.For<IAlertaService>();
        var gen = Substitute.For<IAlertaGeneracionService>();
        var user = Substitute.For<ICurrentUser>();
        var empresaContext = Substitute.For<IEmpresaContext>();

        user.TipoUsuarioCodigo.Returns(tipoUsuario);
        user.UserId.Returns(userId);
        empresaContext.CurrentEmpresaId.Returns(empresaId);
        alertas.ResumenAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new AlertaResumenDto());

        var http = new DefaultHttpContext();
        var ctrl = new AlertasController(alertas, gen, user, empresaContext)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>()),
        };

        return (ctrl, alertas, gen, user, empresaContext);
    }

    [Fact]
    public async Task Index_ConEmpresaYUsuario_CargaListadoYResumen()
    {
        var (ctrl, alertas, _, _, _) = Build();
        var page = PagedResult<AlertaDto>.Create(
            [new AlertaDto { Id = 1, Titulo = "Factura vencida", Severidad = "ADVERTENCIA", EstadoCodigo = "PENDIENTE" }],
            total: 1, page: 1, pageSize: 20);
        alertas.ListarAsync(7, 99, Arg.Any<AlertaQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<AlertaDto>>.Ok(page));
        alertas.ResumenAsync(7, 99, Arg.Any<CancellationToken>())
            .Returns(new AlertaResumenDto { Pendientes = 1, Advertencias = 1 });

        var result = await ctrl.Index("PENDIENTE", null, 1, CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AlertasIndexViewModel>().Subject;
        model.Alertas.Total.Should().Be(1);
        model.Resumen.Pendientes.Should().Be(1);
        await alertas.Received(1).ListarAsync(7, 99, Arg.Is<AlertaQuery>(q => q.EstadoCodigo == "PENDIENTE"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SuperAdminSinEmpresa_RedirigeASoporte()
    {
        var (ctrl, alertas, _, _, _) = Build(empresaId: null, tipoUsuario: "SUPERADMIN");

        var result = await ctrl.Index(null, null, 1, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ControllerName.Should().Be("Soporte");
        await alertas.DidNotReceive().ListarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<AlertaQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leer_LlamaServicioYRedirige()
    {
        var (ctrl, alertas, _, _, _) = Build();
        alertas.MarcarLeidaAsync(7, 99, 5, Arg.Any<CancellationToken>()).Returns(Result.Ok());

        var result = await ctrl.Leer(5, CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        await alertas.Received(1).MarcarLeidaAsync(7, 99, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolver_Falla_PropagaError()
    {
        var (ctrl, alertas, _, _, _) = Build();
        alertas.ResolverAsync(7, 99, 5, Arg.Any<CancellationToken>())
            .Returns(Result.Fail("No encontrada", "NOT_FOUND"));

        var result = await ctrl.Resolver(5, CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Error"].Should().Be("No encontrada");
    }

    [Fact]
    public async Task LeerTodas_MarcaYRedirige()
    {
        var (ctrl, alertas, _, _, _) = Build();
        alertas.MarcarTodasLeidasAsync(7, 99, Arg.Any<CancellationToken>()).Returns(Result.Ok());

        var result = await ctrl.LeerTodas(CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Success"].Should().Be("Alertas marcadas como leídas.");
        await alertas.Received(1).MarcarTodasLeidasAsync(7, 99, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generar_RecalculaYReportaConteo()
    {
        var (ctrl, _, gen, _, _) = Build();
        gen.GenerarAsync(7, Arg.Any<CancellationToken>()).Returns(3);

        var result = await ctrl.Generar(CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        ctrl.TempData["Success"].Should().Be("Se generaron 3 alerta(s) nueva(s).");
        await gen.Received(1).GenerarAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GuardarPreferencias_LlamaServicioYRedirige()
    {
        var (ctrl, alertas, _, _, _) = Build();
        var pref = new PreferenciaNotificacionDto { Canal = "EMAIL", NoMolestar = true };
        alertas.GuardarPreferenciasAsync(7, 99, pref, Arg.Any<CancellationToken>()).Returns(Result.Ok());

        var result = await ctrl.Preferencias(pref, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AlertasController.Preferencias));
        ctrl.TempData["Success"].Should().Be("Preferencias guardadas.");
        await alertas.Received(1).GuardarPreferenciasAsync(7, 99, pref, Arg.Any<CancellationToken>());
    }
}
