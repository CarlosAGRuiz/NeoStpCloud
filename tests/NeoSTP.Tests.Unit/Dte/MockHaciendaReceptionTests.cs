using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class MockHaciendaReceptionTests
{
    private static MockHaciendaReceptionClient Build()
        => new(NullLogger<MockHaciendaReceptionClient>.Instance);

    private static HaciendaReceptionRequest BaseReq(string codigo = "ABCDEF12-3456")
        => new()
        {
            Ambiente = "00",
            AmbienteCodigo = "PRUEBAS",
            IdEnvio = 1,
            TipoDte = "01",
            Documento = "header.payload.signature",
            CodigoGeneracion = codigo,
            Token = "tok",
        };

    [Fact]
    public async Task Procesado_DevuelveSelloYEstadoOk()
    {
        var r = await Build().EnviarAsync(BaseReq());
        r.Success.Should().BeTrue();
        r.Estado.Should().Be("PROCESADO");
        r.SelloRecibido.Should().NotBeNullOrEmpty();
        r.CodigoMsg.Should().Be("000");
    }

    [Fact]
    public async Task DocumentoVacio_Falla400()
    {
        var req = BaseReq();
        req.Documento = "";
        var r = await Build().EnviarAsync(req);
        r.Success.Should().BeFalse();
        r.CodigoHttp.Should().Be(400);
    }

    [Fact]
    public async Task TokenVacio_Falla401()
    {
        var req = BaseReq();
        req.Token = "";
        var r = await Build().EnviarAsync(req);
        r.Success.Should().BeFalse();
        r.CodigoHttp.Should().Be(401);
        r.Estado.Should().Be("ERROR");
    }

    [Fact]
    public async Task CodigoConRech_DevuelveRechazado()
    {
        var r = await Build().EnviarAsync(BaseReq("CODIGORECH123"));
        r.Estado.Should().Be("RECHAZADO");
        r.SelloRecibido.Should().BeNull();
        // Success aquí es true porque MH respondió 200 — pero el estado interno
        // del documento se marca RECHAZADO; el servicio mapea eso correctamente.
        r.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CodigoConCont_DevuelveContingencia()
    {
        var r = await Build().EnviarAsync(BaseReq("CODIGOCONT789"));
        r.Estado.Should().Be("CONTINGENCIA");
        r.ClasificaMsg.Should().Be("ADVERTENCIA");
    }
}
