using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Scan;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Scan;

/// <summary>DTE recibidos: listado/detalle con aislamiento por empresa y filtros.</summary>
public class DteRecibidoServiceTests
{
    private const int EmpresaA = 70;
    private const int EmpresaB = 71;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"recibidos-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static DteDocumentoRecibido Recibido(int empresaId, string emisor, DateOnly fecha, string? nit = null, string? control = null, decimal total = 100m)
        => new()
        {
            EmpresaId = empresaId, EmisorNombre = emisor, EmisorNit = nit,
            Fecha = fecha, NumeroControl = control, Subtotal = total * 0.88m, Iva = total * 0.12m, Total = total,
        };

    [Fact]
    public async Task ListAsync_AislaPorEmpresa_YOrdenaPorFechaDesc()
    {
        var db = NewDb();
        db.DteDocumentosRecibidos.AddRange(
            Recibido(EmpresaA, "Proveedor Viejo", new DateOnly(2026, 1, 10)),
            Recibido(EmpresaA, "Proveedor Nuevo", new DateOnly(2026, 5, 20)),
            Recibido(EmpresaB, "Otra Empresa", new DateOnly(2026, 6, 1)));
        await db.SaveChangesAsync();
        var svc = new DteRecibidoService(db);

        var result = await svc.ListAsync(EmpresaA, new DteRecibidoQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(2);
        result.Value.Items.First().EmisorNombre.Should().Be("Proveedor Nuevo");
    }

    [Fact]
    public async Task ListAsync_FiltraPorBusquedaYRango()
    {
        var db = NewDb();
        db.DteDocumentosRecibidos.AddRange(
            Recibido(EmpresaA, "Acme SA", new DateOnly(2026, 2, 1), nit: "0614-1", control: "DTE-001"),
            Recibido(EmpresaA, "Globex", new DateOnly(2026, 3, 1), control: "DTE-002"),
            Recibido(EmpresaA, "Acme Sucursal", new DateOnly(2026, 4, 1), control: "DTE-003"));
        await db.SaveChangesAsync();
        var svc = new DteRecibidoService(db);

        var porTexto = await svc.ListAsync(EmpresaA, new DteRecibidoQuery { Search = "Acme" });
        porTexto.Value!.Total.Should().Be(2);

        var porRango = await svc.ListAsync(EmpresaA, new DteRecibidoQuery { Desde = new DateOnly(2026, 3, 1), Hasta = new DateOnly(2026, 3, 31) });
        porRango.Value!.Total.Should().Be(1);
        porRango.Value.Items.Single().EmisorNombre.Should().Be("Globex");
    }

    [Fact]
    public async Task GetAsync_DeOtraEmpresa_DevuelveNotFound()
    {
        var db = NewDb();
        var r = Recibido(EmpresaB, "Ajena", new DateOnly(2026, 1, 1));
        db.DteDocumentosRecibidos.Add(r);
        await db.SaveChangesAsync();
        var svc = new DteRecibidoService(db);

        var result = await svc.GetAsync(EmpresaA, r.Id);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("RECIBIDO_NOT_FOUND");
    }

    [Fact]
    public async Task GetAsync_Existente_MapeaCampos()
    {
        var db = NewDb();
        var r = Recibido(EmpresaA, "Proveedor X", new DateOnly(2026, 5, 5), nit: "0614-9", control: "DTE-999", total: 226m);
        db.DteDocumentosRecibidos.Add(r);
        await db.SaveChangesAsync();
        var svc = new DteRecibidoService(db);

        var result = await svc.GetAsync(EmpresaA, r.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EmisorNombre.Should().Be("Proveedor X");
        result.Value.NumeroControl.Should().Be("DTE-999");
        result.Value.Total.Should().Be(226m);
    }
}
