using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Scan;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Integration;

/// <summary>
/// M5.1 — Integración NeoScan ↔ NeoProfit ↔ DTE recibidos: varios servicios reales
/// sobre un mismo DbContext, verificando el flujo de confirmación de un escaneo.
/// </summary>
public class ScanProfitIntegrationTests
{
    private const int Empresa = 100;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"scan-profit-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "X", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static ScanService NewScan(NeoStpDbContext db, ProfitService profit)
        => new(db, new MockScanExtractionService(), profit, Substitute.For<IAuditoriaService>());

    private static SubirScanRequest Captura()
        => new() { Nombre = "factura.jpg", ContentType = "image/jpeg", ContenidoBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }) };

    [Fact]
    public async Task ConfirmarScanComoGasto_CreaProfitGasto_YApareceEnProfit()
    {
        using var db = NewDb();
        var profit = new ProfitService(db, Substitute.For<IAuditoriaService>());
        var scan = NewScan(db, profit);

        // Subir captura (mock => REQUIERE_REVISION) y corregir importes.
        var subido = await scan.SubirAsync(Empresa, Captura(), "tester");
        subido.IsSuccess.Should().BeTrue();
        await scan.CorregirAsync(Empresa, subido.Value!.Id,
            new CorregirScanRequest { EmisorNombre = "Proveedor X", Total = 113m, Subtotal = 100m, Iva = 13m }, "tester");

        // Confirmar como gasto (cruza a NeoProfit).
        var confirmado = await scan.ConfirmarComoGastoAsync(Empresa, subido.Value.Id,
            new CreateProfitGastoRequest { Categoria = "SERVICIOS", Descripcion = "Proveedor X", Monto = 100m, IvaMonto = 13m, IvaDeducible = true }, "tester");

        confirmado.IsSuccess.Should().BeTrue();
        confirmado.Value!.EstadoCodigo.Should().Be("CONFIRMADO");
        confirmado.Value.ProfitGastoId.Should().NotBeNull();

        // El gasto existe en NeoProfit.
        var gastos = await profit.ListGastosAsync(Empresa, new PagedQuery { Page = 1, PageSize = 20 });
        gastos.Value!.Items.Should().ContainSingle(g => g.Descripcion == "Proveedor X" && g.Monto == 100m);
    }

    [Fact]
    public async Task ConfirmarScanComoDteRecibido_LoListaDteRecibidoService()
    {
        using var db = NewDb();
        var profit = new ProfitService(db, Substitute.For<IAuditoriaService>());
        var scan = NewScan(db, profit);

        var subido = await scan.SubirAsync(Empresa, Captura(), "tester");
        var dte = await scan.RegistrarDteRecibidoAsync(Empresa, subido.Value!.Id, new RegistrarDteRecibidoRequest
        {
            EmisorNombre = "Distribuidora SA", EmisorNit = "0614-1", TipoDteCodigo = "03",
            NumeroControl = "DTE-03-001", Subtotal = 100m, Iva = 13m, Total = 113m,
        }, "tester");

        dte.IsSuccess.Should().BeTrue();
        dte.Value!.DteRecibidoId.Should().NotBeNull();

        var recibidos = new DteRecibidoService(db);
        var lista = await recibidos.ListAsync(Empresa, new DteRecibidoQuery());
        lista.Value!.Items.Should().ContainSingle(r => r.EmisorNombre == "Distribuidora SA" && r.Total == 113m);
    }
}
