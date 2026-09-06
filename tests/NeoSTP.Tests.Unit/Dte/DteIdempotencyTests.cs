using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Api.Controllers;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class DteIdempotencyTests
{
    internal static CreateDteDocumentoRequest Request(string key = "sale-123") => new()
    {
        IdempotencyKey = key, ReceptorManual = new() { Nombre = "SYNTHETIC" },
        Lineas = [new() { Codigo = "TEST", Descripcion = "SYNTHETIC", Cantidad = 1, PrecioUnitario = 100 }]
    };

    [Theory]
    [InlineData(null, true)]
    [InlineData("sale-ABC:123", true)]
    [InlineData("", false)]
    [InlineData(" sale ", false)]
    [InlineData("venta\n123", false)]
    [InlineData("venta-á", false)]
    public void Clave_ValidacionSinNormalizacionAmbigua(string? key, bool valid) =>
        DteIdempotency.ValidateKey(key).IsSuccess.Should().Be(valid);

    [Fact]
    public void Huella_MismosImportesEquivalentes_IgnoraSoloLaClave()
    {
        var first = Request();
        var second = Request("another-key");
        second.Lineas[0].PrecioUnitario = 100.00m;
        second.Lineas[0].Cantidad = 1.0000m;
        DteIdempotency.Fingerprint(first).Should().Be(DteIdempotency.Fingerprint(second));
        second.Lineas[0].PrecioUnitario = 101;
        DteIdempotency.Fingerprint(first).Should().NotBe(DteIdempotency.Fingerprint(second));
        DteIdempotency.HashKey("Sale").Should().NotBe(DteIdempotency.HashKey("sale"));
    }

    [Fact]
    public void OrigenPos_NoSeAceptaDesdeJson()
    {
        var req = JsonSerializer.Deserialize<CreateDteDocumentoRequest>(
            """{"VentaPosOrigenId":42,"IdempotencyKey":"sale"}""")!;
        req.VentaPosOrigenId.Should().BeNull();
        req.IdempotencyKey.Should().Be("sale");
    }

    [Fact]
    public void Header_AplicaClaveYRechazaValoresAmbiguos()
    {
        var http = new DefaultHttpContext();
        var req = Request(); req.IdempotencyKey = null;
        http.Request.Headers["Idempotency-Key"] = "same-key";
        DteIdempotencyHeader.Apply(http.Request, req).IsSuccess.Should().BeTrue();
        req.IdempotencyKey.Should().Be("same-key");
        req.IdempotencyKey = "changed";
        DteIdempotencyHeader.Apply(http.Request, req).ErrorCode.Should().Be("IDEMPOTENCY_KEY_INVALID");
        req.IdempotencyKey = null;
        http.Request.Headers["Idempotency-Key"] = new[] { "one", "two" };
        DteIdempotencyHeader.Apply(http.Request, req).IsFailure.Should().BeTrue();
        http.Request.Headers["Idempotency-Key"] = new string('a', 129);
        DteIdempotencyHeader.Apply(http.Request, req).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("BORRADOR")]
    [InlineData("PROCESADO")]
    [InlineData("ERROR")]
    [InlineData("CONTINGENCIA")]
    [InlineData("INVALIDADO")]
    public async Task Replay_DevuelveMismoDocumentoSinConsumirCupoNiEjecutarPipeline(string state)
    {
        await using var db = DteFiscalIsolationTests.Db();
        var request = Request();
        var doc = DteFiscalIsolationTests.Documento();
        doc.EstadoCodigo = state; doc.IdempotencyScope = "DTE";
        doc.IdempotencyKeyHash = DteIdempotency.HashKey(request.IdempotencyKey!);
        doc.IdempotencyRequestHash = DteIdempotency.Fingerprint(request);
        db.DteDocumentos.Add(doc);
        db.DteConfiguracion.Add(new() { EmpresaId = 10 });
        await db.SaveChangesAsync();
        var guard = Substitute.For<ILicenciaGuardService>();
        var generator = Substitute.For<IDteGeneratorService>();
        var service = new DteDocumentosService(db, new DteCalculator(), generator, Substitute.For<IDteSignerService>(),
            Substitute.For<IHaciendaReceptionClient>(), Substitute.For<IHaciendaContingenciaClient>(),
            Substitute.For<IHaciendaEventoClient>(), Substitute.For<IHaciendaAuthClient>(),
            DteFiscalIsolationTests.Protector(), Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
            Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>(), licenciaGuard: guard);
        var result = await new ConnectDteService(service).EmitirAsync(10, request, "retry");
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Id.Should().Be(doc.Id);
        result.Value.EstadoCodigo.Should().Be(state);
        result.Value.IdempotencyReplayed.Should().BeTrue();
        generator.ReceivedCalls().Should().BeEmpty();
        guard.ReceivedCalls().Should().BeEmpty();
        (await db.DteDocumentos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FalloTrasCrear_ConservaReferenciaEnResultado()
    {
        var dte = Substitute.For<IDteDocumentosService>();
        var request = Request();
        var doc = new DteDocumentoDto { Id = 77, EstadoCodigo = "BORRADOR" };
        dte.CreateBorradorAsync(10, request, "actor", Arg.Any<CancellationToken>()).Returns(Result<DteDocumentoDto>.Ok(doc));
        dte.GenerarAsync(10, 77, "actor", Arg.Any<CancellationToken>()).Returns(Result<DteDocumentoDto>.Fail("Corregir emisor", "VALIDATION"));
        dte.GetByIdAsync(10, 77, Arg.Any<CancellationToken>()).Returns(Result<DteDocumentoDto>.Ok(doc));
        var result = await new ConnectDteService(dte).EmitirAsync(10, request, "actor");
        result.IsFailure.Should().BeTrue();
        result.Value!.Id.Should().Be(77);
        result.ErrorCode.Should().Be("VALIDATION");
        await dte.DidNotReceive().EnviarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
