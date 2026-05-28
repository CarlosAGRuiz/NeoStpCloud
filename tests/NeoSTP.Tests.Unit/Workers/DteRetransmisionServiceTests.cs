using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Workers;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Workers;

public class DteRetransmisionServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static NeoStpDbContext BuildContext(string name)
        => new(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase(name).Options);

    private static WorkerOptions DefaultOpts(
        int maxIntentos = 5,
        int cooldownMinutos = 30,
        int loteMaximo = 50) => new()
    {
        RetransmisionContingencia = new()
        {
            MaxIntentos = maxIntentos,
            CooldownMinutos = cooldownMinutos,
            LoteMaximo = loteMaximo,
        },
    };

    private static DteDocument BuildDoc(
        int id, int empresaId,
        string estado = DteEstadoCodigos.Contingencia,
        int intentos = 0,
        DateTime? ultimoIntento = null)
        => new DteDocument(id, empresaId, estado, intentos, ultimoIntento);

    private sealed class DteDocument
    {
        public int Id { get; }
        public int EmpresaId { get; }
        public string Estado { get; }
        public int Intentos { get; }
        public DateTime? UltimoIntento { get; }
        public DteDocument(int id, int emp, string estado, int intentos, DateTime? ultimo)
            => (Id, EmpresaId, Estado, Intentos, UltimoIntento) = (id, emp, estado, intentos, ultimo);
    }

    private static DteDocumento MakeDteDocumento(
        int id, int empresaId,
        string estado = DteEstadoCodigos.Contingencia,
        int intentos = 0,
        DateTime? ultimoIntento = null)
        => new()
        {
            Id = id,
            EmpresaId = empresaId,
            TipoDteCodigo = TipoDteCodigos.FacturaConsumidorFinal,
            NumeroControl = $"DTE-01-00010001-{id:D15}",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = estado,
            FechaEmision = DateTime.UtcNow.Date,
            IntentoRetransmision = intentos,
            UltimoIntentoRetransmisionAt = ultimoIntento,
        };

    private static IDteDocumentosService MockSvcExitoso(string estadoFinal = DteEstadoCodigos.Procesado)
    {
        var svc = Substitute.For<IDteDocumentosService>();
        svc.EnviarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Result<DteDocumentoDto>.Ok(new DteDocumentoDto { EstadoCodigo = estadoFinal }));
        return svc;
    }

    private static IDteDocumentosService MockSvcFallido(string error = "HACIENDA_AUTH_FAILED")
    {
        var svc = Substitute.For<IDteDocumentosService>();
        svc.EnviarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Result<DteDocumentoDto>.Fail(error, error));
        return svc;
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SinDocumentosContingencia_DevuelveCero()
    {
        await using var db = BuildContext(nameof(SinDocumentosContingencia_DevuelveCero));
        var svc = new DteRetransmisionService(db,
            MockSvcExitoso(), Options.Create(DefaultOpts()),
            NullLogger<DteRetransmisionService>.Instance);

        var resultado = await svc.RetransmitirContingenciasAsync();

        resultado.Procesados.Should().Be(0);
        resultado.Exitosos.Should().Be(0);
    }

    [Fact]
    public async Task DocumentoContingencia_SinIntentos_SeRetransmite()
    {
        await using var db = BuildContext(nameof(DocumentoContingencia_SinIntentos_SeRetransmite));
        var doc = MakeDteDocumento(1, 10, DteEstadoCodigos.Contingencia, 0, null);
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        var svc = new DteRetransmisionService(db,
            MockSvcExitoso(DteEstadoCodigos.Procesado), Options.Create(DefaultOpts()),
            NullLogger<DteRetransmisionService>.Instance);

        var resultado = await svc.RetransmitirContingenciasAsync();

        resultado.Procesados.Should().Be(1);
        resultado.Exitosos.Should().Be(1);
        resultado.Fallidos.Should().Be(0);
    }

    [Fact]
    public async Task DocumentoContingencia_SuperaMaxIntentos_NoSeRetransmite()
    {
        await using var db = BuildContext(nameof(DocumentoContingencia_SuperaMaxIntentos_NoSeRetransmite));
        // maxIntentos = 3, doc ya tiene 3 intentos → no debe procesarse
        var doc = MakeDteDocumento(1, 10, DteEstadoCodigos.Contingencia, 3, null);
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        var svc = new DteRetransmisionService(db,
            MockSvcExitoso(), Options.Create(DefaultOpts(maxIntentos: 3)),
            NullLogger<DteRetransmisionService>.Instance);

        var resultado = await svc.RetransmitirContingenciasAsync();

        resultado.Procesados.Should().Be(0);
    }

    [Fact]
    public async Task DocumentoContingencia_EnCooldown_NoSeRetransmite()
    {
        await using var db = BuildContext(nameof(DocumentoContingencia_EnCooldown_NoSeRetransmite));
        // Último intento hace 10 min, cooldown = 30 min → todavía en cooldown
        var doc = MakeDteDocumento(1, 10, DteEstadoCodigos.Contingencia, 1, DateTime.UtcNow.AddMinutes(-10));
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        var svc = new DteRetransmisionService(db,
            MockSvcExitoso(), Options.Create(DefaultOpts(cooldownMinutos: 30)),
            NullLogger<DteRetransmisionService>.Instance);

        var resultado = await svc.RetransmitirContingenciasAsync();

        resultado.Procesados.Should().Be(0);
    }

    [Fact]
    public async Task DocumentoContingencia_CooldownCumplido_SeRetransmite()
    {
        await using var db = BuildContext(nameof(DocumentoContingencia_CooldownCumplido_SeRetransmite));
        // Último intento hace 40 min, cooldown = 30 min → ya puede reintentarse
        var doc = MakeDteDocumento(1, 10, DteEstadoCodigos.Contingencia, 1, DateTime.UtcNow.AddMinutes(-40));
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        var svc = new DteRetransmisionService(db,
            MockSvcExitoso(), Options.Create(DefaultOpts(cooldownMinutos: 30)),
            NullLogger<DteRetransmisionService>.Instance);

        var resultado = await svc.RetransmitirContingenciasAsync();

        resultado.Procesados.Should().Be(1);
        resultado.Exitosos.Should().Be(1);
    }

    [Fact]
    public async Task DocumentoNoContingencia_NoSeRetransmite()
    {
        await using var db = BuildContext(nameof(DocumentoNoContingencia_NoSeRetransmite));
        db.DteDocumentos.AddRange(
            MakeDteDocumento(1, 10, DteEstadoCodigos.Procesado),
            MakeDteDocumento(2, 10, DteEstadoCodigos.Rechazado),
            MakeDteDocumento(3, 10, DteEstadoCodigos.Borrador)
        );
        await db.SaveChangesAsync();

        var svc = new DteRetransmisionService(db,
            MockSvcExitoso(), Options.Create(DefaultOpts()),
            NullLogger<DteRetransmisionService>.Instance);

        var resultado = await svc.RetransmitirContingenciasAsync();

        resultado.Procesados.Should().Be(0);
    }

    [Fact]
    public async Task EnvioFalla_CuentaComoFallido_IncrementaIntento()
    {
        await using var db = BuildContext(nameof(EnvioFalla_CuentaComoFallido_IncrementaIntento));
        var doc = MakeDteDocumento(1, 10, DteEstadoCodigos.Contingencia, 0, null);
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        var svc = new DteRetransmisionService(db,
            MockSvcFallido(), Options.Create(DefaultOpts()),
            NullLogger<DteRetransmisionService>.Instance);

        var resultado = await svc.RetransmitirContingenciasAsync();

        resultado.Procesados.Should().Be(1);
        resultado.Fallidos.Should().Be(1);
        resultado.Exitosos.Should().Be(0);

        // El contador de intentos se habrá incrementado
        var docActualizado = await db.DteDocumentos.FindAsync(1);
        docActualizado!.IntentoRetransmision.Should().Be(1);
    }

    [Fact]
    public async Task LoteMaximoRespetado()
    {
        await using var db = BuildContext(nameof(LoteMaximoRespetado));
        // 10 documentos en contingencia, lote = 3
        for (var i = 1; i <= 10; i++)
            db.DteDocumentos.Add(MakeDteDocumento(i, 10, DteEstadoCodigos.Contingencia));
        await db.SaveChangesAsync();

        var svc = new DteRetransmisionService(db,
            MockSvcExitoso(), Options.Create(DefaultOpts(loteMaximo: 3)),
            NullLogger<DteRetransmisionService>.Instance);

        var resultado = await svc.RetransmitirContingenciasAsync();

        resultado.Procesados.Should().Be(3);
    }
}
