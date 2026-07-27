using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Conta;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Conta;

/// <summary>
/// E7: exportar los asientos en formato plano para cargarlos en un contable externo.
/// Una fila por movimiento — es lo que esos sistemas saben importar.
/// </summary>
public class AsientosCsvTests
{
    private const int Empresa = 1;

    private static NeoStpDbContext NewDb() => new(
        new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"asientos-{Guid.NewGuid()}").Options);

    private static ContabilidadService NewService(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static async Task SeedAsientoAsync(NeoStpDbContext db, DateOnly fecha, decimal monto)
    {
        var caja = new CuentaContable { EmpresaId = Empresa, Codigo = "1101", Nombre = "Efectivo", Tipo = "ACTIVO" };
        var ingreso = new CuentaContable { EmpresaId = Empresa, Codigo = "4101", Nombre = "Ventas", Tipo = "INGRESO" };
        db.CuentasContables.AddRange(caja, ingreso);
        await db.SaveChangesAsync();

        db.AsientosContables.Add(new AsientoContable
        {
            EmpresaId = Empresa, Numero = "AS-0001", Fecha = fecha,
            Concepto = "Venta del día", Origen = "DTE", EstadoCodigo = "ACTIVO",
            Lineas =
            [
                new AsientoContableLinea { CuentaContableId = caja.Id, Debe = monto, Haber = 0m, Detalle = "Cobro" },
                new AsientoContableLinea { CuentaContableId = ingreso.Id, Debe = 0m, Haber = monto, Detalle = "Ingreso" },
            ],
        });
        await db.SaveChangesAsync();
    }

    private static string Texto(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public async Task Exporta_UnaFilaPorMovimiento_ConTotalCuadrado()
    {
        await using var db = NewDb();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedAsientoAsync(db, hoy, 113.00m);

        var r = await NewService(db).AsientosCsvAsync(Empresa, hoy.Year, hoy.Month);

        r.IsSuccess.Should().BeTrue();
        var csv = Texto(r.Value!);
        csv.Should().Contain("AS-0001");
        csv.Should().Contain("1101").And.Contain("4101");
        csv.Should().Contain("Venta del día");
        // Debe y haber totalizan igual: el archivo cuadra.
        csv.Should().Contain("TOTAL");
        var lineas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lineas.Should().HaveCount(4); // encabezado + 2 movimientos + total
    }

    [Fact]
    public async Task NoIncluyeAsientosDeOtroPeriodo()
    {
        await using var db = NewDb();
        var mesPasado = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        await SeedAsientoAsync(db, mesPasado, 50m);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var actual = await NewService(db).AsientosCsvAsync(Empresa, hoy.Year, hoy.Month);
        var anterior = await NewService(db).AsientosCsvAsync(Empresa, mesPasado.Year, mesPasado.Month);

        Texto(actual.Value!).Should().NotContain("AS-0001");
        Texto(anterior.Value!).Should().Contain("AS-0001");
    }

    [Fact]
    public async Task PeriodoInvalido_Falla()
    {
        await using var db = NewDb();

        (await NewService(db).AsientosCsvAsync(Empresa, 2026, 13)).ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task SinAsientos_DevuelveSoloEncabezadoYTotal()
    {
        await using var db = NewDb();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var r = await NewService(db).AsientosCsvAsync(Empresa, hoy.Year, hoy.Month);

        r.IsSuccess.Should().BeTrue();
        Texto(r.Value!).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(2);
    }
}
