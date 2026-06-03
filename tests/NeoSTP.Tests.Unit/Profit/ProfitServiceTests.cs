using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Profit;

/// <summary>
/// NeoProfit — integración del ProfitService: agregación del dashboard (ventas, costo,
/// ganancia, gastos, utilidad) y aislamiento por empresa.
/// </summary>
public class ProfitServiceTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 11;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"profit-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    private static ProfitService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static void SeedVentaConCosto(NeoStpDbContext db, int empresaId, int docId, int prodId,
        decimal cantidad, decimal venta, decimal? costoUnitario, decimal iva)
    {
        db.Productos.Add(new Producto
        {
            Id = prodId, EmpresaId = empresaId, CodigoInterno = $"P{prodId}", Nombre = $"Prod {prodId}",
            TipoItem = "BIEN", PrecioUnitario = venta / cantidad, CostoUnitario = costoUnitario, EstadoCodigo = "ACTIVO",
        });
        db.DteDocumentos.Add(new DteDocumento
        {
            Id = docId, EmpresaId = empresaId, TipoDteCodigo = TipoDteCodigos.FacturaConsumidorFinal,
            EstadoCodigo = DteEstadoCodigos.Procesado, FechaEmision = DateTime.UtcNow.Date,
            NumeroControl = $"DTE-01-{docId:D6}", CodigoGeneracion = Guid.NewGuid().ToString(),
            TotalGravada = venta, IvaTotal = iva, TotalPagar = venta, ReceptorNombre = "Cliente X",
        });
        db.DteDocumentoDetalles.Add(new DteDocumentoDetalle
        {
            DocumentoId = docId, NumeroLinea = 1, ProductoId = prodId, Codigo = $"P{prodId}",
            Descripcion = $"Prod {prodId}", Cantidad = cantidad, PrecioUnitario = venta / cantidad,
            VentaGravada = venta,
        });
    }

    [Fact]
    public async Task Dashboard_CalculaVentasGananciaYUtilidad()
    {
        var db = NewDb();
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0614", RazonSocial = "Demo", EstadoCodigo = "ACTIVA" });
        SeedVentaConCosto(db, EmpresaA, docId: 1, prodId: 1, cantidad: 10m, venta: 1000m, costoUnitario: 60m, iva: 130m);
        db.ProfitGastos.Add(new Domain.Core.Profit.ProfitGasto
        {
            EmpresaId = EmpresaA, Fecha = DateOnly.FromDateTime(DateTime.UtcNow), Categoria = "ALQUILER",
            Descripcion = "Renta local", Monto = 100m, IvaMonto = 13m, IvaDeducible = true, EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();

        var dash = await NewSvc(db).GetDashboardAsync(EmpresaA, new ProfitPeriodoQuery());

        dash.VentaNeta.Should().Be(1000m);
        dash.CostoVentas.Should().Be(600m);
        dash.GananciaBruta.Should().Be(400m);
        dash.MargenPorcentaje.Should().Be(40m);
        dash.GastosTotal.Should().Be(100m);
        dash.UtilidadNeta.Should().Be(300m);   // 400 - 100
        dash.IvaGenerado.Should().Be(130m);
        dash.IvaCredito.Should().Be(13m);       // gasto deducible
        dash.IvaNeto.Should().Be(117m);
        dash.TopProductos.Should().ContainSingle();
        dash.TopProductos[0].Ganancia.Should().Be(400m);
    }

    [Fact]
    public async Task Dashboard_CostoPendiente_CuandoProductoSinCosto()
    {
        var db = NewDb();
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0614", RazonSocial = "Demo", EstadoCodigo = "ACTIVA" });
        SeedVentaConCosto(db, EmpresaA, 1, 1, 5m, 500m, costoUnitario: null, iva: 65m);
        await db.SaveChangesAsync();

        var dash = await NewSvc(db).GetDashboardAsync(EmpresaA, new ProfitPeriodoQuery());

        dash.LineasSinCosto.Should().Be(1);
        dash.CostoVentas.Should().Be(0m);
        dash.TopProductos[0].CostoPendiente.Should().BeTrue();
    }

    [Fact]
    public async Task Dashboard_AislaPorEmpresa()
    {
        var db = NewDb();
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        SeedVentaConCosto(db, EmpresaA, 1, 1, 10m, 1000m, 60m, 130m);
        SeedVentaConCosto(db, EmpresaB, 2, 2, 99m, 9999m, 10m, 1300m);
        await db.SaveChangesAsync();

        var dash = await NewSvc(db).GetDashboardAsync(EmpresaA, new ProfitPeriodoQuery());

        dash.VentaNeta.Should().Be(1000m); // no incluye a EmpresaB
        dash.Documentos.Should().Be(1);
    }

    [Fact]
    public async Task Gastos_CrudYSoftDelete()
    {
        var db = NewDb();
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var creado = await svc.CreateGastoAsync(EmpresaA, new CreateProfitGastoRequest
        {
            Categoria = "servicios", Descripcion = "Luz", Monto = 50m, IvaMonto = 6.5m,
        }, "tester");
        creado.IsSuccess.Should().BeTrue();
        creado.Value!.Categoria.Should().Be("SERVICIOS"); // normalizado
        creado.Value.Total.Should().Be(56.5m);

        var baja = await svc.InactivarGastoAsync(EmpresaA, creado.Value.Id, "tester");
        baja.IsSuccess.Should().BeTrue();

        var rebaja = await svc.InactivarGastoAsync(EmpresaA, creado.Value.Id, "tester");
        rebaja.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task CreateGasto_MontoNegativo_Validation()
    {
        var db = NewDb();
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        await db.SaveChangesAsync();

        var r = await NewSvc(db).CreateGastoAsync(EmpresaA, new CreateProfitGastoRequest { Descripcion = "X", Monto = -1m }, "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }
}
