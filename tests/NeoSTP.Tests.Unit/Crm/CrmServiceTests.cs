using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Crm.Dtos;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Crm;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Crm;

public class CrmServiceTests
{
    private const int Empresa = 1140;
    private const int OtraEmpresa = 1141;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"crm-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.AddRange(
            new Empresa { Id = Empresa, Nit = "0614-CRM", RazonSocial = "CRM SA", EstadoCodigo = "ACTIVA" },
            new Empresa { Id = OtraEmpresa, Nit = "0614-OTRA", RazonSocial = "Otra SA", EstadoCodigo = "ACTIVA" });
        db.Clientes.AddRange(
            new Cliente { Id = 1, EmpresaId = Empresa, NumeroDocumento = "00000001-1", Nombre = "Cliente Uno", Correo = "cliente@neo.test" },
            new Cliente { Id = 2, EmpresaId = OtraEmpresa, NumeroDocumento = "00000002-2", Nombre = "Cliente Dos" });
        db.SaveChanges();
        return db;
    }

    private static CrmService NewSvc(NeoStpDbContext db, NeoSTP.Application.Connect.IConnectDteService? dte = null)
        => new(db, Substitute.For<IAuditoriaService>(), dte ?? Substitute.For<NeoSTP.Application.Connect.IConnectDteService>());

    [Fact]
    public async Task ListEtapas_CreaPipelineDefaultPorEmpresa()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var result = await svc.ListEtapasAsync(Empresa);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(6);
        result.Value!.First().Codigo.Should().Be("LEAD");
        result.Value.Last().Codigo.Should().Be("PERDIDA");
        (await db.EtapasPipelineCrm.CountAsync(x => x.EmpresaId == OtraEmpresa)).Should().Be(0);
    }

    [Fact]
    public async Task CrearContacto_ClienteDeOtraEmpresa_Falla()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var result = await svc.CrearContactoAsync(Empresa, new UpsertContactoCrmRequest
        {
            ClienteId = 2,
            Nombre = "Contacto externo",
        }, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CLIENTE_NOT_FOUND");
    }

    [Fact]
    public async Task CrearOportunidad_UsaEtapaDefaultEInfiereClienteDesdeContacto()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var contacto = await svc.CrearContactoAsync(Empresa, new UpsertContactoCrmRequest
        {
            ClienteId = 1,
            Nombre = "Ana Ventas",
            Email = "ana@cliente.test",
        }, "tester");

        var result = await svc.CrearOportunidadAsync(Empresa, new CrearOportunidadCrmRequest
        {
            ContactoCrmId = contacto.Value!.Id,
            Titulo = "Renovacion anual",
            MontoEstimado = 1200m,
        }, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.ClienteId.Should().Be(1);
        result.Value.EtapaCodigo.Should().Be("LEAD");
        result.Value.Probabilidad.Should().Be(10m);
        result.Value.EstadoCodigo.Should().Be(OportunidadCrmEstados.Abierta);
    }

    [Fact]
    public async Task CambiarEtapa_Ganada_CierraOportunidad()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var opp = await svc.CrearOportunidadAsync(Empresa, new CrearOportunidadCrmRequest
        {
            ClienteId = 1,
            Titulo = "Proyecto POS",
            MontoEstimado = 2500m,
        }, "tester");
        var etapas = await svc.ListEtapasAsync(Empresa);
        var ganada = etapas.Value!.Single(x => x.Codigo == "GANADA");

        var result = await svc.CambiarEtapaAsync(Empresa, opp.Value!.Id, new CambiarEtapaOportunidadRequest
        {
            EtapaPipelineCrmId = ganada.Id,
        }, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(OportunidadCrmEstados.Ganada);
        result.Value.FechaCierreReal.Should().NotBeNull();
        result.Value.Probabilidad.Should().Be(100m);
    }

    [Fact]
    public async Task Actividad_Completar_ActualizaResumen()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var opp = await svc.CrearOportunidadAsync(Empresa, new CrearOportunidadCrmRequest
        {
            ClienteId = 1,
            Titulo = "Seguimiento",
            MontoEstimado = 100m,
        }, "tester");
        var actividad = await svc.CrearActividadAsync(Empresa, new CrearActividadCrmRequest
        {
            OportunidadCrmId = opp.Value!.Id,
            Tipo = "LLAMADA",
            Asunto = "Llamar decisor",
            FechaProgramada = DateTime.UtcNow.AddDays(-1),
        }, "tester");

        var resumenAntes = await svc.ResumenAsync(Empresa);
        await svc.CompletarActividadAsync(Empresa, actividad.Value!.Id, new CompletarActividadCrmRequest { Resultado = "Interesado" }, "tester");
        var resumenDespues = await svc.ResumenAsync(Empresa);

        resumenAntes.Value!.ActividadesPendientes.Should().Be(1);
        resumenAntes.Value.ActividadesVencidas.Should().Be(1);
        resumenDespues.Value!.ActividadesPendientes.Should().Be(0);
        resumenDespues.Value.ActividadesVencidas.Should().Be(0);
    }

    [Fact]
    public async Task GetOportunidad_DeOtraEmpresa_NotFound()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var opp = await svc.CrearOportunidadAsync(Empresa, new CrearOportunidadCrmRequest
        {
            ClienteId = 1,
            Titulo = "Aislada",
            MontoEstimado = 100m,
        }, "tester");

        var result = await svc.GetOportunidadAsync(OtraEmpresa, opp.Value!.Id);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CRM_OPORTUNIDAD_NOT_FOUND");
    }

    // ── Cotizaciones ──────────────────────────────────────────────────────────

    private static CrearCotizacionCrmRequest Cotizacion(decimal precio = 113m, decimal cant = 1m) => new()
    {
        ClienteId = 1,
        Titulo = "Propuesta servicios",
        Lineas = [new CrearCotizacionCrmLineaRequest { Descripcion = "Servicio", Cantidad = cant, PrecioUnitario = precio }],
    };

    [Fact]
    public async Task CrearCotizacion_CalculaTotalesConIvaIncluido()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.CrearCotizacionAsync(Empresa, Cotizacion(precio: 113m), "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Numero.Should().Be("COT-000001");
        r.Value.Total.Should().Be(113m);
        r.Value.IvaTotal.Should().Be(13m);    // contenido: 113 * 13/113
        r.Value.SubTotal.Should().Be(100m);   // total − IVA
        r.Value.EstadoCodigo.Should().Be("BORRADOR");
        r.Value.Lineas.Should().ContainSingle();
    }

    [Fact]
    public async Task CambiarEstadoCotizacion_FlujoEnviadaAceptada()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var cot = await svc.CrearCotizacionAsync(Empresa, Cotizacion(), "t");

        var enviada = await svc.CambiarEstadoCotizacionAsync(Empresa, cot.Value!.Id, new CambiarEstadoCotizacionRequest { EstadoCodigo = "ENVIADA" }, "t");
        var aceptada = await svc.CambiarEstadoCotizacionAsync(Empresa, cot.Value.Id, new CambiarEstadoCotizacionRequest { EstadoCodigo = "ACEPTADA" }, "t");

        enviada.Value!.EstadoCodigo.Should().Be("ENVIADA");
        aceptada.Value!.EstadoCodigo.Should().Be("ACEPTADA");
    }

    [Fact]
    public async Task ConvertirCotizacion_Exito_EnlazaDteYMarcaConvertida()
    {
        var db = NewDb();
        var dte = Substitute.For<NeoSTP.Application.Connect.IConnectDteService>();
        dte.EmitirAsync(Empresa, Arg.Any<NeoSTP.Application.Dte.Dtos.CreateDteDocumentoRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<NeoSTP.Application.Dte.Dtos.DteDocumentoDto>.Ok(new NeoSTP.Application.Dte.Dtos.DteDocumentoDto { Id = 888, EstadoCodigo = "PROCESADO" }));
        var svc = NewSvc(db, dte);
        var cot = await svc.CrearCotizacionAsync(Empresa, Cotizacion(), "t");

        var r = await svc.ConvertirCotizacionADteAsync(Empresa, cot.Value!.Id, new ConvertirCotizacionRequest { TipoDteCodigo = "01" }, "t");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EstadoCodigo.Should().Be("CONVERTIDA");
        r.Value.DteDocumentoId.Should().Be(888);
        await dte.Received(1).EmitirAsync(Empresa,
            Arg.Is<NeoSTP.Application.Dte.Dtos.CreateDteDocumentoRequest>(q => q.TipoDteCodigo == "01" && q.Lineas.Count == 1),
            "t", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConvertirCotizacion_FallaEmision_NoMarcaConvertida()
    {
        var db = NewDb();
        var dte = Substitute.For<NeoSTP.Application.Connect.IConnectDteService>();
        dte.EmitirAsync(Arg.Any<int>(), Arg.Any<NeoSTP.Application.Dte.Dtos.CreateDteDocumentoRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<NeoSTP.Application.Dte.Dtos.DteDocumentoDto>.Fail("Certificado no cargado.", "VALIDATION"));
        var svc = NewSvc(db, dte);
        var cot = await svc.CrearCotizacionAsync(Empresa, Cotizacion(), "t");

        var r = await svc.ConvertirCotizacionADteAsync(Empresa, cot.Value!.Id, new ConvertirCotizacionRequest(), "t");

        r.IsFailure.Should().BeTrue();
        (await db.CotizacionesCrm.FirstAsync()).EstadoCodigo.Should().Be("BORRADOR");
    }

    [Fact]
    public async Task ConvertirCotizacion_YaConvertida_Falla()
    {
        var db = NewDb();
        var dte = Substitute.For<NeoSTP.Application.Connect.IConnectDteService>();
        dte.EmitirAsync(Arg.Any<int>(), Arg.Any<NeoSTP.Application.Dte.Dtos.CreateDteDocumentoRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<NeoSTP.Application.Dte.Dtos.DteDocumentoDto>.Ok(new NeoSTP.Application.Dte.Dtos.DteDocumentoDto { Id = 1, EstadoCodigo = "PROCESADO" }));
        var svc = NewSvc(db, dte);
        var cot = await svc.CrearCotizacionAsync(Empresa, Cotizacion(), "t");
        await svc.ConvertirCotizacionADteAsync(Empresa, cot.Value!.Id, new ConvertirCotizacionRequest(), "t");

        var r = await svc.ConvertirCotizacionADteAsync(Empresa, cot.Value.Id, new ConvertirCotizacionRequest(), "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }
}
