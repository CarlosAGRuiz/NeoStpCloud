using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Productos.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Productos;

/// <summary>Entrega 5 — escalas de precio por volumen y unidades alternativas.</summary>
public class ProductoPreciosTests
{
    private const int Empresa = 99;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"prec-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "F", RazonSocial = "Ferretería", EstadoCodigo = "ACTIVA" });
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = Empresa, CodigoInterno = "CLAVO", Nombre = "Clavo 2\"", PrecioUnitario = 0.10m,
            EstadoCodigo = "ACTIVO", UnidadMedidaCodigo = "59", TipoItem = "BIEN",
        });
        db.SaveChanges();
        return db;
    }

    private static ProductosService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    // ─── PrecioResolver (puro) ──────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 0.10)]    // sin escala alcanzada → base
    [InlineData(12, 0.08)]   // docena
    [InlineData(99, 0.08)]
    [InlineData(100, 0.05)]  // ciento
    [InlineData(500, 0.05)]
    public void Resolver_AplicaLaMejorEscalaAlcanzada(decimal cantidad, decimal esperado)
    {
        var escalas = new[] { (12m, 0.08m), (100m, 0.05m) };

        PrecioResolver.Resolver(0.10m, escalas, cantidad).Should().Be(esperado);
    }

    [Fact]
    public void Resolver_SinEscalas_UsaBase()
        => PrecioResolver.Resolver(5m, [], 1000m).Should().Be(5m);

    // ─── SetPrecios / GetPrecios ────────────────────────────────────────────────

    [Fact]
    public async Task SetPrecios_GuardaYReemplazaJuegoCompleto()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r1 = await svc.SetPreciosAsync(Empresa, 1, new SetProductoPreciosRequest
        {
            Escalas = [new() { CantidadMinima = 12, PrecioUnitario = 0.08m }, new() { CantidadMinima = 100, PrecioUnitario = 0.05m }],
            Unidades = [new() { UnidadMedidaCodigo = "58", Nombre = "Docena", Factor = 12, PrecioUnitario = 0.96m }],
        }, "t");
        r1.IsSuccess.Should().BeTrue(r1.Error);
        r1.Value!.Escalas.Should().HaveCount(2);
        r1.Value.Unidades.Should().ContainSingle(u => u.Nombre == "Docena" && u.Factor == 12);

        // Reemplazo completo: guardar con una sola escala elimina las anteriores.
        var r2 = await svc.SetPreciosAsync(Empresa, 1, new SetProductoPreciosRequest
        {
            Escalas = [new() { CantidadMinima = 50, PrecioUnitario = 0.06m }],
        }, "t");
        r2.Value!.Escalas.Should().ContainSingle(e => e.CantidadMinima == 50);
        r2.Value.Unidades.Should().BeEmpty();
        (await db.ProductoPreciosEscala.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SetPrecios_Invalidos_Validation()
    {
        var svc = NewSvc(NewDb());

        (await svc.SetPreciosAsync(Empresa, 1, new SetProductoPreciosRequest
        {
            Escalas = [new() { CantidadMinima = 12, PrecioUnitario = 1 }, new() { CantidadMinima = 12, PrecioUnitario = 2 }],
        }, "t")).ErrorCode.Should().Be("VALIDATION"); // mínimas duplicadas

        (await svc.SetPreciosAsync(Empresa, 1, new SetProductoPreciosRequest
        {
            Unidades = [new() { UnidadMedidaCodigo = "58", Nombre = "Docena", Factor = 0 }],
        }, "t")).ErrorCode.Should().Be("VALIDATION"); // factor cero

        (await svc.SetPreciosAsync(Empresa, 999, new SetProductoPreciosRequest(), "t"))
            .ErrorCode.Should().Be("PRODUCTO_NOT_FOUND");
    }

    [Fact]
    public async Task GetEscalas_DevuelveVariosProductosEnUnaConsulta()
    {
        var db = NewDb();
        db.Productos.Add(new Producto
        {
            Id = 2, EmpresaId = Empresa, CodigoInterno = "TORNILLO", Nombre = "Tornillo", PrecioUnitario = 0.15m,
            EstadoCodigo = "ACTIVO", UnidadMedidaCodigo = "59", TipoItem = "BIEN",
        });
        await db.SaveChangesAsync();
        var svc = NewSvc(db);
        await svc.SetPreciosAsync(Empresa, 1, new SetProductoPreciosRequest
        { Escalas = [new() { CantidadMinima = 12, PrecioUnitario = 0.08m }] }, "t");

        var map = await svc.GetEscalasAsync(Empresa, [1, 2]);

        map.Should().ContainKey(1);
        map.Should().NotContainKey(2); // sin escalas no aparece
        map[1].Should().ContainSingle(e => e.CantidadMinima == 12);
    }
}
