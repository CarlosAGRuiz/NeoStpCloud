using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Controllers;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Shared;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// B-1 (NeoCloud Mobile): endpoint POST /api/dte/emitir — emisión en un solo paso.
/// Verifica delegación a IConnectDteService, resolución de tenant y mapeo de resultado.
/// </summary>
public class DteControllerEmitirTests
{
    private static (DteController ctrl, IConnectDteService connect, IDteDocumentosService docs) Build(int? empresaId)
    {
        var docs = Substitute.For<IDteDocumentosService>();
        var connect = Substitute.For<IConnectDteService>();
        var user = Substitute.For<ICurrentUser>();
        user.EmpresaId.Returns(empresaId);
        user.Username.Returns("vendedor1");

        var ctrl = new DteController(docs, connect, user)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return (ctrl, connect, docs);
    }

    [Fact]
    public async Task Emitir_DelegaEnConnectDte_YDevuelveOk()
    {
        var (ctrl, connect, _) = Build(empresaId: 5);
        var req = new CreateDteDocumentoRequest { TipoDteCodigo = "01" };
        var final = new DteDocumentoDto { Id = 99, EstadoCodigo = DteEstadoCodigos.Procesado };
        connect.EmitirAsync(5, req, "vendedor1", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(final));

        var result = await ctrl.Emitir(req, empresaId: null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<DteDocumentoDto>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Data!.EstadoCodigo.Should().Be(DteEstadoCodigos.Procesado);
        await connect.Received(1).EmitirAsync(5, req, "vendedor1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Emitir_SinEmpresa_DevuelveBadRequestNoTenant()
    {
        var (ctrl, connect, _) = Build(empresaId: null);

        var result = await ctrl.Emitir(new CreateDteDocumentoRequest(), empresaId: null, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        await connect.DidNotReceive().EmitirAsync(Arg.Any<int>(), Arg.Any<CreateDteDocumentoRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Emitir_FallaValidacion_DevuelveBadRequest()
    {
        var (ctrl, connect, _) = Build(empresaId: 5);
        connect.EmitirAsync(5, Arg.Any<CreateDteDocumentoRequest>(), "vendedor1", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Fail("Esquema inválido", "VALIDATION"));

        var result = await ctrl.Emitir(new CreateDteDocumentoRequest(), empresaId: null, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task EmitirFactura_FuerzaTipo01()
    {
        var (ctrl, connect, _) = Build(empresaId: 7);
        connect.EmitirAsync(7, Arg.Any<CreateDteDocumentoRequest>(), "vendedor1", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(new DteDocumentoDto { Id = 1, EstadoCodigo = "PROCESADO" }));

        var req = new CreateDteDocumentoRequest { TipoDteCodigo = "99" };
        await ctrl.EmitirFactura(req, empresaId: null, CancellationToken.None);

        req.TipoDteCodigo.Should().Be(TipoDteCodigos.FacturaConsumidorFinal);
        await connect.Received(1).EmitirAsync(7, req, "vendedor1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuperAdmin_UsaEmpresaIdDeQuery()
    {
        var (ctrl, connect, _) = Build(empresaId: null); // SuperAdmin sin empresa fija
        connect.EmitirAsync(33, Arg.Any<CreateDteDocumentoRequest>(), "vendedor1", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(new DteDocumentoDto { Id = 1, EstadoCodigo = "PROCESADO" }));

        var result = await ctrl.Emitir(new CreateDteDocumentoRequest(), empresaId: 33, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        await connect.Received(1).EmitirAsync(33, Arg.Any<CreateDteDocumentoRequest>(), "vendedor1", Arg.Any<CancellationToken>());
    }
}
