using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Dte.Contingencia;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class DteFiscalIsolationTests
{
    internal static NeoStpDbContext Db() => new(new DbContextOptionsBuilder<NeoStpDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    internal static DteDocumento Documento(string ambiente = DteAmbientes.Pruebas) => new()
    {
        EmpresaId = 10, AmbienteCodigo = ambiente, TipoDteCodigo = "01",
        NumeroControl = "DTE-01-M001P001-000000000000001",
        CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
        FechaEmision = new DateTime(2026, 9, 1), EstadoCodigo = DteEstadoCodigos.Enviado,
        ReceptorNombre = "SYNTHETIC", TotalGravada = 100, TotalPagar = 100,
    };

    internal static string Payload(DteDocumento doc) => JsonSerializer.Serialize(new
    {
        identificacion = new { version = doc.VersionDte, ambiente = DteAmbientes.CodigoMh(doc.AmbienteCodigo),
            tipoDte = doc.TipoDteCodigo, numeroControl = doc.NumeroControl, codigoGeneracion = doc.CodigoGeneracion }
    });

    internal static string Jws(string json) => "eyJhbGciOiJSUzUxMiJ9." +
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".c2ln";

    internal static ISecretProtector Protector()
    {
        var p = Substitute.For<ISecretProtector>();
        p.Protect(Arg.Any<string>()).Returns(x => (string)x[0]);
        p.Unprotect(Arg.Any<string>()).Returns(x => (string)x[0]);
        return p;
    }

    private static DteDocumentosService Service(NeoStpDbContext db, IDteSignerService signer, IHaciendaAuthClient auth) =>
        new(db, new DteCalculator(), Substitute.For<IDteGeneratorService>(), signer,
            Substitute.For<IHaciendaReceptionClient>(), Substitute.For<IHaciendaContingenciaClient>(),
            Substitute.For<IHaciendaEventoClient>(), auth, Protector(), Substitute.For<IDtePdfService>(),
            Substitute.For<ITenantEmailSender>(), Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>());

    private static async Task<DteConfiguracion> Seed(NeoStpDbContext db, DteDocumento doc, string ambiente)
    {
        db.Empresas.Add(new Empresa { Id = 10, Nit = "00000000000000", RazonSocial = "SYNTHETIC" });
        var config = new DteConfiguracion { EmpresaId = 10, AmbienteCodigo = ambiente,
            CertificadoBlob = [1], UsuarioMh = "fixture", PasswordMhCifrado = "fixture-password" };
        db.DteConfiguracion.Add(config);
        doc.Json = new DteDocumentoJson { JsonDte = Payload(doc), JsonFirmado = Jws(Payload(doc)) };
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();
        return config;
    }

    [Theory]
    [InlineData("generar")]
    [InlineData("validar")]
    [InlineData("firmar")]
    [InlineData("enviar")]
    [InlineData("retorno")]
    [InlineData("invalidacion")]
    [InlineData("contingencia")]
    public async Task CambioDeAmbiente_NoFirmaNiAutenticaNiModificaDocumento(string operation)
    {
        await using var db = Db();
        var doc = Documento();
        if (operation is "generar" or "validar" or "firmar") doc.EstadoCodigo = DteEstadoCodigos.Borrador;
        if (operation == "enviar") doc.EstadoCodigo = DteEstadoCodigos.Firmado;
        if (operation == "retorno") doc.EstadoCodigo = DteEstadoCodigos.Procesado;
        await Seed(db, doc, DteAmbientes.Produccion);
        var before = doc.EstadoCodigo;
        var json = doc.Json!.JsonDte;
        var signer = Substitute.For<IDteSignerService>();
        var auth = Substitute.For<IHaciendaAuthClient>();
        var svc = Service(db, signer, auth);
        Result result = operation switch
        {
            "generar" => await svc.GenerarAsync(10, doc.Id, "test"),
            "validar" => await svc.ValidarAsync(10, doc.Id, "test"),
            "firmar" => await svc.FirmarAsync(10, doc.Id, "test"),
            "enviar" => await svc.EnviarAsync(10, doc.Id, "test"),
            "retorno" => await svc.TransmitirEventoRetornoAsync(10, doc.Id, "test"),
            "invalidacion" => await svc.TransmitirInvalidacionEventoAsync(10, doc.Id, 2, "Test", null, "Test", "13", "test", "test"),
            _ => await svc.TransmitirEventoContingenciaAsync(10, [doc.Id], 1, null, "Test", "13", "test", "test"),
        };
        result.ErrorCode.Should().Be("DTE_AMBIENTE_INCOMPATIBLE");
        signer.ReceivedCalls().Should().BeEmpty();
        auth.ReceivedCalls().Should().BeEmpty();
        (await db.DteDocumentos.AsNoTracking().SingleAsync()).EstadoCodigo.Should().Be(before);
        doc.Json.JsonDte.Should().Be(json);
        (await db.DteEventos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Firmar_NoReinterpretaEstablecimientoHistorico()
    {
        await using var db = Db();
        var doc = Documento();
        doc.EstadoCodigo = DteEstadoCodigos.Generado;
        var config = await Seed(db, doc, DteAmbientes.Pruebas);
        config.CodigoEstablecimientoMh = "M002";
        await db.SaveChangesAsync();
        var signer = Substitute.For<IDteSignerService>();
        var result = await Service(db, signer, Substitute.For<IHaciendaAuthClient>()).FirmarAsync(10, doc.Id, "test");
        result.ErrorCode.Should().Be("DTE_ESTABLECIMIENTO_INCOMPATIBLE");
        signer.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("PROCESADO")]
    [InlineData("INVALIDADO")]
    [InlineData("ENVIADO")]
    public async Task Revalidar_NoReabreDocumentoFiscal(string estado)
    {
        await using var db = Db();
        var doc = Documento(); doc.EstadoCodigo = estado;
        await Seed(db, doc, DteAmbientes.Pruebas);
        var result = await Service(db, Substitute.For<IDteSignerService>(), Substitute.For<IHaciendaAuthClient>()).ValidarAsync(10, doc.Id, "test");
        result.ErrorCode.Should().Be("INVALID_STATE");
        doc.EstadoCodigo.Should().Be(estado);
    }

    [Theory]
    [InlineData("enviar")]
    [InlineData("consultar")]
    public async Task LoteDeOtroAmbiente_NoContactaHacienda(string operation)
    {
        await using var db = Db();
        var doc = Documento();
        await Seed(db, doc, DteAmbientes.Produccion);
        var evento = new DteEvento { EmpresaId = 10, AmbienteCodigo = DteAmbientes.Pruebas,
            TipoEventoCodigo = TipoEventoCodigos.Contingencia, EstadoCodigo = DteEventoEstadoCodigos.Procesado,
            SelloRecibido = "synthetic", CodigoGeneracion = Guid.NewGuid().ToString() };
        db.DteEventos.Add(evento); await db.SaveChangesAsync();
        db.DteEventoDocumentosRelacionados.Add(new() { EventoId = evento.Id, DocumentoId = doc.Id, RolCodigo = DteEventoRolCodigos.LoteContingencia });
        var lote = new DteContingenciaLote { EmpresaId = 10, EventoContingenciaId = evento.Id,
            AmbienteCodigo = DteAmbientes.Pruebas, CodigoLote = "fixture" };
        if (operation == "consultar") db.DteContingenciaLotes.Add(lote);
        await db.SaveChangesAsync();
        var send = Substitute.For<IHaciendaLoteClient>();
        var query = Substitute.For<IHaciendaConsultaLoteClient>();
        var auth = Substitute.For<IHaciendaAuthClient>();
        var service = new ContingenciaLoteService(db, send, query, auth, Protector(), NullLogger<ContingenciaLoteService>.Instance);
        Result result = operation == "enviar"
            ? await service.CrearYEnviarLoteAsync(evento.Id, 10, "test")
            : await service.ConsultarLoteAsync(lote.Id, 10);
        result.ErrorCode.Should().Be("DTE_AMBIENTE_INCOMPATIBLE");
        send.ReceivedCalls().Should().BeEmpty();
        query.ReceivedCalls().Should().BeEmpty();
        auth.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Credenciales_CambiarContextoExigePasswordExplicita()
    {
        await using var db = Db();
        var config = await Seed(db, Documento(), DteAmbientes.Pruebas);
        var service = new DteConfiguracionService(db, Protector(), Substitute.For<IHaciendaAuthClient>(), Substitute.For<IAuditoriaService>());
        var blocked = await service.SaveAsync(10, new() { AmbienteCodigo = DteAmbientes.Produccion, UsuarioMh = "fixture" }, "test");
        blocked.ErrorCode.Should().Be("DTE_CREDENCIALES_REQUERIDAS");
        config.AmbienteCodigo.Should().Be(DteAmbientes.Pruebas);
        var saved = await service.SaveAsync(10, new() { AmbienteCodigo = DteAmbientes.Produccion,
            UsuarioMh = "fixture", PasswordMh = "explicit-production-password" }, "test");
        saved.IsSuccess.Should().BeTrue();
        config.TokenMhCifrado.Should().BeNull();
        config.PasswordMhCifrado.Should().Be("explicit-production-password");
    }

    [Fact]
    public async Task TokenSinContexto_SeRenuevaYLaSiguienteLlamadaUsaCache()
    {
        await using var db = Db();
        var config = await Seed(db, Documento(), DteAmbientes.Produccion);
        config.TokenMhCifrado = "legacy-unbound-token";
        config.TokenMhExpiraAt = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();
        var auth = Substitute.For<IHaciendaAuthClient>();
        auth.AutenticarAsync("fixture", "fixture-password", DteAmbientes.Produccion, Arg.Any<CancellationToken>())
            .Returns(new HaciendaAuthResult { Success = true, Token = "Bearer new-token" });
        var first = await HaciendaTokenProvider.GetAsync(db, config, auth, Protector(), default);
        var second = await HaciendaTokenProvider.GetAsync(db, config, auth, Protector(), default);
        first.Token.Should().Be("new-token");
        second.Token.Should().Be("new-token");
        await auth.Received(1).AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("production")]
    public async Task Crear_AmbienteDesconocidoNoReservaNumero(string? ambiente)
    {
        await using var db = Db();
        if (ambiente is not null)
        {
            db.DteConfiguracion.Add(new() { EmpresaId = 10, AmbienteCodigo = ambiente });
        }
        db.Empresas.Add(new Empresa { Id = 10, Nit = "00000000000000", RazonSocial = "SYNTHETIC" });
        await db.SaveChangesAsync();
        var result = await Service(db, Substitute.For<IDteSignerService>(), Substitute.For<IHaciendaAuthClient>())
            .CreateBorradorAsync(10, new() { ReceptorManual = new() { Nombre = "SYNTHETIC" },
                Lineas = [new() { Codigo = "TEST", Descripcion = "SYNTHETIC", Cantidad = 1, PrecioUnitario = 100 }] }, "test");
        result.ErrorCode.Should().Be(ambiente is null ? "CONFIG_NOT_FOUND" : "DTE_AMBIENTE_INVALIDO");
        (await db.DteDocumentos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Firmar_PayloadDeOtroAmbienteSeBloquea()
    {
        await using var db = Db();
        var doc = Documento(); doc.EstadoCodigo = DteEstadoCodigos.Generado;
        await Seed(db, doc, DteAmbientes.Pruebas);
        doc.Json!.JsonDte = Payload(Documento(DteAmbientes.Produccion));
        await db.SaveChangesAsync();
        var signer = Substitute.For<IDteSignerService>();
        var result = await Service(db, signer, Substitute.For<IHaciendaAuthClient>()).FirmarAsync(10, doc.Id, "test");
        result.ErrorCode.Should().Be("DTE_PAYLOAD_INCOMPATIBLE");
        signer.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task LibrosYResumenFiscal_ExcluyenPruebasYOtrasEmpresas()
    {
        await using var db = Db();
        foreach (var type in new[] { "01", "03" })
        {
            foreach (var ambiente in new[] { DteAmbientes.Produccion, DteAmbientes.Pruebas })
            {
                var doc = Documento(ambiente); doc.TipoDteCodigo = type;
                doc.EstadoCodigo = DteEstadoCodigos.Procesado;
                doc.TotalGravada = type == "01" ? 113 : 100; doc.IvaTotal = 13;
                db.DteDocumentos.Add(doc);
            }
            var other = Documento(DteAmbientes.Produccion); other.EmpresaId = 99;
            other.EstadoCodigo = DteEstadoCodigos.Procesado; other.TipoDteCodigo = type;
            db.DteDocumentos.Add(other);
        }
        await db.SaveChangesAsync();
        var service = new ReporteFiscalService(db);
        var cf = await service.LibroVentasConsumidorAsync(10, 2026, 9);
        cf.Value!.Filas.Single().Documentos.Should().Be(1);
        var ccf = await service.LibroVentasContribuyentesAsync(10, 2026, 9);
        ccf.Value!.Filas.Should().ContainSingle();
        var f07 = await service.ResumenF07Async(10, 2026, 9);
        f07.Value!.VentasNetasGravadas.Should().Be(200);
        f07.Value.DebitoFiscal.Should().Be(26);
    }

    [Fact]
    public async Task TokenCache_AtadoAEmpresaAmbienteUsuarioYPassword()
    {
        await using var db = Db();
        var config = await Seed(db, Documento(), DteAmbientes.Pruebas);
        var protector = Protector();
        await HaciendaTokenCache.StoreAsync(db, config, protector, "Bearer fixture-token", DateTime.UtcNow.AddHours(1), default);
        HaciendaTokenCache.Read(config, protector).Should().Be("fixture-token");
        config.AmbienteCodigo = DteAmbientes.Produccion;
        HaciendaTokenCache.Read(config, protector).Should().BeNull();
        config.AmbienteCodigo = DteAmbientes.Pruebas;
        config.UsuarioMh = "another";
        HaciendaTokenCache.Read(config, protector).Should().BeNull();
        config.UsuarioMh = "fixture"; config.PasswordMhCifrado = "changed";
        HaciendaTokenCache.Read(config, protector).Should().BeNull();
        config.PasswordMhCifrado = "fixture-password"; config.EmpresaId = 99;
        HaciendaTokenCache.Read(config, protector).Should().BeNull();
    }

    [Theory]
    [InlineData("PRODUCCION", "00")]
    [InlineData("PRUEBAS", "01")]
    [InlineData("production", "00")]
    public async Task HttpRecepcion_NoCreaClienteSiAmbienteNoCoincide(string ambiente, string wire)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var client = new HttpHaciendaReceptionClient(factory, Options.Create(new HaciendaOptions()), NullLogger<HttpHaciendaReceptionClient>.Instance);
        var result = await client.EnviarAsync(new() { AmbienteCodigo = ambiente, Ambiente = wire, Documento = Jws(Payload(Documento())) });
        result.Success.Should().BeFalse();
        factory.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("PRUEBAS", "00", true)]
    [InlineData("PRODUCCION", "01", true)]
    [InlineData("PRUEBAS", "01", false)]
    [InlineData("PRODUCCION", "00", false)]
    [InlineData("otro", "00", false)]
    public void Jws_VerificaAmbienteDentroDelPayload(string ambiente, string wire, bool expected)
    {
        var json = JsonSerializer.Serialize(new { identificacion = new { ambiente = wire } });
        DteFiscalContext.CoincideJws(Jws(json), ambiente).Should().Be(expected);
    }

    [Fact]
    public void Jws_NoAceptaOtraIdentidadAunqueElAmbienteCoincida()
    {
        var doc = Documento();
        var payload = Jws(Payload(doc));
        doc.CodigoGeneracion = Guid.NewGuid().ToString();
        DteFiscalContext.CoincideJws(payload, doc.AmbienteCodigo, doc).Should().BeFalse();
    }

    [Fact]
    public async Task Contingencia_TipoEstablecimientoInvalidoNoFirmaNiAutentica()
    {
        await using var db = Db();
        var doc = Documento();
        var config = await Seed(db, doc, DteAmbientes.Pruebas);
        config.TipoEstablecimientoCodigo = "OFICINA";
        await db.SaveChangesAsync();

        var signer = Substitute.For<IDteSignerService>();
        var auth = Substitute.For<IHaciendaAuthClient>();
        var result = await Service(db, signer, auth).TransmitirEventoContingenciaAsync(
            10, [doc.Id], 1, null, "Test", "13", "00000000-0", "test");

        result.ErrorCode.Should().Be("DTE_TIPO_ESTABLECIMIENTO_INVALIDO");
        signer.ReceivedCalls().Should().BeEmpty();
        auth.ReceivedCalls().Should().BeEmpty();
        (await db.DteEventos.CountAsync()).Should().Be(0);
    }
}
