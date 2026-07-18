using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Clientes.Dtos;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Clientes;

/// <summary>Mejora 1 — clientes extranjeros: documento opcional, país de residencia y precarga FEX.</summary>
public class ClienteExtranjeroTests
{
    private const int Empresa = 90;
    private const string Espania = "9314";

    // ---------- Validador ----------

    [Fact]
    public void Extranjero_SinDocumento_NoExigeDocumento()
    {
        var req = new CreateClienteRequest
        {
            TipoDocumentoCodigo = "OTRO",
            NumeroDocumento = null,
            Nombre = "Cliente Madrid",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
            PaisCodigo = Espania,
        };

        ClienteValidator.Validate(req).Should().BeEmpty();
    }

    [Fact]
    public void Local_SinDocumento_SigueSiendoObligatorio()
    {
        var req = new CreateClienteRequest
        {
            TipoDocumentoCodigo = "DUI",
            NumeroDocumento = null,
            Nombre = "Cliente local",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
            PaisCodigo = ClienteValidator.PaisElSalvador,
        };

        ClienteValidator.Validate(req).Should().Contain(e => e.Contains("obligatorio"));
    }

    [Fact]
    public void Extranjero_Contribuyente_EsInvalido()
    {
        var req = new CreateClienteRequest
        {
            TipoDocumentoCodigo = "OTRO",
            NumeroDocumento = "X-123",
            Nombre = "Empresa extranjera",
            TipoContribuyenteCodigo = "CONTRIBUYENTE",
            Nrc = "123456-7",
            CodigoActividad = "62010",
            PaisCodigo = Espania,
        };

        ClienteValidator.Validate(req).Should().Contain(e => e.Contains("extranjero"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(0, false)]
    public void TipoPersona_SoloAcepta1o2(int tipoPersona, bool valido)
    {
        var req = new CreateClienteRequest
        {
            TipoDocumentoCodigo = "DUI",
            NumeroDocumento = "12345678-9",
            Nombre = "X",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
            TipoPersona = tipoPersona,
        };

        var tieneError = ClienteValidator.Validate(req).Any(e => e.Contains("Tipo de persona"));
        tieneError.Should().Be(!valido);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("9300", false)]
    [InlineData("9314", true)]
    public void EsExtranjero_SegunPais(string? pais, bool esperado)
        => ClienteValidator.EsExtranjero(pais).Should().Be(esperado);

    // ---------- ClientesService ----------

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"ext-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "E", RazonSocial = "E", EstadoCodigo = "ACTIVA" });
        var cat = new Catalogo { Id = 500, Codigo = "PAIS", Nombre = "País", EsSistema = true, Activo = true };
        db.Catalogos.Add(cat);
        db.CatalogoItems.Add(new CatalogoItem
        {
            Id = 5001, CatalogoId = 500, Codigo = Espania, Valor = "España", Activo = true,
            MetadataJson = "{\"codigoMH\": \"9314\", \"nombreMH\": \"ESPAÑA\"}",
        });
        db.SaveChanges();
        return db;
    }

    private static ClientesService NewClientesSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    [Fact]
    public async Task Create_ExtranjeroSinDocumento_PersisteConPais()
    {
        var db = NewDb();
        var svc = NewClientesSvc(db);

        var r = await svc.CreateAsync(Empresa, new CreateClienteRequest
        {
            TipoDocumentoCodigo = "OTRO",
            NumeroDocumento = null,
            Nombre = "Cliente Madrid",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
            PaisCodigo = Espania,
            TipoPersona = 1,
        }, "tester");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.EsExtranjero.Should().BeTrue();
        r.Value.NumeroDocumento.Should().BeNull();
        var persistido = await db.Clientes.AsNoTracking().FirstAsync();
        persistido.PaisCodigo.Should().Be(Espania);
        persistido.TipoPersona.Should().Be(1);
    }

    [Fact]
    public async Task Create_PaisInexistente_Validation()
    {
        var svc = NewClientesSvc(NewDb());

        var r = await svc.CreateAsync(Empresa, new CreateClienteRequest
        {
            TipoDocumentoCodigo = "OTRO",
            NumeroDocumento = "P-1",
            Nombre = "X",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
            PaisCodigo = "9999",
        }, "tester");

        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Create_DosExtranjerosSinDocumento_NoChocanComoDuplicados()
    {
        var db = NewDb();
        var svc = NewClientesSvc(db);

        CreateClienteRequest Req(string nombre) => new()
        {
            TipoDocumentoCodigo = "OTRO",
            NumeroDocumento = null,
            Nombre = nombre,
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
            PaisCodigo = Espania,
        };

        (await svc.CreateAsync(Empresa, Req("Uno"), "t")).IsSuccess.Should().BeTrue();
        (await svc.CreateAsync(Empresa, Req("Dos"), "t")).IsSuccess.Should().BeTrue();
        (await db.Clientes.CountAsync()).Should().Be(2);
    }

    // ---------- Precarga FEX desde cliente ----------

    private static DteDocumentosService NewDteSvc(NeoStpDbContext db) => new(
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

    [Fact]
    public async Task Fex_ConClienteExtranjero_PrecargaPaisYTipoPersona()
    {
        var db = NewDb();
        var svc = NewDteSvc(db);
        var cliente = new Cliente
        {
            EmpresaId = Empresa, TipoDocumentoCodigo = "OTRO", Nombre = "Cliente Madrid",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", PaisCodigo = Espania, EstadoCodigo = "ACTIVO",
        };
        var doc = new DteDocumento { EmpresaId = Empresa, TipoDteCodigo = TipoDteCodigos.FacturaExportacion };
        var request = new CreateDteDocumentoRequest { TipoDteCodigo = TipoDteCodigos.FacturaExportacion };

        var r = await svc.AplicarDatosExportacionAsync(doc, request, cliente, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error);
        doc.ReceptorPaisCodigo.Should().Be("9314");
        doc.ReceptorPaisNombre.Should().Be("ESPAÑA");
        doc.ReceptorTipoPersona.Should().Be(1); // consumidor final sin TipoPersona → natural
    }

    [Fact]
    public async Task Fex_RequestExplicito_GanaSobreCliente()
    {
        var db = NewDb();
        var svc = NewDteSvc(db);
        var cliente = new Cliente
        {
            EmpresaId = Empresa, TipoDocumentoCodigo = "OTRO", Nombre = "X",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", PaisCodigo = Espania,
            TipoPersona = 2, EstadoCodigo = "ACTIVO",
        };
        var doc = new DteDocumento { EmpresaId = Empresa, TipoDteCodigo = TipoDteCodigos.FacturaExportacion };
        var request = new CreateDteDocumentoRequest
        {
            TipoDteCodigo = TipoDteCodigos.FacturaExportacion,
            ReceptorPaisCodigo = "9539",
            ReceptorPaisNombre = "Estados Unidos",
            ReceptorTipoPersona = 1,
        };

        var r = await svc.AplicarDatosExportacionAsync(doc, request, cliente, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error);
        doc.ReceptorPaisCodigo.Should().Be("9539");
        doc.ReceptorPaisNombre.Should().Be("ESTADOS UNIDOS");
        doc.ReceptorTipoPersona.Should().Be(1);
    }

    [Fact]
    public async Task Fex_ClienteSinPais_Validation()
    {
        var db = NewDb();
        var svc = NewDteSvc(db);
        var cliente = new Cliente
        {
            EmpresaId = Empresa, TipoDocumentoCodigo = "DUI", NumeroDocumento = "1-1", Nombre = "Local",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "ACTIVO",
        };
        var doc = new DteDocumento { EmpresaId = Empresa, TipoDteCodigo = TipoDteCodigos.FacturaExportacion };
        var request = new CreateDteDocumentoRequest { TipoDteCodigo = TipoDteCodigos.FacturaExportacion };

        var r = await svc.AplicarDatosExportacionAsync(doc, request, cliente, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Fex_ClienteConPaisInactivo_Validation()
    {
        var db = NewDb();
        var item = await db.CatalogoItems.FirstAsync();
        item.Activo = false;
        await db.SaveChangesAsync();

        var svc = NewDteSvc(db);
        var cliente = new Cliente
        {
            EmpresaId = Empresa, TipoDocumentoCodigo = "OTRO", Nombre = "X",
            TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", PaisCodigo = Espania, EstadoCodigo = "ACTIVO",
        };
        var doc = new DteDocumento { EmpresaId = Empresa, TipoDteCodigo = TipoDteCodigos.FacturaExportacion };
        var request = new CreateDteDocumentoRequest { TipoDteCodigo = TipoDteCodigos.FacturaExportacion };

        var r = await svc.AplicarDatosExportacionAsync(doc, request, cliente, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Should().Contain("PAIS");
    }
}
