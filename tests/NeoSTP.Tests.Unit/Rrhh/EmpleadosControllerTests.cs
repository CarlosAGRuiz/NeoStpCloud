using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Web.Controllers;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Rrhh;

public class EmpleadosControllerTests
{
    private static (EmpleadosController ctrl, IEmpleadosService svc, ICurrentUser user, IEmpresaContext empresa)
        Build(int? empresaId = 7, bool canView = true, bool canManage = true, string tipoUsuario = "EMPRESA")
    {
        var svc = Substitute.For<IEmpleadosService>();
        var user = Substitute.For<ICurrentUser>();
        var empresa = Substitute.For<IEmpresaContext>();

        user.TipoUsuarioCodigo.Returns(tipoUsuario);
        user.Username.Returns("rrhh1");
        user.HasPermiso("Rrhh.Empleados.Ver").Returns(canView);
        user.HasPermiso("Rrhh.Empleados.Gestionar").Returns(canManage);
        user.HasPermiso("Rrhh.Nomina.Ver").Returns(true);
        empresa.CurrentEmpresaId.Returns(empresaId);

        var http = new DefaultHttpContext();
        var ctrl = new EmpleadosController(svc, user, empresa)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>()),
        };
        return (ctrl, svc, user, empresa);
    }

    [Fact]
    public async Task Index_ConPermiso_CargaListado()
    {
        var (ctrl, svc, _, _) = Build();
        svc.GetListAsync(7, Arg.Any<PagedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<EmpleadoDto>>.Ok(PagedResult<EmpleadoDto>.Create(
                [new EmpleadoDto { Id = 1, Codigo = "E001", NombreCompleto = "Juan Pérez" }], 1, 1, 20)));

        var result = await ctrl.Index(null, 1, CancellationToken.None);

        result.Should().BeOfType<ViewResult>();
        await svc.Received(1).GetListAsync(7, Arg.Any<PagedQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SinPermiso_Forbid()
    {
        var (ctrl, svc, _, _) = Build(canView: false);
        var result = await ctrl.Index(null, 1, CancellationToken.None);
        result.Should().BeOfType<ForbidResult>();
        await svc.DidNotReceive().GetListAsync(Arg.Any<int>(), Arg.Any<PagedQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Index_SuperAdminSinEmpresa_RedirigeASoporte()
    {
        var (ctrl, _, _, _) = Build(empresaId: null, tipoUsuario: "SUPERADMIN");
        var result = await ctrl.Index(null, 1, CancellationToken.None);
        result.Should().BeOfType<RedirectToActionResult>().Which.ControllerName.Should().Be("Soporte");
    }

    [Fact]
    public async Task Create_Exitoso_RedirigeADetalle()
    {
        var (ctrl, svc, _, _) = Build();
        svc.CreateAsync(7, Arg.Any<CreateEmpleadoRequest>(), "rrhh1", Arg.Any<CancellationToken>())
            .Returns(Result<EmpleadoDetalleDto>.Ok(new EmpleadoDetalleDto { Id = 9 }));

        var result = await ctrl.Create(new CreateEmpleadoRequest { Codigo = "E1", Nombres = "Ana", Apellidos = "López", NumeroDocumento = "0", SalarioMensual = 500 }, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(EmpleadosController.Detalle));
        ctrl.TempData["Success"].Should().Be("Empleado registrado.");
    }

    [Fact]
    public async Task Create_Falla_RetornaVistaConErrores()
    {
        var (ctrl, svc, _, _) = Build();
        svc.CreateAsync(7, Arg.Any<CreateEmpleadoRequest>(), "rrhh1", Arg.Any<CancellationToken>())
            .Returns(Result<EmpleadoDetalleDto>.Fail("Datos inválidos.", "VALIDATION", new[] { "El salario mensual debe ser mayor a 0." }));

        var result = await ctrl.Create(new CreateEmpleadoRequest(), CancellationToken.None);

        result.Should().BeOfType<ViewResult>();
        ctrl.ModelState.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Inactivar_SinGestionar_Forbid()
    {
        var (ctrl, svc, _, _) = Build(canManage: false);
        var result = await ctrl.Inactivar(5, CancellationToken.None);
        result.Should().BeOfType<ForbidResult>();
        await svc.DidNotReceive().InactivarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Detalle_NoEncontrado_NotFound()
    {
        var (ctrl, svc, _, _) = Build();
        svc.GetAsync(7, 99, Arg.Any<CancellationToken>())
            .Returns(Result<EmpleadoDetalleDto>.Fail("Empleado no encontrado.", "EMPLEADO_NOT_FOUND"));
        var result = await ctrl.Detalle(99, CancellationToken.None);
        result.Should().BeOfType<NotFoundResult>();
    }
}
