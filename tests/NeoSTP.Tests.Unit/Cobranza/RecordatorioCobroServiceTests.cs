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
        result.Value.Detalles.Should().ContainSingle(d => d.Motivo == "Ya enviado hoy.");
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
}
