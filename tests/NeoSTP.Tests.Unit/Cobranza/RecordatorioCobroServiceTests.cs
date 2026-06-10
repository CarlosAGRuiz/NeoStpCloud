using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Cobranza;

public class RecordatorioCobroServiceTests
{
    private const int Empresa = 81;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"recordatorios-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "0614", RazonSocial = "Neo Test", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static RecordatorioCobroService NewSvc(
        NeoStpDbContext db,
        ITenantEmailSender? email = null,
        IWhatsAppSender? whatsApp = null)
    {
        var cobranza = new CobranzaService(db, Substitute.For<IAuditoriaService>());
        return new RecordatorioCobroService(
            db,
            cobranza,
            email ?? Substitute.For<ITenantEmailSender>(),
            whatsApp ?? Substitute.For<IWhatsAppSender>(),
            Substitute.For<IAuditoriaService>());
    }

    private static void AddFacturaVencida(
        NeoStpDbContext db,
        int id = 1,
        string? email = "cliente@demo.local",
        string? telefono = "50370000000")
    {
        db.DteDocumentos.Add(new DteDocumento
        {
            Id = id,
            EmpresaId = Empresa,
            TipoDteCodigo = TipoDteCodigos.FacturaConsumidorFinal,
            EstadoCodigo = DteEstadoCodigos.Procesado,
            CondicionOperacionCodigo = "2",
            PlazoDias = 5,
            FechaEmision = DateTime.UtcNow.Date.AddDays(-10),
            TotalPagar = 100m,
            ReceptorNombre = "Cliente demo",
            ReceptorCorreo = email,
            ReceptorTelefono = telefono,
            NumeroControl = $"DTE-01-{id:D6}",
            CodigoGeneracion = Guid.NewGuid().ToString(),
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task EjecutarAsync_EnviaEmailYRegistraLog()
    {
        var db = NewDb();
        AddFacturaVencida(db);
        var email = Substitute.For<ITenantEmailSender>();
        email.EnviarAsync(Empresa, Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendResult { Success = true, MessageId = "mail-1" });

        var result = await NewSvc(db, email).EjecutarAsync(Empresa, new EjecutarRecordatoriosCobroRequest(), "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EnviadosEmail.Should().Be(1);
        db.RecordatoriosCobro.Should().ContainSingle(r =>
            r.Canal == RecordatorioCanales.Email &&
            r.EstadoCodigo == RecordatorioEstados.Enviado &&
            r.MessageId == "mail-1");
        await email.Received(1).EnviarAsync(
            Empresa,
            Arg.Is<EmailMessage>(m => m.To == "cliente@demo.local" && m.Subject.Contains("DTE-01-000001")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EjecutarAsync_NoDuplicaEmailEnElMismoDia()
    {
        var db = NewDb();
        AddFacturaVencida(db);
        db.RecordatoriosCobro.Add(new RecordatorioCobro
        {
            EmpresaId = Empresa,
            DteDocumentoId = 1,
            FechaRecordatorio = DateOnly.FromDateTime(DateTime.UtcNow),
            Canal = RecordatorioCanales.Email,
            Destinatario = "cliente@demo.local",
            EstadoCodigo = RecordatorioEstados.Enviado,
            Saldo = 100m,
            DiasVencido = 5,
        });
        db.SaveChanges();
        var email = Substitute.For<ITenantEmailSender>();

        var result = await NewSvc(db, email).EjecutarAsync(Empresa, new EjecutarRecordatoriosCobroRequest(), "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Omitidos.Should().Be(1);
        result.Value.Detalles.Should().ContainSingle(d => d.Motivo == "Ya enviado dentro de la frecuencia configurada.");
        await email.DidNotReceive().EnviarAsync(Arg.Any<int>(), Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EjecutarAsync_OmiteSiNoHayDestinatario()
    {
        var db = NewDb();
        AddFacturaVencida(db, email: null, telefono: null);
        var email = Substitute.For<ITenantEmailSender>();

        var result = await NewSvc(db, email).EjecutarAsync(Empresa, new EjecutarRecordatoriosCobroRequest(), "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Omitidos.Should().Be(1);
        db.RecordatoriosCobro.Should().ContainSingle(r =>
            r.Canal == RecordatorioCanales.Email &&
            r.EstadoCodigo == RecordatorioEstados.Omitido &&
            r.Motivo == "Sin destinatario.");
        await email.DidNotReceive().EnviarAsync(Arg.Any<int>(), Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    // ── Configuración por empresa (V2-D3) ────────────────────────────────────

    [Fact]
    public async Task Configuracion_UpsertYLectura()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var guardada = await svc.GuardarConfiguracionAsync(Empresa, new NeoSTP.Application.Cobranza.GuardarConfigRecordatorioRequest
        {
            Activo = true, DiasVencidoMinimo = 5, FrecuenciaDias = 7, MaximoPorEjecucion = 25,
            EnviarEmail = true, AsuntoPlantilla = "Pago pendiente {numeroControl}",
        }, "admin");
        var leida = await svc.GetConfiguracionAsync(Empresa);

        guardada.IsSuccess.Should().BeTrue();
        leida.Value!.Activo.Should().BeTrue();
        leida.Value.FrecuenciaDias.Should().Be(7);
        leida.Value.AsuntoPlantilla.Should().Be("Pago pendiente {numeroControl}");
    }

    [Fact]
    public async Task Configuracion_ActivaSinCanales_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r = await svc.GuardarConfiguracionAsync(Empresa, new NeoSTP.Application.Cobranza.GuardarConfigRecordatorioRequest
        { Activo = true, EnviarEmail = false, EnviarWhatsApp = false }, "admin");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task EjecutarSegunConfiguracion_SinConfigActiva_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r = await svc.EjecutarSegunConfiguracionAsync(Empresa, "worker");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("RECORDATORIOS_DESHABILITADOS");
    }

    [Fact]
    public async Task EjecutarSegunConfiguracion_UsaPlantillaEnAsunto()
    {
        var db = NewDb();
        var email = Substitute.For<ITenantEmailSender>();
        email.EnviarAsync(Arg.Any<int>(), Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendResult { Success = true, MessageId = "ok" });
        var svc = NewSvc(db, email);
        AddFacturaVencida(db);
        await svc.GuardarConfiguracionAsync(Empresa, new NeoSTP.Application.Cobranza.GuardarConfigRecordatorioRequest
        { Activo = true, EnviarEmail = true, DiasVencidoMinimo = 0, AsuntoPlantilla = "Pago pendiente {numeroControl}" }, "admin");

        var r = await svc.EjecutarSegunConfiguracionAsync(Empresa, "worker");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EnviadosEmail.Should().Be(1);
        await email.Received(1).EnviarAsync(Empresa,
            Arg.Is<EmailMessage>(m => m.Subject.StartsWith("Pago pendiente DTE-01-")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AplicarPlantilla_SustituyePlaceholders()
    {
        var f = new CobroPendienteDto
        {
            NumeroControl = "DTE-01-000001", ClienteNombre = "ACME", Saldo = 150.5m,
            DiasVencido = 4, Vencimiento = new DateOnly(2026, 6, 1),
        };

        var texto = RecordatorioCobroService.AplicarPlantilla(
            "Hola {cliente}: {numeroControl} debe ${saldo} ({diasVencido} días, venció {vencimiento}).", f);

        texto.Should().Be("Hola ACME: DTE-01-000001 debe $150.50 (4 días, venció 01/06/2026).");
    }
}
