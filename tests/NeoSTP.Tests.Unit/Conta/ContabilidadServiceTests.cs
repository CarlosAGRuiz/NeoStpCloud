using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Profit;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Conta;

/// <summary>NEOCONTA — asientos automáticos (doble partida, idempotencia), reversa y balanza.</summary>
public class ContabilidadServiceTests
{
    private const int Empresa = 95;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"conta-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Contable SA", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static ContabilidadService NewSvc(NeoStpDbContext db) => new(db, Substitute.For<IAuditoriaService>());

    private static void SeedVentaCcf(NeoStpDbContext db, int id = 1)
    {
        // CCF: neto 1000 + IVA 130 → total operación 1130.
        db.DteDocumentos.Add(new DteDocumento
        {
            Id = id, EmpresaId = Empresa, TipoDteCodigo = "03",
            NumeroControl = $"DTE-03-M001P001-{id:000000000000000}",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = DteEstadoCodigos.Procesado,
            FechaEmision = new DateTime(2026, 6, 10),
            MontoTotalOperacion = 1130m, IvaTotal = 130m, TotalPagar = 1130m,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task ListCuentas_SiembraCatalogoMinimoPorEmpresa()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r = await svc.ListCuentasAsync(Empresa);

        r.Value!.Should().HaveCount(8);
        r.Value.Select(c => c.Codigo).Should().Contain(["1101", "1102", "2102", "4101"]);
    }

    [Fact]
    public async Task GenerarAsientos_VentaCcf_DoblePartidaCuadrada()
    {
        var db = NewDb(); var svc = NewSvc(db);
        SeedVentaCcf(db);

        var r = await svc.GenerarAsientosPeriodoAsync(Empresa, 2026, 6, "conta");

        r.Value.Should().Be(1);
        var asiento = await db.AsientosContables.Include(a => a.Lineas).FirstAsync();
        asiento.TotalDebe.Should().Be(1130m);
        asiento.TotalHaber.Should().Be(1130m); // doble partida
        asiento.Lineas.Should().HaveCount(3);  // CxC / Ventas / IVA débito
    }

    [Fact]
    public async Task GenerarAsientos_EsIdempotente()
    {
        var db = NewDb(); var svc = NewSvc(db);
        SeedVentaCcf(db);
        await svc.GenerarAsientosPeriodoAsync(Empresa, 2026, 6, "conta");

        var segunda = await svc.GenerarAsientosPeriodoAsync(Empresa, 2026, 6, "conta");

        segunda.Value.Should().Be(0); // no duplica
        (await db.AsientosContables.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GenerarAsientos_CobroYGasto()
    {
        var db = NewDb(); var svc = NewSvc(db);
        db.PagosCliente.Add(new PagoCliente { Id = 1, EmpresaId = Empresa, DteDocumentoId = 99, Fecha = new DateOnly(2026, 6, 12), Monto = 500m, EstadoCodigo = "CONFIRMADO" });
        db.ProfitGastos.Add(new ProfitGasto { Id = 1, EmpresaId = Empresa, Fecha = new DateOnly(2026, 6, 13), Categoria = "SERVICIOS", Descripcion = "Internet", Monto = 100m, IvaMonto = 13m, IvaDeducible = true, EstadoCodigo = "ACTIVO" });
        db.ProfitGastos.Add(new ProfitGasto { Id = 2, EmpresaId = Empresa, Fecha = new DateOnly(2026, 6, 14), Categoria = "COMPRA", Descripcion = "No debe asentarse (ya está en facturas)", Monto = 50m, EstadoCodigo = "ACTIVO" });
        db.SaveChanges();

        var r = await svc.GenerarAsientosPeriodoAsync(Empresa, 2026, 6, "conta");

        r.Value.Should().Be(2); // cobro + gasto SERVICIOS (la categoría COMPRA se excluye)
        var asientos = await db.AsientosContables.Include(a => a.Lineas).ToListAsync();
        asientos.Should().OnlyContain(a => a.TotalDebe == a.TotalHaber);
    }

    [Fact]
    public async Task Reversar_CreaEspejo_YMarcaOriginal()
    {
        var db = NewDb(); var svc = NewSvc(db);
        SeedVentaCcf(db);
        await svc.GenerarAsientosPeriodoAsync(Empresa, 2026, 6, "conta");
        var original = await db.AsientosContables.FirstAsync();

        var r = await svc.ReversarAsientoAsync(Empresa, original.Id, "venta anulada", "conta");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Origen.Should().Be("REVERSA");
        r.Value.ReversaDeId.Should().Be(original.Id);
        r.Value.TotalDebe.Should().Be(original.TotalHaber);
        (await db.AsientosContables.FirstAsync(a => a.Id == original.Id)).EstadoCodigo.Should().Be("REVERSADO");

        // Reversar dos veces falla.
        var otra = await svc.ReversarAsientoAsync(Empresa, original.Id, null, "conta");
        otra.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Balanza_CuadraYNeteaReversas()
    {
        var db = NewDb(); var svc = NewSvc(db);
        SeedVentaCcf(db);
        await svc.GenerarAsientosPeriodoAsync(Empresa, 2026, 6, "conta");
        var original = await db.AsientosContables.FirstAsync();
        await svc.ReversarAsientoAsync(Empresa, original.Id, null, "conta");

        var balanza = await svc.BalanzaAsync(Empresa, 2026, 6);

        balanza.Value!.Cuadrada.Should().BeTrue();
        balanza.Value.TotalDebe.Should().Be(balanza.Value.TotalHaber);
        // Original + reversa se netean: saldos en cero.
        balanza.Value.Cuentas.Should().OnlyContain(c => c.SaldoDeudor == 0 && c.SaldoAcreedor == 0);
    }
}
