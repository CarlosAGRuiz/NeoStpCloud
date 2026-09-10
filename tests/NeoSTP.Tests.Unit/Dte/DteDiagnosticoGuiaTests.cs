using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Diagnostico;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class DteDiagnosticoGuiaTests
{
    private const string Territorio = """
        {"estado":"ERROR","codigoMsg":"096","descripcionMsg":"DOCUMENTO NO CUMPLE CON NORMATIVA DE CUMPLIMIENTO",
        "observaciones":["Campo #/emisor/direccion/municipio no cumple el formato requerido",
        "Campo #/emisor/direccion/departamento no cumple el formato requerido"]}
        """;
    private const string Actividad = """
        {"estado":"ERROR","codigoMsg":"008","descripcionMsg":"[emisor.codActividad] NO CORRESPONDE A CONTRIBUYENTE"}
        """;

    [Fact]
    public void Territorio_IdentificaAmbosCamposDeLaEmpresaSinConfundirConPermisos()
    {
        var guia = DteDiagnosticoGuia.Crear("ERROR", null, DateTime.UtcNow, Territorio);
        guia.CodigoHacienda.Should().Be("096");
        guia.Campos.Select(x => x.Campo).Should().BeEquivalentTo("emisor.direccion.municipio", "emisor.direccion.departamento");
        guia.Campos.Should().OnlyContain(x => x.Seccion == "EMPRESA" && x.AccionSugerida.Contains("catálogos"));
        guia.SiguientePaso.Should().Be("CORREGIR_DATOS");
        guia.RequiereConsultaHacienda.Should().BeFalse();
        guia.ReintentoAutomatico.Should().BeFalse();
    }

    [Theory]
    [InlineData("emisor", "EMPRESA")]
    [InlineData("receptor", "RECEPTOR_DTE")]
    public void Actividad_NoInventaCodigoYSeparaEmpresaDeCliente(string sujeto, string seccion)
    {
        var guia = DteDiagnosticoGuia.Crear("ERROR", null, DateTime.UtcNow, Actividad.Replace("emisor", sujeto));
        guia.Campos.Should().ContainSingle().Which.Seccion.Should().Be(seccion);
        guia.Campos[0].AccionSugerida.Should().Contain("registrada").And.Contain("no garantiza");
        guia.AccionSugerida.Should().Contain("mismo ID");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("<html>502</html>")]
    [InlineData("{\"estado\":12,\"codigoMsg\":{},\"observaciones\":[{},null,1]}")]
    [InlineData("{}")]
    [InlineData(null)]
    public void RespuestaAusenteOMalformada_NoPermiteSuponerRechazo(string? raw)
    {
        var guia = DteDiagnosticoGuia.Crear("ERROR", null, DateTime.UtcNow, raw);
        guia.RequiereConsultaHacienda.Should().BeTrue();
        guia.Codigo.Should().Be("DTE_RESULTADO_INCIERTO");
    }

    [Theory]
    [InlineData("ENVIADO", null, "SOLO_NO", true)]
    [InlineData("CONTINGENCIA", null, "SOLO_NO", true)]
    [InlineData("FIRMADO", null, "SOLO_NO", true)]
    [InlineData("PROCESADO", null, "SOLO_NO", true)]
    [InlineData("PROCESADO", "synthetic", "SOLO_CONSULTA", false)]
    [InlineData("INVALIDADO", "synthetic", "SOLO_CONSULTA", false)]
    public void EstadosTerminalesOInciertos_NoSugierenOtraEmision(string estado, string? sello, string paso, bool consulta)
    {
        var guia = DteDiagnosticoGuia.Crear(estado, sello, null, Actividad);
        guia.RequiereConsultaHacienda.Should().Be(consulta);
        guia.SiguientePaso.Should().Be(consulta ? "CONCILIAR_HACIENDA" : paso);
    }

    [Theory]
    [InlineData("BORRADOR")]
    [InlineData("GENERADO")]
    [InlineData("VALIDADO")]
    public void Pendientes_NoDicenProcesado(string estado)
    {
        var guia = DteDiagnosticoGuia.Crear(estado, null, null, null);
        guia.SiguientePaso.Should().Be("CONTINUAR_DOCUMENTO");
        guia.Codigo.Should().Be("DTE_PENDIENTE");
    }

    [Fact]
    public void Duplicado_PrecedeInclusoALaCorreccionDeCampos()
    {
        var guia = DteDiagnosticoGuia.Crear("RECHAZADO", null, DateTime.UtcNow,
            """{"estado":"RECHAZADO","codigoMsg":"999","descripcionMsg":"[emisor.nit] DOCUMENTO DUPLICADO"}""");
        guia.RequiereConsultaHacienda.Should().BeTrue();
    }

    [Fact]
    public void CodigoDesconocido_NoInventaLaCausa()
    {
        var guia = DteDiagnosticoGuia.Crear("RECHAZADO", null, DateTime.UtcNow,
            """{"estado":"RECHAZADO","codigoMsg":"XYZ","descripcionMsg":"Nuevo mensaje técnico"}""");
        guia.SiguientePaso.Should().Be("REVISAR_DIAGNOSTICO");
        guia.MensajeTecnico.Should().Be("Nuevo mensaje técnico");
        guia.Campos.Should().BeEmpty();
    }

    [Theory]
    [InlineData("HACIENDA_AUTH_FAILED")]
    [InlineData("FIRMA_FAILED")]
    [InlineData("DTE_AMBIENTE_INCOMPATIBLE")]
    public void ErrorLocal_IndicaConfiguracionSinCambiarIdentidad(string code)
    {
        var guia = DteDiagnosticoGuia.Crear("BORRADOR", null, null, null, code, "test");
        guia.SiguientePaso.Should().Be("REVISAR_CONFIGURACION");
        guia.ReintentoAutomatico.Should().BeFalse();
    }

    [Fact]
    public void ValidacionLocal_ExponeCampoSinRespuestaMh()
    {
        var guia = DteDiagnosticoGuia.Crear("BORRADOR", null, null, null, "VALIDATION", "Emisor incompleto",
            ["[emisor.correo] Falta correo", "[emisor.telefono] Falta teléfono"]);
        guia.Campos.Should().HaveCount(2);
        guia.CodigoHacienda.Should().BeNull();
        guia.SiguientePaso.Should().Be("CORREGIR_DATOS");
    }

    [Fact]
    public async Task Diagnostico_LeeRespuestaDirectaSinSincronizarYAislaEmpresa()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        doc.EstadoCodigo = "ERROR"; doc.EnviadoAt = DateTime.UtcNow; doc.Json!.RespuestaHacienda = Territorio;
        db.DteErrorCatalogo.Add(new() { Codigo = "096", Tipo = "HACIENDA", MensajeTecnico = "Autorización",
            Descripcion = "La cuenta no está autorizada", CausaProbable = "Faltan permisos", AccionSugerida = "Solicitar habilitación" });
        db.DteErrorOcurrencias.Add(new() { EmpresaId = 10, DteDocumentoId = doc.Id, CodigoError = "096",
            Mensaje = "Error", RespuestaMhJson = Territorio, Fuente = "EVENTO", OcurrioAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new DiagnosticoHaciendaService(db);
        var dto = await service.ObtenerDiagnosticoDocumentoAsync(10, doc.Id);
        dto!.Diagnostico!.Campos.Should().HaveCount(2);
        dto.Errores[0].Descripcion.Should().Contain("municipio");
        (await service.ObtenerDiagnosticoDocumentoAsync(20, doc.Id)).Should().BeNull();
        (await service.ListarCatalogoAsync()).Single().Descripcion.Should().NotContain("no está autorizada");
        (await db.DteErrorOcurrencias.SingleAsync()).Resuelta.Should().BeFalse();
        doc.Json.RespuestaHacienda.Should().Be(Territorio);
    }

    [Theory]
    [InlineData(400, Territorio, "HACIENDA_DATOS_INVALIDOS", "ERROR")]
    [InlineData(200, Actividad, "HACIENDA_DATOS_INVALIDOS", "ERROR")]
    [InlineData(401, "{\"descripcionMsg\":\"Token inválido\"}", "HACIENDA_AUTH_FAILED", "ERROR")]
    [InlineData(500, Territorio, "DTE_RESULTADO_INCIERTO", "ENVIADO")]
    [InlineData(200, "<html>error</html>", "DTE_RESULTADO_INCIERTO", "ENVIADO")]
    [InlineData(200, "{\"estado\":\"PROCESADO\"}", "DTE_RESULTADO_INCIERTO", "ENVIADO")]
    [InlineData(200, "{\"estado\":\"PROCESADO\",\"selloRecibido\":123}", "DTE_RESULTADO_INCIERTO", "ENVIADO")]
    public async Task Enviar_ErrorDevuelveDocumentoYGuiaPersistida(int http, string raw, string codigo, string estado)
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        using var handler = new FixedResponse(http, raw);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, false));
        var reception = new HttpHaciendaReceptionClient(factory, Options.Create(new HaciendaOptions
            { PruebasBaseUrl = "https://synthetic.example.test" }), NullLogger<HttpHaciendaReceptionClient>.Instance);
        var service = Service(db, reception);
        var result = await service.EnviarAsync(10, doc.Id, "test");
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(codigo);
        result.Value!.Id.Should().Be(doc.Id);
        result.Value.EstadoCodigo.Should().Be(estado);
        result.Value.Diagnostico!.RequiereConsultaHacienda.Should().Be(codigo == "DTE_RESULTADO_INCIERTO");
        var reloaded = await service.GetByIdAsync(10, doc.Id);
        reloaded.Value!.Diagnostico.Should().BeEquivalentTo(result.Value.Diagnostico);
        (await db.DteDocumentos.CountAsync()).Should().Be(1);
        (await db.DteErrorOcurrencias.CountAsync(o => o.EmpresaId == 10 && o.DteDocumentoId == doc.Id)).Should().Be(1);
        handler.Calls.Should().Be(1);
        if (estado == "ENVIADO")
        {
            (await service.EnviarAsync(10, doc.Id, "test")).ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
            handler.Calls.Should().Be(1);
        }
        if (http >= 400)
        {
            using var evidence = JsonDocument.Parse(reloaded.Value.RespuestaHacienda!);
            evidence.RootElement.GetProperty("respuestaOriginal").GetString().Should().Be(raw);
        }
    }

    [Fact]
    public async Task Enviar_ProcesadoConSelloEsExito()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        var reception = Substitute.For<IHaciendaReceptionClient>();
        reception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaReceptionResult { Success = true, CodigoHttp = 200, Estado = "PROCESADO", SelloRecibido = "synthetic" });
        var result = await Service(db, reception).EnviarAsync(10, doc.Id, "test");
        result.IsSuccess.Should().BeTrue();
        result.Value!.Diagnostico!.Codigo.Should().Be("DTE_PROCESADO");
        result.Value.EstadoCodigo.Should().Be("PROCESADO");
    }

    [Fact]
    public async Task RespuestaPerdida_MantieneMarcaDurableYNoReenvia()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        var reception = Substitute.For<IHaciendaReceptionClient>();
        reception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<HaciendaReceptionResult>(new OperationCanceledException("synthetic")));
        var service = Service(db, reception);
        await service.Invoking(s => s.EnviarAsync(10, doc.Id, "test")).Should().ThrowAsync<OperationCanceledException>();
        db.ChangeTracker.Clear();
        (await service.GetByIdAsync(10, doc.Id)).Value!.EstadoCodigo.Should().Be("ENVIADO");
        (await service.EnviarAsync(10, doc.Id, "test")).ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        await reception.Received(1).EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("TIMEOUT")]
    [InlineData("NETWORK_ERROR")]
    public async Task TransporteSinRaw_NoSePierdeDiagnosticoNiSeEncolaComoContingencia(string clasificacion)
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        var reception = Substitute.For<IHaciendaReceptionClient>();
        reception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>()).Returns(new HaciendaReceptionResult
            { CodigoHttp = 0, Estado = "CONTINGENCIA", ClasificaMsg = clasificacion, DescripcionMsg = "synthetic" });
        var result = await Service(db, reception).EnviarAsync(10, doc.Id, "test");
        result.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        result.Value!.EstadoCodigo.Should().Be("ENVIADO");
        result.Value.RespuestaHacienda.Should().Contain(clasificacion);
    }

    [Fact]
    public async Task LegacyIncierto_RegenerarNoDestruyeEvidencia()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        doc.EstadoCodigo = "ERROR"; doc.EnviadoAt = DateTime.UtcNow; doc.Json!.RespuestaHacienda = "legacy unknown";
        await db.SaveChangesAsync();
        var result = await Service(db, Substitute.For<IHaciendaReceptionClient>()).GenerarAsync(10, doc.Id, "test");
        result.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        doc.Json.RespuestaHacienda.Should().Be("legacy unknown");
    }

    [Fact]
    public async Task RechazoConfirmado_RegeneraValidaFirmaYEnviaMismoDteConHistorial()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        doc.Detalles.Add(new() { NumeroLinea = 1, Cantidad = 1, PrecioUnitario = 100, Codigo = "TEST", Descripcion = "SYNTHETIC" });
        await db.SaveChangesAsync();
        var originalCode = doc.CodigoGeneracion;
        var originalNumber = doc.NumeroControl;
        var reception = Substitute.For<IHaciendaReceptionClient>();
        reception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>()).Returns(
            new HaciendaReceptionResult { CodigoHttp = 200, Estado = "ERROR", CodigoMsg = "008", Raw = Actividad },
            new HaciendaReceptionResult { CodigoHttp = 200, Estado = "PROCESADO", Success = true, SelloRecibido = "synthetic" });
        var service = Service(db, reception);
        (await service.EnviarAsync(10, doc.Id, "test")).IsFailure.Should().BeTrue();
        (await service.GenerarAsync(10, doc.Id, "test")).IsSuccess.Should().BeTrue();
        (await service.ValidarAsync(10, doc.Id, "test")).IsSuccess.Should().BeTrue();
        (await service.FirmarAsync(10, doc.Id, "test")).IsSuccess.Should().BeTrue();
        var result = await service.EnviarAsync(10, doc.Id, "test");
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(doc.Id);
        result.Value.CodigoGeneracion.Should().Be(originalCode);
        result.Value.NumeroControl.Should().Be(originalNumber);
        (await db.DteDocumentos.CountAsync()).Should().Be(1);
        (await db.DteErrorOcurrencias.SingleAsync()).RespuestaMhJson.Should().Be(Actividad);
        await reception.Received(2).EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>());
    }

    private static async Task<DteDocumento> Seed(NeoStpDbContext db)
    {
        db.Empresas.Add(new Empresa { Id = 10, Nit = "00000000000000", RazonSocial = "SYNTHETIC", Correo = "test@example.test", Telefono = "00000000" });
        db.DteConfiguracion.Add(new() { EmpresaId = 10, AmbienteCodigo = "PRUEBAS", UsuarioMh = "fixture", PasswordMhCifrado = "fixture", CertificadoBlob = [1] });
        var doc = DteFiscalIsolationTests.Documento();
        doc.EstadoCodigo = DteEstadoCodigos.Firmado; // Initial transmission, not a legacy uncertain ENVIADO.
        doc.Json = new() { JsonDte = DteFiscalIsolationTests.Payload(doc), JsonFirmado = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(doc)) };
        db.DteDocumentos.Add(doc); await db.SaveChangesAsync(); return doc;
    }

    private static DteDocumentosService Service(NeoStpDbContext db, IHaciendaReceptionClient reception)
    {
        var auth = Substitute.For<IHaciendaAuthClient>();
        auth.AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaAuthResult { Success = true, Token = "synthetic", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        var generator = Substitute.For<IDteGeneratorService>();
        generator.Generar(Arg.Any<DteDocumento>(), Arg.Any<DteConfiguracion?>())
            .Returns(call => Result<string>.Ok(DteFiscalIsolationTests.Payload(call.Arg<DteDocumento>())));
        var signer = Substitute.For<IDteSignerService>();
        signer.FirmarAsync(Arg.Any<string>(), Arg.Any<byte[]?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => new DteSignResult { Success = true, JsonFirmado = DteFiscalIsolationTests.Jws(call.ArgAt<string>(0)) });
        return new(db, new DteCalculator(), generator, signer, reception,
            Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(), auth,
            DteFiscalIsolationTests.Protector(), Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
            Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>());
    }

    private sealed class FixedResponse(int status, string raw) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(raw) });
        }
    }
}
