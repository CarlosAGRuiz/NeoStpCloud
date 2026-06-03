using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Datos;

/// <summary>
/// Sprint 26.4.a — restauración (reactivación) de maestros inactivados por error.
/// Verifica que el soft-restore vuelve a ACTIVO, valida estado y aísla por empresa.
/// </summary>
public class RestaurarMaestrosTests
{
    private const int EmpresaA = 10;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"restore-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0614", RazonSocial = "Demo", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    // ─── Clientes ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RestaurarCliente_ReactivaInactivo()
    {
        var db = NewDb();
        db.Clientes.Add(new Cliente
        {
            Id = 1, EmpresaId = EmpresaA, TipoDocumentoCodigo = "DUI", NumeroDocumento = "12345678-9",
            Nombre = "Cliente", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "INACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new ClientesService(db, Substitute.For<IAuditoriaService>());

        var r = await svc.RestaurarAsync(EmpresaA, 1, "tester");

        r.IsSuccess.Should().BeTrue();
        (await db.Clientes.AsNoTracking().FirstAsync(c => c.Id == 1)).EstadoCodigo.Should().Be("ACTIVO");
    }

    [Fact]
    public async Task RestaurarCliente_YaActivo_RetornaInvalidState()
    {
        var db = NewDb();
        db.Clientes.Add(new Cliente
        {
            Id = 2, EmpresaId = EmpresaA, TipoDocumentoCodigo = "DUI", NumeroDocumento = "11111111-1",
            Nombre = "Activo", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new ClientesService(db, Substitute.For<IAuditoriaService>());

        var r = await svc.RestaurarAsync(EmpresaA, 2, "tester");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task RestaurarCliente_Inexistente_RetornaNotFound()
    {
        var db = NewDb();
        var svc = new ClientesService(db, Substitute.For<IAuditoriaService>());

        var r = await svc.RestaurarAsync(EmpresaA, 999, "tester");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("CLIENTE_NOT_FOUND");
    }

    [Fact]
    public async Task RestaurarCliente_DeOtraEmpresa_NoLoEncuentra()
    {
        var db = NewDb();
        db.Clientes.Add(new Cliente
        {
            Id = 3, EmpresaId = 99, TipoDocumentoCodigo = "DUI", NumeroDocumento = "22222222-2",
            Nombre = "Ajeno", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "INACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new ClientesService(db, Substitute.For<IAuditoriaService>());

        var r = await svc.RestaurarAsync(EmpresaA, 3, "tester");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("CLIENTE_NOT_FOUND");
    }

    // ─── Productos ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RestaurarProducto_ReactivaInactivo()
    {
        var db = NewDb();
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = EmpresaA, CodigoInterno = "PROD-1", Nombre = "Producto",
            TipoItem = "BIEN", EstadoCodigo = "INACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new ProductosService(db, Substitute.For<IAuditoriaService>());

        var r = await svc.RestaurarAsync(EmpresaA, 1, "tester");

        r.IsSuccess.Should().BeTrue();
        (await db.Productos.AsNoTracking().FirstAsync(p => p.Id == 1)).EstadoCodigo.Should().Be("ACTIVO");
    }

    [Fact]
    public async Task RestaurarProducto_YaActivo_RetornaInvalidState()
    {
        var db = NewDb();
        db.Productos.Add(new Producto
        {
            Id = 2, EmpresaId = EmpresaA, CodigoInterno = "PROD-2", Nombre = "Activo",
            TipoItem = "BIEN", EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new ProductosService(db, Substitute.For<IAuditoriaService>());

        var r = await svc.RestaurarAsync(EmpresaA, 2, "tester");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task RestaurarProducto_Inexistente_RetornaNotFound()
    {
        var db = NewDb();
        var svc = new ProductosService(db, Substitute.For<IAuditoriaService>());

        var r = await svc.RestaurarAsync(EmpresaA, 999, "tester");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("PRODUCTO_NOT_FOUND");
    }
}
