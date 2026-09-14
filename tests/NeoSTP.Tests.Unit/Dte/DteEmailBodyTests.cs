using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// Verifica el cuerpo HTML del correo de envío de DTE (cuadro de datos + total).
/// También deja una muestra en tmp/email-demo.html para revisión visual.
/// </summary>
public class DteEmailBodyTests
{

    [Theory]
    [InlineData("BORRADOR", "SEAL", 10)]
    [InlineData("ERROR", "SEAL", 10)]
    [InlineData("PROCESADO", null, 10)]
    [InlineData("PROCESADO", "SEAL", 99)]
    public async Task AutomaticEmailDoesNotSendUnprocessedUnsealedOrOtherTenant(string state, string? seal, int tenant)
    {
        await using var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var doc = Sample(); doc.Id=20; doc.EmpresaId=10; doc.Empresa.Id=10;
        doc.Empresa.Nit="00000000000000"; doc.EstadoCodigo=state; doc.SelloRecibido=seal;
        db.DteDocumentos.Add(doc); await db.SaveChangesAsync();
        var email=Substitute.For<ITenantEmailSender>();
        var service=new DteDocumentosService(db,new DteCalculator(),Substitute.For<IDteGeneratorService>(),
            Substitute.For<IDteSignerService>(),Substitute.For<IHaciendaReceptionClient>(),
            Substitute.For<IHaciendaContingenciaClient>(),Substitute.For<IHaciendaEventoClient>(),
            Substitute.For<IHaciendaAuthClient>(),DteFiscalIsolationTests.Protector(),Substitute.For<IDtePdfService>(),email,
            Substitute.For<IAuditoriaService>(),Substitute.For<IConnectWebhookDispatcher>());
        await service.EnviarCorreoAutomaticoAsync(tenant,20,"test");
        email.ReceivedCalls().Should().BeEmpty();
        (await db.NotificationOutbox.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AutomaticEmailQueuesIndependentReceiverAndIssuerMessagesWithoutSendingInline()
    {
        await using var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var doc = Sample();
        doc.Id = 20; doc.EmpresaId = 10; doc.Empresa.Id = 10;
        doc.Empresa.Nit = "00000000000000";
        doc.Empresa.Correo = "issuer@example.invalid";
        doc.ReceptorCorreo = "receiver@example.invalid";
        db.DteDocumentos.Add(doc); await db.SaveChangesAsync();

        var email = Substitute.For<ITenantEmailSender>();
        var service = new DteDocumentosService(db, new DteCalculator(), Substitute.For<IDteGeneratorService>(),
            Substitute.For<IDteSignerService>(), Substitute.For<IHaciendaReceptionClient>(),
            Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(),
            Substitute.For<IHaciendaAuthClient>(), DteFiscalIsolationTests.Protector(), Substitute.For<IDtePdfService>(), email,
            Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>());
        await service.EnviarCorreoAutomaticoAsync(10, 20, "test");

        var rows = await db.NotificationOutbox
            .Where(x => x.Tipo == NotificationOutboxTipos.DteCorreo)
            .ToListAsync();

        rows.Should().HaveCount(2);
        rows.Select(x => x.Finalidad).Should().BeEquivalentTo(DteCorreoFinalidades.Receptor, DteCorreoFinalidades.Emisor);
        rows.Select(x => x.Destinatario).Should().BeEquivalentTo("receiver@example.invalid", "issuer@example.invalid");
        email.ReceivedCalls().Should().BeEmpty();
    }

    private static DteDocumento Sample()
    {
        var d = new DteDocumento
        {
            TipoDteCodigo = TipoDteCodigos.ComprobanteCreditoFiscal,
            NumeroControl = "DTE-03-00010001-000000000000123",
            CodigoGeneracion = "B5F1C2A3-9D4E-4F6A-8B7C-1234567890AB",
            SelloRecibido = "2025ABCD1234EF5678901234567890ABCDEF1234",
            FechaEmision = new DateTime(2026, 6, 3), HoraEmision = new TimeSpan(10, 35, 0),
            EstadoCodigo = DteEstadoCodigos.Procesado,
            ReceptorNombre = "Comercial Los Andes, S.A. de C.V.",
            TotalPagar = 1130.01m,
            Empresa = new Empresa { Id = 1, RazonSocial = "Distribuidora El Salvador, S.A. de C.V." },
        };
        return d;
    }

    [Fact]
    public void BuildBody_ContieneCuadroDeDatosYTotal()
    {
        var html = DteDocumentosService.BuildBody(Sample(), "Distribuidora El Salvador, S.A. de C.V.");

        html.Should().Contain("DATOS DEL DOCUMENTO");
        html.Should().Contain("DTE-03-00010001-000000000000123");
        html.Should().Contain("B5F1C2A3-9D4E-4F6A-8B7C-1234567890AB");
        html.Should().Contain("Comercial Los Andes");
        html.Should().Contain("TOTAL A PAGAR");
        html.Should().Contain("1,130.01");
        html.Should().Contain("PROCESADO");

        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tmp");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "email-demo.html"), html);

            // Variante con logo: reemplaza cid:logo por un data URI para previsualizar en navegador.
            var conLogo = DteDocumentosService.BuildBody(Sample(), "Distribuidora El Salvador, S.A. de C.V.", incluirLogo: true);
            var logoPng = SkiaDemo.Imagen(220, 90, "DISAL", color: "#FFFFFF");
            conLogo = conLogo.Replace("cid:logo", "data:image/png;base64," + Convert.ToBase64String(logoPng));
            File.WriteAllText(Path.Combine(dir, "email-demo-logo.html"), conLogo);
        }
        catch { /* muestra best-effort */ }
    }

    [Fact]
    public void BuildBody_SinSello_OmiteLaFila()
    {
        var d = Sample();
        d.SelloRecibido = null;

        var html = DteDocumentosService.BuildBody(d, "Emisor");

        html.Should().NotContain("Sello de recepción");
    }

    [Fact]
    public async Task Reenvio_OmiteLogoLegacyInvalidoSinFallarElCorreo()
    {
        await using var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase("dte-email-branding-" + Guid.NewGuid()).Options);
        var empresa = new Empresa { Id = 10, Nit = "00000000000000", RazonSocial = "Empresa sintética",
            LogoBlob = "not-an-image"u8.ToArray(), LogoContentType = "image/png" };
        var dte = Sample();
        dte.Id = 20; dte.EmpresaId = empresa.Id; dte.Empresa = empresa; dte.ReceptorCorreo = "cliente@example.invalid";
        dte.Json = new DteDocumentoJson { JsonDte = "{\"synthetic\":true}" };
        db.Empresas.Add(empresa); db.DteDocumentos.Add(dte); await db.SaveChangesAsync();
        EmailMessage? captured = null;
        var email = Substitute.For<ITenantEmailSender>();
        email.EnviarAsync(10, Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured = call.ArgAt<EmailMessage>(1);
            return new EmailSendResult { Success = true, Mensaje = "synthetic" };
        });
        var pdf = Substitute.For<IDtePdfService>(); pdf.Generar(Arg.Any<DteDocumento>()).Returns("%PDF-synthetic"u8.ToArray());
        var service = new DteDocumentosService(db, new DteCalculator(), Substitute.For<IDteGeneratorService>(),
            Substitute.For<IDteSignerService>(), Substitute.For<IHaciendaReceptionClient>(),
            Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(),
            Substitute.For<IHaciendaAuthClient>(), DteFiscalIsolationTests.Protector(), pdf, email,
            Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>());

        var result = await service.ReenviarPorCorreoAsync(10, dte.Id, null, "audit");

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.InlineImages.Should().BeEmpty();
        captured.HtmlBody.Should().NotContain("cid:logo");
        captured.Attachments.Should().HaveCount(2);
        captured.Cc.Should().BeNull();
        captured.Bcc.Should().BeNull();
    }
}
