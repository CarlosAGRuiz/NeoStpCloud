using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Api.Controllers;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class DteTypeAuthorizationTests
{
    [Theory]
    [InlineData("01,03,11,14", 4)]
    [InlineData("01", 1)]
    [InlineData(null, 11)]
    [InlineData("", 0)]
    [InlineData("01, 03", 0)]
    [InlineData("01,01", 0)]
    [InlineData("01,99", 0)]
    [InlineData("1", 0)]
    [InlineData("01,", 0)]
    [InlineData("*", 0)]
    public void RestrictionIsExactAndMalformedValuesFailClosed(string? csv, int count)
        => DteTypeAuthorization.Resolve(csv).Should().HaveCount(count);

    [Fact]
    public async Task ListUsesCurrentTenantRestrictionWithoutCacheOrGlobalChanges()
    {
        await using var f = await Fixture.Create();
        f.Db.DteConfiguracion.Add(new() { EmpresaId = 20, TiposDteAutorizadosCsv = "07" });
        await f.Db.SaveChangesAsync();
        (await f.Service.GetTiposDisponiblesAsync(10)).Select(t => t.Codigo).Should().Equal("01", "03", "11", "14");
        (await f.Service.GetTiposDisponiblesAsync(20)).Select(t => t.Codigo).Should().Equal("07");
        (await f.Service.GetTiposDisponiblesAsync(999)).Should().BeEmpty();
        f.Config.TiposDteAutorizadosCsv = "14";
        await f.Db.SaveChangesAsync();
        (await f.Service.GetTiposDisponiblesAsync(10)).Select(t => t.Codigo).Should().Equal("14");
        (await f.Service.GetTiposDisponiblesAsync(20)).Select(t => t.Codigo).Should().Equal("07");
    }

    [Fact]
    public async Task DirectCreateRejectsUnauthorizedTypeWithoutDocumentOrCorrelative()
    {
        await using var f = await Fixture.Create();
        var request = DteIdempotencyTests.Request(); request.TipoDteCodigo = "04";
        var result = await f.Service.CreateBorradorAsync(10, request, "test");
        result.ErrorCode.Should().Be("DTE_TIPO_NO_AUTORIZADO");
        (await f.Db.DteDocumentos.CountAsync()).Should().Be(0);
        (await f.Db.DteCorrelativos.CountAsync()).Should().Be(0);
        f.NoExternalCalls();
    }

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("11")]
    [InlineData("14")]
    public async Task EachExplicitlyAllowedTypePassesPersistedTenantAuthorization(string tipo)
    {
        await using var f = await Fixture.Create();
        var result = await DteTypeAuthorization.ValidateAsync(f.Db, 10, tipo, default);
        result.IsSuccess.Should().BeTrue(result.Error);
        (await f.Db.DteDocumentos.CountAsync()).Should().Be(0);
        f.NoExternalCalls();
    }

    [Theory]
    [InlineData("generar")]
    [InlineData("validar")]
    [InlineData("firmar")]
    [InlineData("enviar")]
    public async Task PreviouslyCreatedUnauthorizedDraftCannotAdvanceFiscalPipeline(string operation)
    {
        await using var f = await Fixture.Create();
        var doc = await f.AddDocument("04", "BORRADOR");
        var before = doc.Json!.JsonDte;
        Result result = operation switch
        {
            "generar" => await f.Service.GenerarAsync(10, doc.Id, "test"),
            "validar" => await f.Service.ValidarAsync(10, doc.Id, "test"),
            "firmar" => await f.Service.FirmarAsync(10, doc.Id, "test"),
            _ => await f.Service.EnviarAsync(10, doc.Id, "test")
        };
        result.ErrorCode.Should().Be("DTE_TIPO_NO_AUTORIZADO");
        var persisted = await f.Db.DteDocumentos.AsNoTracking().Include(d => d.Json).SingleAsync();
        persisted.EstadoCodigo.Should().Be("BORRADOR");
        persisted.Json!.JsonDte.Should().Be(before);
        f.NoExternalCalls();
    }

    [Fact]
    public async Task HistoricalReadAndCreationReplayRemainAvailableAfterRevocation()
    {
        await using var f = await Fixture.Create();
        var req = DteIdempotencyTests.Request(); req.TipoDteCodigo = "04";
        var doc = await f.AddDocument("04", "PROCESADO");
        doc.IdempotencyScope = "DTE";
        doc.IdempotencyKeyHash = DteIdempotency.HashKey(req.IdempotencyKey!);
        doc.IdempotencyRequestHash = DteIdempotency.Fingerprint(req);
        await f.Db.SaveChangesAsync();
        (await f.Service.GetByIdAsync(10, doc.Id)).IsSuccess.Should().BeTrue();
        (await f.Service.GetTiposConsultaAsync(10)).Should().Contain(t => t.Codigo == "04" && t.Nombre == "Histórico");
        (await f.Service.GetTiposDisponiblesAsync(10)).Should().NotContain(t => t.Codigo == "04");
        (await f.Service.GetTiposConsultaAsync(20)).Should().NotContain(t => t.Codigo == "04");
        (await f.Service.GetByIdAsync(20, doc.Id)).ErrorCode.Should().Be("DTE_NOT_FOUND");
        var replay = await f.Service.CreateBorradorAsync(10, req, "test");
        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value!.Id.Should().Be(doc.Id);
        replay.Value.IdempotencyReplayed.Should().BeTrue();
        (await f.Db.DteDocumentos.CountAsync()).Should().Be(1);
        f.NoExternalCalls();
    }

    [Fact]
    public async Task ApiPostAndTypesIgnoreForeignTenantQueryAndRejectDirectUnauthorizedRequest()
    {
        await using var f = await Fixture.Create();
        var user = Substitute.For<ICurrentUser>(); user.EmpresaId.Returns(10);
        var controller = new DteController(f.Service, new ConnectDteService(f.Service), user)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var request = DteIdempotencyTests.Request(); request.TipoDteCodigo = "04";
        var response = await controller.CrearGenerico(request, 20, default);
        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        var types = await controller.Tipos(20, default);
        types.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)types).Value.Should()
            .BeOfType<NeoSTP.Shared.ApiResponse<IReadOnlyList<TipoDteDisponibleDto>>>().Subject;
        payload.Data!.Select(t => t.Codigo).Should().Equal("01", "03", "11", "14");
        (await f.Db.DteDocumentos.CountAsync()).Should().Be(0);
        f.NoExternalCalls();
    }

    [Fact]
    public async Task OrdinaryConfigurationSaveCannotMassAssignOrClearAuthorization()
    {
        await using var f = await Fixture.Create();
        var request = JsonSerializer.Deserialize<SaveDteConfiguracionRequest>(
            """{"AmbienteCodigo":"PRUEBAS","TiposDteAutorizadosCsv":"01,03,04,05,06,07,08,09,11,14,15"}""")!;
        var service = new DteConfiguracionService(f.Db, DteFiscalIsolationTests.Protector(), f.Auth,
            Substitute.For<IAuditoriaService>());
        var result = await service.SaveAsync(10, request, "client");
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.TiposDteAutorizadosCsv.Should().Be("01,03,11,14");
        (await f.Db.DteConfiguracion.AsNoTracking().SingleAsync()).TiposDteAutorizadosCsv.Should().Be("01,03,11,14");
        f.NoExternalCalls();
    }

    [Fact]
    public async Task CertificationMatrixAndSummaryUseOnlyConfiguredTypes()
    {
        await using var f = await Fixture.Create();
        foreach (var tipo in new[] { "01", "03", "04", "11", "14", "15" })
            f.Db.CertificacionMatriz.Add(new() { TipoDteCodigo = tipo, Nombre = tipo,
                Descripcion = "Synthetic", Activo = true, EscenariosRequeridos = 1 });
        await f.Db.SaveChangesAsync();
        var service = new CertificacionDteService(f.Db, Substitute.For<IAuditoriaService>());
        var matrix = await service.GetMatrizAsync(10);
        matrix.Value!.Select(m => m.TipoDteCodigo).Should().BeEquivalentTo("01", "03", "11", "14");
        (await service.GetResumenAsync(10)).Value!.TotalTipos.Should().Be(4);
        (await service.GetEscenariosAsync("04", 10)).ErrorCode.Should().Be("DTE_TIPO_NO_AUTORIZADO");
        (await service.GetMatrizAsync(20)).Value!.Should().HaveCount(6);
    }

    [Fact]
    public async Task CatalogAndLookupFilterTenantItemsAndObserveRevocationWithoutChangingGlobalCatalog()
    {
        await using var f = await Fixture.Create();
        var catalog = new NeoSTP.Domain.Core.Catalogos.Catalogo { Codigo = "TIPO_FACTURA", Nombre = "Tipos", Activo = true };
        f.Db.Catalogos.Add(catalog);
        foreach (var tipo in new[] { "01", "03", "04", "11", "14" })
            catalog.Items.Add(new() { Codigo = "LOCAL_" + tipo, Valor = tipo, MetadataJson = $"{{\"codigoMH\":\"{tipo}\"}}" });
        catalog.Items.Add(new() { Codigo = "BAD", Valor = "Malformed", MetadataJson = "{bad" });
        await f.Db.SaveChangesAsync();
        var catalogs = new CatalogosService(f.Db, Substitute.For<IAuditoriaService>());
        var lookup = new LookupService(f.Db, catalogs);
        (await catalogs.GetItemsAsync("TIPO_FACTURA", 10)).Value!.Select(i => i.Valor).Should().BeEquivalentTo("01", "03", "11", "14");
        (await lookup.GetCatalogoAsync("TIPO_FACTURA", 10)).Should().HaveCount(4);
        f.Config.TiposDteAutorizadosCsv = "14"; await f.Db.SaveChangesAsync();
        (await lookup.GetCatalogoAsync("TIPO_FACTURA", 10)).Select(i => i.Value).Should().Equal("LOCAL_14");
        (await catalogs.GetItemsAsync("TIPO_FACTURA", null)).Value!.Should().HaveCount(6);
        (await f.Db.CatalogoItems.CountAsync()).Should().Be(6);
    }

    [Fact]
    public async Task WebCreationMenuUsesServiceAndDirectDisallowedFormsAreForbidden()
    {
        await using var f = await Fixture.Create();
        var user = Substitute.For<ICurrentUser>(); user.EmpresaId.Returns(10);
        user.HasPermiso(Arg.Any<string>()).Returns(true);
        var context = Substitute.For<NeoSTP.Application.Empresas.IEmpresaContext>(); context.CurrentEmpresaId.Returns(10);
        var controller = new NeoSTP.Web.Controllers.DteDocumentosController(f.Service,
            Substitute.For<NeoSTP.Application.Clientes.IClientesService>(),
            Substitute.For<NeoSTP.Application.Productos.IProductosService>(),
            new CatalogosService(f.Db, Substitute.For<IAuditoriaService>()),
            new CertificacionDteService(f.Db, Substitute.For<IAuditoriaService>()), user, context);
        (await controller.Create("04", null, default)).Should().BeOfType<ForbidResult>();
        (await controller.CrearRetencion(default)).Should().BeOfType<ForbidResult>();
        (await controller.Index(new(), default)).Should().BeOfType<ViewResult>();
        var types = (IReadOnlyList<TipoDteDisponibleDto>)controller.ViewBag.TiposDteDisponibles;
        types.Select(t => t.Codigo).Should().Equal("01", "03", "11", "14");
        f.NoExternalCalls();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal NeoStpDbContext Db { get; } = DteFiscalIsolationTests.Db();
        internal DteConfiguracion Config { get; } = new() { EmpresaId = 10, TiposDteAutorizadosCsv = "01,03,11,14" };
        internal IDteGeneratorService Generator { get; } = Substitute.For<IDteGeneratorService>();
        internal IDteSignerService Signer { get; } = Substitute.For<IDteSignerService>();
        internal IHaciendaAuthClient Auth { get; } = Substitute.For<IHaciendaAuthClient>();
        internal IHaciendaReceptionClient Reception { get; } = Substitute.For<IHaciendaReceptionClient>();
        internal DteDocumentosService Service { get; }
        private Fixture() => Service = new(Db, new DteCalculator(), Generator, Signer, Reception,
            Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(), Auth,
            DteFiscalIsolationTests.Protector(), Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
            Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>());
        internal static async Task<Fixture> Create()
        {
            var f = new Fixture();
            f.Db.Empresas.Add(new Empresa { Id = 10, Nit = "00000000000010", RazonSocial = "SYNTHETIC" });
            f.Db.DteConfiguracion.Add(f.Config);
            await f.Db.SaveChangesAsync();
            return f;
        }
        internal async Task<DteDocumento> AddDocument(string tipo, string estado)
        {
            var doc = DteFiscalIsolationTests.Documento(); doc.TipoDteCodigo = tipo; doc.EstadoCodigo = estado;
            doc.NumeroControl = $"DTE-{tipo}-M001P001-000000000000001";
            doc.Json = new() { JsonDte = DteFiscalIsolationTests.Payload(doc) };
            Db.DteDocumentos.Add(doc); await Db.SaveChangesAsync(); return doc;
        }
        internal void NoExternalCalls()
        {
            Generator.ReceivedCalls().Should().BeEmpty(); Signer.ReceivedCalls().Should().BeEmpty();
            Auth.ReceivedCalls().Should().BeEmpty(); Reception.ReceivedCalls().Should().BeEmpty();
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
