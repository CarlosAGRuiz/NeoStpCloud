using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Productos.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Productos;

/// <summary>Mejoras 4+6 — categorías de producto sobre catálogo por empresa.</summary>
public class ProductoCategoriaTests
{
    private const int Empresa = 91;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cat-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "E", RazonSocial = "E", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static ProductosService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static CreateProductoRequest Producto(string codigo, string? categoria = null) => new()
    {
        CodigoInterno = codigo,
        Nombre = $"Producto {codigo}",
        PrecioUnitario = 10m,
        CategoriaCodigo = categoria,
    };

    [Fact]
    public async Task Create_ConCategoriaNueva_CreaCatalogoEItem()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.CreateAsync(Empresa, Producto("P1", "Salud"), "tester");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.CategoriaCodigo.Should().Be("SALUD");

        var catalogo = await db.Catalogos.SingleAsync(c => c.Codigo == CatalogCodes.CategoriaProducto);
        catalogo.EmpresaId.Should().Be(Empresa);
        catalogo.EsSistema.Should().BeFalse();

        var item = await db.CatalogoItems.SingleAsync(i => i.CatalogoId == catalogo.Id);
        item.Codigo.Should().Be("SALUD");
        item.Valor.Should().Be("Salud");
    }

    [Fact]
    public async Task Create_CategoriaRepetida_NoDuplicaItem()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        (await svc.CreateAsync(Empresa, Producto("P1", "Salud"), "t")).IsSuccess.Should().BeTrue();
        (await svc.CreateAsync(Empresa, Producto("P2", "salud"), "t")).IsSuccess.Should().BeTrue();

        (await db.CatalogoItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetList_FiltraPorCategoria()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CreateAsync(Empresa, Producto("P1", "Salud"), "t");
        await svc.CreateAsync(Empresa, Producto("P2", "Higiene"), "t");
        await svc.CreateAsync(Empresa, Producto("P3"), "t");

        var todos = await svc.GetListAsync(Empresa, new PagedQuery());
        var salud = await svc.GetListAsync(Empresa, new PagedQuery(), "salud");

        todos.Value!.Total.Should().Be(3);
        salud.Value!.Total.Should().Be(1);
        salud.Value.Items[0].CodigoInterno.Should().Be("P1");
    }

    [Fact]
    public async Task GetCategorias_UneCatalogoYEnUso()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CreateAsync(Empresa, Producto("P1", "Salud"), "t");

        // Categoría definida en catálogo pero sin productos aún.
        var catalogo = await db.Catalogos.SingleAsync(c => c.Codigo == CatalogCodes.CategoriaProducto);
        db.CatalogoItems.Add(new CatalogoItem
        {
            CatalogoId = catalogo.Id, Codigo = "HIGIENE", Valor = "Higiene", Activo = true,
        });
        await db.SaveChangesAsync();

        var r = await svc.GetCategoriasAsync(Empresa);

        r.Value.Should().BeEquivalentTo(new[] { "HIGIENE", "SALUD" });
    }

    [Fact]
    public async Task Import_ConColumnaCategoria_AutoCrea()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var csv = "codigo,nombre,precio,categoria\nA1,Alcohol gel,2.50,Higiene\nA2,Ibuprofeno,1.25,Salud\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var r = await svc.ImportAsync(Empresa, new BulkImportRequest { Format = BulkFileFormat.Csv, Content = ms }, "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.Inserted.Should().Be(2);
        (await db.CatalogoItems.CountAsync()).Should().Be(2);
        (await db.Productos.SingleAsync(p => p.CodigoInterno == "A1")).CategoriaCodigo.Should().Be("HIGIENE");
    }
}

/// <summary>Mejora 4 — catálogos custom: colisiones y resolución empresa-vs-sistema.</summary>
public class CatalogosCustomTests
{
    private const int Empresa = 92;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cus-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "E", RazonSocial = "E", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static CatalogosService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    [Fact]
    public async Task Create_EmpresaConCodigoDeSistema_Bloqueado()
    {
        var db = NewDb();
        db.Catalogos.Add(new Catalogo { Codigo = "PAIS", Nombre = "País", EsSistema = true, Activo = true });
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var r = await svc.CreateAsync(Empresa, new CreateCatalogoRequest { Codigo = "pais", Nombre = "Mi país" }, "t");

        r.ErrorCode.Should().Be("CAT_SYSTEM_CODE");
    }

    [Fact]
    public async Task Create_CatalogoPropio_QuedaComoNoSistema()
    {
        var svc = NewSvc(NewDb());

        var r = await svc.CreateAsync(Empresa, new CreateCatalogoRequest { Codigo = "ZONAS", Nombre = "Zonas de reparto" }, "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.EsSistema.Should().BeFalse();
        r.Value.EmpresaId.Should().Be(Empresa);
    }

    [Fact]
    public async Task GetItems_PrefiereCatalogoDeEmpresaSobreGlobal()
    {
        var db = NewDb();
        // Legado: mismo código en catálogo global y de empresa.
        var global = new Catalogo { Codigo = "ZONAS", Nombre = "Global", EsSistema = true, Activo = true };
        var propio = new Catalogo { Codigo = "ZONAS", Nombre = "Propio", EsSistema = false, Activo = true, EmpresaId = Empresa };
        db.Catalogos.AddRange(global, propio);
        db.CatalogoItems.Add(new CatalogoItem { Catalogo = global, Codigo = "G1", Valor = "Global 1", Activo = true });
        db.CatalogoItems.Add(new CatalogoItem { Catalogo = propio, Codigo = "P1", Valor = "Propio 1", Activo = true });
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var r = await svc.GetItemsAsync("ZONAS", Empresa);

        r.Value.Should().ContainSingle(i => i.Codigo == "P1");
    }

    [Fact]
    public async Task CreateItem_EnCatalogoPropio_Funciona()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CreateAsync(Empresa, new CreateCatalogoRequest { Codigo = "ZONAS", Nombre = "Zonas" }, "t");

        var r = await svc.CreateItemAsync(Empresa, "ZONAS", new CreateCatalogoItemRequest { Codigo = "NORTE", Valor = "Zona norte" }, "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.EsSistema.Should().BeFalse();
    }

    [Fact]
    public async Task CreateItem_EnCatalogoDeSistema_NoVisibleParaEmpresa()
    {
        var db = NewDb();
        db.Catalogos.Add(new Catalogo { Codigo = "PAIS", Nombre = "País", EsSistema = true, Activo = true });
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var r = await svc.CreateItemAsync(Empresa, "PAIS", new CreateCatalogoItemRequest { Codigo = "X", Valor = "X" }, "t");

        r.ErrorCode.Should().Be("CAT_NOT_FOUND");
    }
}
