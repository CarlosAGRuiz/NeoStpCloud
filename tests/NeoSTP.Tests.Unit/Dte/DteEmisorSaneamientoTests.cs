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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    public async Task Generar_PersisteVersionRealDelJsonEnLugarDeVersionLegacy(int version)
    {
        await using var db = await VersionFixtureAsync();
        var document = await db.DteDocumentos.SingleAsync();
        document.VersionDte.Should().Be(1);
        var generator = Substitute.For<IDteGeneratorService>();
        generator.Generar(Arg.Any<DteDocumento>(), Arg.Any<DteConfiguracion?>()).Returns(call =>
            Result<string>.Ok(DteFiscalIsolationTests.Payload(call.Arg<DteDocumento>()).Replace("\"version\":1", $"\"version\":{version}")));
        var result = await CreateService(db, generator, Lookup()).GenerarAsync(EmpresaId, document.Id, "test");
        result.IsSuccess.Should().BeTrue(result.Error);
        db.ChangeTracker.Clear();
        var persisted = await db.DteDocumentos.Include(d => d.Json).SingleAsync();
        persisted.VersionDte.Should().Be(version);
        using var json = System.Text.Json.JsonDocument.Parse(persisted.Json!.JsonDte);
        json.RootElement.GetProperty("identificacion").GetProperty("version").GetInt32().Should().Be(persisted.VersionDte);
    }

    [Theory]
    [InlineData("\"version\":0")]
    [InlineData("\"version\":-1")]
    [InlineData("\"version\":2.5")]
    [InlineData("\"version\":\"2\"")]
    [InlineData("\"version\":null")]
    [InlineData("\"version\":2147483648")]
    [InlineData("\"version\":1,\"version\":2")]
    [InlineData("\"other\":2")]
    public async Task Generar_NoPersisteVersionInvalidaONoUnivoca(string replacement)
    {
        await using var db = await VersionFixtureAsync();
        var document = await db.DteDocumentos.SingleAsync();
        var generator = Substitute.For<IDteGeneratorService>();
        generator.Generar(Arg.Any<DteDocumento>(), Arg.Any<DteConfiguracion?>()).Returns(call =>
            Result<string>.Ok(DteFiscalIsolationTests.Payload(call.Arg<DteDocumento>()).Replace("\"version\":1", replacement)));
        var result = await CreateService(db, generator, Lookup()).GenerarAsync(EmpresaId, document.Id, "test");
        result.ErrorCode.Should().Be("DTE_PAYLOAD_INCOMPATIBLE");
        db.ChangeTracker.Clear();
        var persisted = await db.DteDocumentos.Include(d => d.Json).SingleAsync();
        persisted.VersionDte.Should().Be(1);
        persisted.EstadoCodigo.Should().Be(DteEstadoCodigos.Borrador);
        persisted.Json.Should().BeNull();
    }

    private static async Task<NeoStpDbContext> VersionFixtureAsync()
    {
        var db = CreateDb();
        db.Empresas.Add(new Empresa { Id = EmpresaId, Nit = "06140101001011", RazonSocial = "Synthetic issuer",
            Departamento = "La Libertad", Municipio = "La Libertad Centro", Telefono = "22220000", Correo = "synthetic@example.invalid" });
        db.DteConfiguracion.Add(new DteConfiguracion { EmpresaId = EmpresaId, AmbienteCodigo = "PRUEBAS",
            TipoEstablecimientoCodigo = "02", CodigoEstablecimientoMh = "M001", CodigoPuntoVentaMh = "P001" });
        var doc = Documento(); doc.VersionDte = 1;
        db.DteDocumentos.Add(doc); await db.SaveChangesAsync(); return db;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generar_RechazaMunicipioDeOtroPadreAntesDePerderSuIdentidad(bool issuer)
    {
        await using var db = CreateDb();
        db.Empresas.Add(new Empresa { Id = EmpresaId, Nit = "06140101001011", RazonSocial = "Synthetic issuer",
            Departamento = "La Libertad", Municipio = issuer ? "OTRO_MUNICIPIO" : "LA_LIBERTAD_CENTRO",
            Telefono = "22220000", Correo = "synthetic@example.invalid" });
        db.DteConfiguracion.Add(new DteConfiguracion { EmpresaId = EmpresaId, AmbienteCodigo = "PRUEBAS",
            TipoEstablecimientoCodigo = "02", CodigoEstablecimientoMh = "M001", CodigoPuntoVentaMh = "P001" });
        var doc = Documento();
        if (!issuer) { doc.ReceptorDepartamentoCodigo = "La Libertad"; doc.ReceptorMunicipioCodigo = "OTRO_MUNICIPIO"; }
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();
        var lookup = Lookup();
        lookup.GetCatalogoAsync(CatalogCodes.MunicipioEs, EmpresaId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LookupItem>>([
                new("LA_LIBERTAD_CENTRO", "La Libertad Centro", "LA_LIBERTAD", "{\"codigoMH\":\"22\"}"),
                new("OTRO_MUNICIPIO", "Otro municipio", "OTRO_DEPARTAMENTO", "{\"codigoMH\":\"22\"}")]));
        var generator = Substitute.For<IDteGeneratorService>();
        var result = await CreateService(db, generator, lookup).GenerarAsync(EmpresaId, doc.Id, "test");
        result.ErrorCode.Should().Be("DTE_TERRITORIO_MUNICIPIO");
        generator.DidNotReceive().Generar(Arg.Any<DteDocumento>(), Arg.Any<DteConfiguracion?>());
        db.ChangeTracker.Clear();
        (await db.Empresas.SingleAsync()).Municipio.Should().Be(issuer ? "OTRO_MUNICIPIO" : "LA_LIBERTAD_CENTRO");
        if (!issuer) (await db.DteDocumentos.SingleAsync()).ReceptorMunicipioCodigo.Should().Be("OTRO_MUNICIPIO");
    }

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

        string? nitCapturado = null;
        string? nrcCapturado = null;
        string? departamentoCapturado = null;
        string? municipioCapturado = null;
        DteConfiguracion? configCapturada = null;
        var generator = Substitute.For<IDteGeneratorService>();
        generator.Generar(Arg.Any<DteDocumento>(), Arg.Any<DteConfiguracion?>())
            .Returns(call =>
            {
                var capturado = call.ArgAt<DteDocumento>(0);
                nitCapturado = capturado.Empresa?.Nit;
                nrcCapturado = capturado.Empresa?.Nrc;
                departamentoCapturado = capturado.Empresa?.Departamento;
                municipioCapturado = capturado.Empresa?.Municipio;
                configCapturada = call.ArgAt<DteConfiguracion?>(1);
                return Result<string>.Ok(DteFiscalIsolationTests.Payload(capturado));
            });

        var service = CreateService(db, generator, Lookup());

        var result = await service.GenerarAsync(EmpresaId, documento.Id, "test");

        result.IsSuccess.Should().BeTrue(result.Error);
        nitCapturado.Should().Be("06140101001011");
        nrcCapturado.Should().Be("1234567");
        departamentoCapturado.Should().Be("05");
        municipioCapturado.Should().Be("22");
        configCapturada!.TipoEstablecimientoCodigo.Should().Be("02");

        var empresaPersistida = await db.Empresas.AsNoTracking().SingleAsync(e => e.Id == EmpresaId);
        empresaPersistida.Nit.Should().Be("0614-010100-101-1");
        empresaPersistida.Departamento.Should().Be("La Libertad");
        empresaPersistida.Municipio.Should().Be("La Libertad Centro");
    }

    internal static NeoStpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"dte-emisor-saneamiento-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    internal static DteDocumento Documento()
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

    internal static ILookupService Lookup()
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

    internal static DteDocumentosService CreateService(
        NeoStpDbContext db,
        IDteGeneratorService generator,
        ILookupService lookup,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
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
            lookup: lookup, configuration: configuration);
    }
}
