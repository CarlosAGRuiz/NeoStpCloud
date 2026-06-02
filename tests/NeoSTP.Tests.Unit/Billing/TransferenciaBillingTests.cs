using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>
/// Pagos LATAM PL.4 — flujo de transferencia bancaria offline: iniciar (pago
/// PENDIENTE_VERIFICACION), subir comprobante, confirmar (activa) y rechazar.
/// </summary>
public class TransferenciaBillingTests
{
    private const int EmpresaA = 10;

    private static (BillingService svc, NeoStpDbContext db, int planId) Build()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"transfer-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Database.EnsureCreated(); // aplica seed (planes 200-206)
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0614", RazonSocial = "Demo S.A.", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();

        var planId = db.Planes.AsNoTracking().OrderBy(p => p.Id).First().Id;

        var opts = Options.Create(new BillingOptions
        {
            Provider = "Mock",
            Transferencia = new TransferenciaOptions
            {
                Banco = "Banco Agrícola", TipoCuenta = "Corriente", NumeroCuenta = "1234-5678",
                Titular = "NeoSTP S.A. de C.V.", Instrucciones = "Transfiere y sube el comprobante.",
            },
        });

        var resolver = new PaymentProviderResolver(
            new IPaymentProvider[] { new TransferenciaPaymentProvider(), new MockPaymentProvider() }, opts);

        var email = Substitute.For<IEmailSender>();
        return (new BillingService(db, resolver, email, opts), db, planId);
    }

    [Fact]
    public async Task Iniciar_CreaPagoPendienteConInstrucciones()
    {
        var (svc, db, planId) = Build();
        var plan = await db.Planes.AsNoTracking().FirstAsync(p => p.Id == planId);

        var r = await svc.IniciarTransferenciaAsync(new IniciarTransferenciaRequest(EmpresaA, planId));

        r.IsSuccess.Should().BeTrue();
        r.Value!.Monto.Should().Be(plan.PrecioMensual);
        r.Value.Banco.Should().Be("Banco Agrícola");

        var pago = await db.BillingPayments.AsNoTracking().SingleAsync();
        pago.Status.Should().Be("PENDIENTE_VERIFICACION");
        pago.Metodo.Should().Be("TRANSFERENCIA");
    }

    [Fact]
    public async Task RegistrarComprobante_GuardaUrl()
    {
        var (svc, db, planId) = Build();
        var ini = await svc.IniciarTransferenciaAsync(new IniciarTransferenciaRequest(EmpresaA, planId));

        var r = await svc.RegistrarComprobanteAsync(EmpresaA, ini.Value!.PaymentId, "/uploads/comprobante.png");

        r.IsSuccess.Should().BeTrue();
        (await db.BillingPayments.AsNoTracking().SingleAsync()).ComprobanteUrl.Should().Be("/uploads/comprobante.png");
    }

    [Fact]
    public async Task Confirmar_ActivaSuscripcionYLicencia()
    {
        var (svc, db, planId) = Build();
        var ini = await svc.IniciarTransferenciaAsync(new IniciarTransferenciaRequest(EmpresaA, planId));

        var r = await svc.ConfirmarTransferenciaAsync(ini.Value!.PaymentId, "admin");

        r.IsSuccess.Should().BeTrue();
        var pago = await db.BillingPayments.AsNoTracking().SingleAsync();
        pago.Status.Should().Be("SUCCEEDED");
        pago.VerificadoPor.Should().Be("admin");

        var sub = await db.BillingSubscriptions.AsNoTracking().SingleAsync();
        sub.Status.Should().Be(SubscriptionStatus.Active);

        (await db.EmpresaPlanes.AsNoTracking().AnyAsync(ep => ep.EmpresaId == EmpresaA && ep.EstadoCodigo == "ACTIVO"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Confirmar_DosVeces_SegundaFallaEstadoInvalido()
    {
        var (svc, _, planId) = Build();
        var ini = await svc.IniciarTransferenciaAsync(new IniciarTransferenciaRequest(EmpresaA, planId));
        await svc.ConfirmarTransferenciaAsync(ini.Value!.PaymentId, "admin");

        var r = await svc.ConfirmarTransferenciaAsync(ini.Value!.PaymentId, "admin");
        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("ESTADO_INVALIDO");
    }

    [Fact]
    public async Task Rechazar_DejaPagoFallidoConMotivo()
    {
        var (svc, db, planId) = Build();
        var ini = await svc.IniciarTransferenciaAsync(new IniciarTransferenciaRequest(EmpresaA, planId));

        var r = await svc.RechazarTransferenciaAsync(ini.Value!.PaymentId, "Comprobante ilegible", "admin");

        r.IsSuccess.Should().BeTrue();
        var pago = await db.BillingPayments.AsNoTracking().SingleAsync();
        pago.Status.Should().Be("FAILED");
        pago.FailureReason.Should().Be("Comprobante ilegible");
    }

    [Fact]
    public async Task GetPendientes_ListaSoloPendientesConEmpresa()
    {
        var (svc, _, planId) = Build();
        var ini = await svc.IniciarTransferenciaAsync(new IniciarTransferenciaRequest(EmpresaA, planId));

        var pend = await svc.GetTransferenciasPendientesAsync(null);
        pend.Value!.Should().ContainSingle();
        pend.Value![0].EmpresaNombre.Should().Be("Demo S.A.");

        await svc.ConfirmarTransferenciaAsync(ini.Value!.PaymentId, "admin");
        (await svc.GetTransferenciasPendientesAsync(null)).Value!.Should().BeEmpty();
    }
}
