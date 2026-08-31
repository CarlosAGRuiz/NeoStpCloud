using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class DteEmisorSaneamientoTests
{
    private const int EmpresaId = 23;

    [Fact]
    public async Task Generar_SaneaEmisorConNombresTerritorialesAntesDelJson()
    {
        await using var db = CreateDb();
        var empresa = new Empresa
        {
            Id = EmpresaId,
            Nit = "0614-010100-101-1",
            Nrc = "123456-7",
            RazonSocial = "DANIEL IMPORTADORA Y DISTRIBUIDORA",
            CodigoActividad = "46900",
            ActividadEconomica = "Venta al por mayor de productos varios",
            Departamento = "La Libertad",
            Municipio = "La Libertad Centro",
            Direccion = "Direccion fiscal",
            Telefono = "22220000",
            Correo = "emisor@example.test",
            EstadoCodigo = EmpresaEstados.Activa,
        };
        var config = new DteConfiguracion
        {
            EmpresaId = EmpresaId,
            AmbienteCodigo = "PRUEBAS",
            TipoEstablecimientoCodigo = "CASA_MATRIZ",
            CodigoEstablecimientoMh = "M001",
            CodigoPuntoVentaMh = "P001",
        };
        var documento = Documento();

        db.Empresas.Add(empresa);
        db.DteConfiguracion.Add(config);
        db.DteDocumentos.Add(documento);
        await db.SaveChangesAsync();

        DteDocumento? capturado = null;
        DteConfiguracion? configCapturada = null;
        var generator = Substitute.For<IDteGeneratorService>();
        generator.Generar(Arg.Any<DteDocumento>(), Arg.Any<DteConfiguracion?>())
            .Returns(call =>
            {
                capturado = call.ArgAt<DteDocumento>(0);
                configCapturada = call.ArgAt<DteConfiguracion?>(1);
                return Result<string>.Ok("{}");
            });

        var service = CreateService(db, generator, Lookup());

        var result = await service.GenerarAsync(EmpresaId, documento.Id, "test");

        result.IsSuccess.Should().BeTrue(result.Error);
        capturado!.Empresa!.Nit.Should().Be("06140101001011");
        capturado.Empresa.Nrc.Should().Be("1234567");
        capturado.Empresa.Departamento.Should().Be("05");
        capturado.Empresa.Municipio.Should().Be("22");
        configCapturada!.TipoEstablecimientoCodigo.Should().Be("02");

        var empresaPersistida = await db.Empresas.AsNoTracking().SingleAsync(e => e.Id == EmpresaId);
        empresaPersistida.Nit.Should().Be("0614-010100-101-1");
        empresaPersistida.Departamento.Should().Be("La Libertad");
        empresaPersistida.Municipio.Should().Be("La Libertad Centro");
    }

    private static NeoStpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"dte-emisor-saneamiento-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    private static DteDocumento Documento()
    {
        var doc = new DteDocumento
        {
            EmpresaId = EmpresaId,
            TipoDteCodigo = TipoDteCodigos.FacturaConsumidorFinal,
            NumeroControl = "DTE-01-M001P001-000000000000009",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            FechaEmision = new DateTime(2026, 8, 31),
            HoraEmision = new TimeSpan(12, 36, 0),
            TipoMonedaCodigo = "USD",
            CondicionOperacionCodigo = "1",
            ModeloFacturacion = 1,
            TipoTransmision = 1,
            ReceptorNombre = "Consumidor Final",
            EstadoCodigo = DteEstadoCodigos.Borrador,
            AmbienteCodigo = "PRUEBAS",
        };
        doc.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1,
            Codigo = "ITEM-1",
            Descripcion = "Producto de prueba",
            Cantidad = 1m,
            PrecioUnitario = 100m,
            VentaGravada = 100m,
            UnidadMedidaCodigo = "59",
            TipoItem = 1,
        });
        return doc;
    }

    private static ILookupService Lookup()
    {
        var lookup = Substitute.For<ILookupService>();
        lookup.GetCatalogoAsync(CatalogCodes.DepartamentoEs, EmpresaId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LookupItem>>(
                [new LookupItem("LA_LIBERTAD", "La Libertad", null, "{\"codigoMH\":\"05\"}")]));
        lookup.GetCatalogoAsync(CatalogCodes.MunicipioEs, EmpresaId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LookupItem>>(
                [new LookupItem("LA_LIBERTAD_CENTRO", "La Libertad Centro", "LA_LIBERTAD", "{\"codigoMH\":\"22\"}")]));
        lookup.GetCatalogoAsync(CatalogCodes.TipoEstablecimiento, EmpresaId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LookupItem>>(
                [new LookupItem("CASA_MATRIZ", "Casa Matriz", null, "{\"codigoMH\":\"02\"}")]));
        return lookup;
    }

    private static DteDocumentosService CreateService(
        NeoStpDbContext db,
        IDteGeneratorService generator,
        ILookupService lookup)
    {
        var auditoria = Substitute.For<IAuditoriaService>();
        auditoria.RegistrarAsync(Arg.Any<AuditoriaEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return new DteDocumentosService(
            db,
            new DteCalculator(),
            generator,
            Substitute.For<IDteSignerService>(),
            Substitute.For<IHaciendaReceptionClient>(),
            Substitute.For<IHaciendaContingenciaClient>(),
            Substitute.For<IHaciendaEventoClient>(),
            Substitute.For<IHaciendaAuthClient>(),
            Substitute.For<ISecretProtector>(),
            Substitute.For<IDtePdfService>(),
            Substitute.For<ITenantEmailSender>(),
            auditoria,
            Substitute.For<IConnectWebhookDispatcher>(),
            lookup: lookup);
    }
}
