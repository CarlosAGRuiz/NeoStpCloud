using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Cobranza;

/// <summary>
/// Cobranza/CxC: saldos derivados de DTE a crédito menos pagos confirmados, registro de pagos
/// y aislamiento por empresa.
/// </summary>
public class CobranzaServiceTests
{
    private const int EmpresaA = 30;
    private const int EmpresaB = 31;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cobranza-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static CobranzaService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static void AddDte(NeoStpDbContext db, int empresaId, int id, decimal total,
        string condicion = "2", string tipo = "01", string estado = "PROCESADO",
        int plazoDias = 30, DateTime? emision = null, string cliente = "Cliente X", int? clienteId = 1)
    {
        db.DteDocumentos.Add(new DteDocumento
        {
            Id = id, EmpresaId = empresaId, TipoDteCodigo = tipo, EstadoCodigo = estado,
            CondicionOperacionCodigo = condicion, PlazoDias = plazoDias,
            FechaEmision = emision ?? DateTime.UtcNow.Date, TotalPagar = total,
            ReceptorNombre = cliente, ClienteId = clienteId,
            NumeroControl = $"DTE-{tipo}-{id:D6}", CodigoGeneracion = Guid.NewGuid().ToString(),
        });
    }

    [Fact]
    public async Task Pendientes_SoloFacturaCreditoProcesadaConSaldo()
    {
        var db = NewDb();
        AddDte(db, EmpresaA, 1, total: 100m, condicion: "2");          // crédito → cobrable
        AddDte(db, EmpresaA, 2, total: 200m, condicion: "1");          // contado → excluido
        AddDte(db, EmpresaA, 3, total: 300m, condicion: "2", tipo: "05"); // NC → excluido
        AddDte(db, EmpresaA, 4, total: 400m, condicion: "2", estado: "BORRADOR"); // no procesado
        AddDte(db, EmpresaB, 5, total: 500m, condicion: "2");          // otra empresa
        await db.SaveChangesAsync();

        var r = await NewSvc(db).GetPendientesAsync(EmpresaA, new CobranzaQuery());

        r.IsSuccess.Should().BeTrue();
        r.Value!.Items.Should().ContainSingle();
        r.Value.Items[0].DteDocumentoId.Should().Be(1);
        r.Value.Items[0].Saldo.Should().Be(100m);
    }

    [Fact]
    public async Task RegistrarPago_ReduceSaldo_YValidaExceso()
    {
        var db = NewDb();
        AddDte(db, EmpresaA, 1, total: 100m, condicion: "2");
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var pago = await svc.RegistrarPagoAsync(EmpresaA, 1, new RegistrarPagoRequest { Monto = 40m }, "tester");
        pago.IsSuccess.Should().BeTrue();

        var pend = await svc.GetPendientesAsync(EmpresaA, new CobranzaQuery());
        pend.Value!.Items[0].Saldo.Should().Be(60m);
        pend.Value.Items[0].Pagado.Should().Be(40m);

        var exceso = await svc.RegistrarPagoAsync(EmpresaA, 1, new RegistrarPagoRequest { Monto = 999m }, "tester");
        exceso.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task RegistrarPago_QueSaldaFactura_LaSacaDePendientes()
    {
        var db = NewDb();
        AddDte(db, EmpresaA, 1, total: 100m, condicion: "2");
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        await svc.RegistrarPagoAsync(EmpresaA, 1, new RegistrarPagoRequest { Monto = 100m }, "tester");

        (await svc.GetPendientesAsync(EmpresaA, new CobranzaQuery())).Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PagoPendienteRevision_NoReduceSaldo()
    {
        var db = NewDb();
        AddDte(db, EmpresaA, 1, total: 100m, condicion: "2");
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        await svc.RegistrarPagoAsync(EmpresaA, 1, new RegistrarPagoRequest { Monto = 50m, PendienteRevision = true }, "tester");

        var pend = await svc.GetPendientesAsync(EmpresaA, new CobranzaQuery());
        pend.Value!.Items[0].Saldo.Should().Be(100m); // sigue sin confirmar
    }

    [Fact]
    public async Task RegistrarPago_SobreContado_InvalidState()
    {
        var db = NewDb();
        AddDte(db, EmpresaA, 1, total: 100m, condicion: "1"); // contado
        await db.SaveChangesAsync();

        var r = await NewSvc(db).RegistrarPagoAsync(EmpresaA, 1, new RegistrarPagoRequest { Monto = 10m }, "tester");

        r.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Resumen_TotalizaPendienteYVencido()
    {
        var db = NewDb();
        AddDte(db, EmpresaA, 1, total: 100m, condicion: "2", plazoDias: 30, emision: DateTime.UtcNow.Date); // vence en 30 → pendiente
        AddDte(db, EmpresaA, 2, total: 200m, condicion: "2", plazoDias: 0, emision: DateTime.UtcNow.Date.AddDays(-10)); // vencida
        await db.SaveChangesAsync();

        var resumen = await NewSvc(db).GetResumenAsync(EmpresaA);

        resumen.TotalPendiente.Should().Be(300m);
        resumen.FacturasPendientes.Should().Be(2);
        resumen.TotalVencido.Should().Be(200m);
        resumen.FacturasVencidas.Should().Be(1);
        resumen.ClientesConDeuda.Should().Be(1);
    }
}
