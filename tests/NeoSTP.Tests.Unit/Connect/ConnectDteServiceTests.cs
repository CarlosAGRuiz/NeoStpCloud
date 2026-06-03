using FluentAssertions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Connect;

/// <summary>
/// NeoConnect — orquestación de emisión (EmitirAsync) que encadena el pipeline
/// borrador → generar → validar → firmar → enviar reusando IDteDocumentosService.
/// </summary>
public class ConnectDteServiceTests
{
    private const int EmpresaA = 7;

    private static DteDocumentoDto Doc(int id, string estado) => new() { Id = id, EstadoCodigo = estado };

    [Fact]
    public async Task EmitirAsync_PipelineCompleto_DevuelveProcesado()
    {
        var dte = Substitute.For<IDteDocumentosService>();
        var req = new CreateDteDocumentoRequest();

        dte.CreateBorradorAsync(EmpresaA, req, "actor", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(Doc(42, "BORRADOR")));
        dte.GenerarAsync(EmpresaA, 42, "actor", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(Doc(42, "GENERADO")));
        dte.ValidarAsync(EmpresaA, 42, "actor", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(Doc(42, "VALIDADO")));
        dte.FirmarAsync(EmpresaA, 42, "actor", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(Doc(42, "FIRMADO")));
        dte.EnviarAsync(EmpresaA, 42, "actor", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(Doc(42, "PROCESADO")));

        var svc = new ConnectDteService(dte);
        var r = await svc.EmitirAsync(EmpresaA, req, "actor");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EstadoCodigo.Should().Be("PROCESADO");
        await dte.Received(1).EnviarAsync(EmpresaA, 42, "actor", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirAsync_FallaEnValidar_DetieneYNoFirma()
    {
        var dte = Substitute.For<IDteDocumentosService>();
        var req = new CreateDteDocumentoRequest();

        dte.CreateBorradorAsync(EmpresaA, req, null, Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(Doc(9, "BORRADOR")));
        dte.GenerarAsync(EmpresaA, 9, null, Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(Doc(9, "GENERADO")));
        dte.ValidarAsync(EmpresaA, 9, null, Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Fail("Esquema inválido", "VALIDATION"));

        var svc = new ConnectDteService(dte);
        var r = await svc.EmitirAsync(EmpresaA, req, null);

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
        await dte.DidNotReceive().FirmarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await dte.DidNotReceive().EnviarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirAsync_FallaEnBorrador_NoContinua()
    {
        var dte = Substitute.For<IDteDocumentosService>();
        var req = new CreateDteDocumentoRequest();

        dte.CreateBorradorAsync(EmpresaA, req, null, Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Fail("Cliente no existe", "CLIENTE_NOT_FOUND"));

        var svc = new ConnectDteService(dte);
        var r = await svc.EmitirAsync(EmpresaA, req, null);

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("CLIENTE_NOT_FOUND");
        await dte.DidNotReceive().GenerarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
