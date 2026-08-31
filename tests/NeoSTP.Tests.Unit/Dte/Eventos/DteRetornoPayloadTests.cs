using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte.Eventos;

/// <summary>
/// Regresiones del contrato ERET que Hacienda valida además del JSON Schema público.
/// </summary>
public class DteRetornoPayloadTests
{
    private const int EmpresaId = 10;

    [Fact]
    public async Task Retorno_Fe_UsaCodigosMhAlfanumericos_SinTributo20_YConTotalLetras()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"eret-{Guid.NewGuid()}")
            .Options;
        await using var db = new NeoStpDbContext(options);

        db.Empresas.Add(new Empresa
        {
            Id = EmpresaId,
            Nit = "06142608991012",
            RazonSocial = "Empresa ERET",
            EstadoCodigo = "ACTIVA",
        });
        db.DteConfiguracion.Add(new DteConfiguracion
        {
            EmpresaId = EmpresaId,
            AmbienteCodigo = "PRUEBAS",
            TipoEstablecimientoCodigo = "CASA_MATRIZ",
            CodigoEstablecimientoMh = "0001",
            CodigoPuntoVentaMh = "0001",
            CertificadoBlob = [1],
        });
        var documento = new DteDocumento
        {
            EmpresaId = EmpresaId,
            TipoDteCodigo = TipoDteCodigos.FacturaConsumidorFinal,
            NumeroControl = "DTE-01-M001P001-000000000000001",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = DteEstadoCodigos.Procesado,
            FechaEmision = DateTime.UtcNow.Date,
            TotalGravada = 113m,
        };
        db.DteDocumentos.Add(documento);
        await db.SaveChangesAsync();

        var signer = Substitute.For<IDteSignerService>();
        signer.FirmarAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new DteSignResult { Success = false, Detalle = "Fallo controlado para inspeccionar JSON" });

        var service = new DteDocumentosService(
            db,
            new DteCalculator(),
            Substitute.For<IDteGeneratorService>(),
            signer,
            Substitute.For<IHaciendaReceptionClient>(),
            Substitute.For<IHaciendaContingenciaClient>(),
            Substitute.For<IHaciendaEventoClient>(),
            Substitute.For<IHaciendaAuthClient>(),
            Substitute.For<ISecretProtector>(),
            Substitute.For<IDtePdfService>(),
            Substitute.For<ITenantEmailSender>(),
            Substitute.For<IAuditoriaService>(),
            Substitute.For<IConnectWebhookDispatcher>());

        var result = await service.TransmitirEventoRetornoAsync(EmpresaId, documento.Id, "test");

        result.ErrorCode.Should().Be("FIRMA_FAILED");
        var evento = await db.DteEventos
            .Include(e => e.Json)
            .SingleAsync(e => e.TipoEventoCodigo == TipoEventoCodigos.Retorno);
        using var eret = JsonDocument.Parse(evento.Json!.JsonSinFirmar);
        var root = eret.RootElement;

        root.GetProperty("identificacion").GetProperty("tipoEvento").GetString().Should().Be("18");
        root.GetProperty("documentoRelacionado")[0].GetProperty("tipoDocumento").GetString().Should().Be("01");

        var emisor = root.GetProperty("emisor");
        emisor.GetProperty("codEstableMH").GetString().Should().Be("M001");
        emisor.GetProperty("codPuntoVentaMH").GetString().Should().Be("P001");
        emisor.GetProperty("codEstable").GetString().Should().Be("0001");
        emisor.GetProperty("codPuntoVenta").GetString().Should().Be("0001");

        var item = root.GetProperty("cuerpoDocumento")[0];
        item.GetProperty("tributos").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("codTributo").ValueKind.Should().Be(JsonValueKind.Null);

        var resumen = root.GetProperty("resumen");
        resumen.GetProperty("tributos").ValueKind.Should().Be(JsonValueKind.Null);
        resumen.GetProperty("totalLetras").GetString().Should().NotBeNullOrWhiteSpace();
        resumen.GetProperty("totalIva").GetDecimal().Should().Be(13m);
    }

    [Theory]
    [InlineData("03")]
    [InlineData("05")]
    [InlineData("09")]
    [InlineData("15")]
    public async Task Retorno_RechazaTiposDteNoAdmitidos(string tipoDte)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"eret-tipo-{Guid.NewGuid()}")
            .Options;
        await using var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaId, Nit = "06142608991012", RazonSocial = "Empresa ERET" });
        db.DteConfiguracion.Add(new DteConfiguracion { EmpresaId = EmpresaId, CertificadoBlob = [1] });
        var documento = new DteDocumento
        {
            EmpresaId = EmpresaId,
            TipoDteCodigo = tipoDte,
            NumeroControl = $"DTE-{tipoDte}-M001P001-000000000000001",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = DteEstadoCodigos.Procesado,
        };
        db.DteDocumentos.Add(documento);
        await db.SaveChangesAsync();

        var service = new DteDocumentosService(
            db,
            new DteCalculator(),
            Substitute.For<IDteGeneratorService>(),
            Substitute.For<IDteSignerService>(),
            Substitute.For<IHaciendaReceptionClient>(),
            Substitute.For<IHaciendaContingenciaClient>(),
            Substitute.For<IHaciendaEventoClient>(),
            Substitute.For<IHaciendaAuthClient>(),
            Substitute.For<ISecretProtector>(),
            Substitute.For<IDtePdfService>(),
            Substitute.For<ITenantEmailSender>(),
            Substitute.For<IAuditoriaService>(),
            Substitute.For<IConnectWebhookDispatcher>());

        var result = await service.TransmitirEventoRetornoAsync(EmpresaId, documento.Id, "test");

        result.ErrorCode.Should().Be("INVALID_DTE_TYPE");
        (await db.DteEventos.CountAsync()).Should().Be(0);
    }
}
