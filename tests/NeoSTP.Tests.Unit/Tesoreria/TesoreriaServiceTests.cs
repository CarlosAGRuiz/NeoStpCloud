using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Tesoreria.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Tesoreria;

/// <summary>NEOTESORERIA — TesoreriaService: cuentas, movimientos y recálculo de saldo.</summary>
public class TesoreriaServiceTests
{
    private const int Empresa = 77;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"tes-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "X", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static TesoreriaService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static async Task<int> NuevaCuenta(TesoreriaService svc, decimal saldoInicial = 100m, string tipo = "CAJA")
    {
        var r = await svc.CrearCuentaAsync(Empresa, new CreateCuentaTesoreriaRequest
        {
            Codigo = $"C{Guid.NewGuid():N}".Substring(0, 8), Nombre = "Cuenta", TipoCuenta = tipo, SaldoInicial = saldoInicial,
        }, "tester");
        r.IsSuccess.Should().BeTrue();
        return r.Value!.Id;
    }

    [Fact]
    public async Task CrearCuenta_InicializaSaldoActual()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.CrearCuentaAsync(Empresa, new CreateCuentaTesoreriaRequest
        { Codigo = "CAJA", Nombre = "Caja general", TipoCuenta = "CAJA", SaldoInicial = 250m }, "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.SaldoActual.Should().Be(250m);
        r.Value.EstadoCodigo.Should().Be("ACTIVA");
    }

    [Fact]
    public async Task CrearCuenta_CodigoDuplicado_Falla()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CrearCuentaAsync(Empresa, new CreateCuentaTesoreriaRequest { Codigo = "CAJA", Nombre = "A", TipoCuenta = "CAJA" }, "t");

        var r = await svc.CrearCuentaAsync(Empresa, new CreateCuentaTesoreriaRequest { Codigo = "CAJA", Nombre = "B", TipoCuenta = "CAJA" }, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("DUPLICATE");
    }

    [Fact]
    public async Task RegistrarEgreso_RestaDelSaldo_YGuardaSnapshot()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var cid = await NuevaCuenta(svc, saldoInicial: 500m);

        var r = await svc.RegistrarMovimientoAsync(Empresa, new RegistrarMovimientoRequest
        { CuentaId = cid, Tipo = "EGRESO", Monto = 120.50m, Concepto = "Pago planilla", Origen = "PLANILLA", OrigenId = 9 }, "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.SaldoResultante.Should().Be(379.50m);
        r.Value.Origen.Should().Be("PLANILLA");
        var cuenta = await db.CuentasTesoreria.FirstAsync(c => c.Id == cid);
        cuenta.SaldoActual.Should().Be(379.50m);
    }

    [Fact]
    public async Task RegistrarIngreso_SumaAlSaldo()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var cid = await NuevaCuenta(svc, saldoInicial: 100m);

        await svc.RegistrarMovimientoAsync(Empresa, new RegistrarMovimientoRequest
        { CuentaId = cid, Tipo = "INGRESO", Monto = 75m, Concepto = "Cobro factura" }, "t");

        (await db.CuentasTesoreria.FirstAsync(c => c.Id == cid)).SaldoActual.Should().Be(175m);
    }

    [Fact]
    public async Task AnularMovimiento_RevierteSaldo()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var cid = await NuevaCuenta(svc, saldoInicial: 500m);
        var mov = await svc.RegistrarMovimientoAsync(Empresa, new RegistrarMovimientoRequest
        { CuentaId = cid, Tipo = "EGRESO", Monto = 200m, Concepto = "x" }, "t");

        var r = await svc.AnularMovimientoAsync(Empresa, mov.Value!.Id, "t");

        r.IsSuccess.Should().BeTrue();
        (await db.CuentasTesoreria.FirstAsync(c => c.Id == cid)).SaldoActual.Should().Be(500m);
        (await db.MovimientosTesoreria.FirstAsync(m => m.Id == mov.Value.Id)).EstadoCodigo.Should().Be("ANULADO");
    }

    [Fact]
    public async Task RegistrarMovimiento_CuentaInactiva_Falla()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var cid = await NuevaCuenta(svc);
        await svc.InactivarCuentaAsync(Empresa, cid, "t");

        var r = await svc.RegistrarMovimientoAsync(Empresa, new RegistrarMovimientoRequest
        { CuentaId = cid, Tipo = "EGRESO", Monto = 10m, Concepto = "x" }, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Resumen_AgrupaPorTipoYSumaSaldos()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await NuevaCuenta(svc, saldoInicial: 300m, tipo: "BANCO");
        await NuevaCuenta(svc, saldoInicial: 50m, tipo: "CAJA");

        var r = await svc.ResumenAsync(Empresa);

        r.IsSuccess.Should().BeTrue();
        r.Value!.SaldoTotal.Should().Be(350m);
        r.Value.SaldoBancos.Should().Be(300m);
        r.Value.SaldoCaja.Should().Be(50m);
        r.Value.CuentasActivas.Should().Be(2);
    }

    [Fact]
    public async Task AnularMovimiento_DosVeces_Falla()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var cid = await NuevaCuenta(svc, saldoInicial: 500m);
        var mov = await svc.RegistrarMovimientoAsync(Empresa, new RegistrarMovimientoRequest
        { CuentaId = cid, Tipo = "EGRESO", Monto = 100m, Concepto = "x" }, "t");
        await svc.AnularMovimientoAsync(Empresa, mov.Value!.Id, "t");

        var r = await svc.AnularMovimientoAsync(Empresa, mov.Value.Id, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }
}
