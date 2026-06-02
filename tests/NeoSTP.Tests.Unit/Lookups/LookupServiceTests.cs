using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Lookups;

/// <summary>
/// LK.1 — LookupService: mapeo y filtrado de catálogos, caché por instancia,
/// resolución territorial (distrito → municipio 2024) y datos maestros.
/// </summary>
public class LookupServiceTests
{
    private const int EmpresaA = 10;

    private static (LookupService svc, NeoStpDbContext db, ICatalogosService cat) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"lookup-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0614", RazonSocial = "Demo", EstadoCodigo = "ACTIVA" });
        seed?.Invoke(db);
        db.SaveChanges();
        var cat = Substitute.For<ICatalogosService>();
        return (new LookupService(db, cat), db, cat);
    }

    private static CatalogoItemDto Item(string codigo, string valor, string? parent = null, int orden = 0, bool activo = true, string? meta = null)
        => new() { Codigo = codigo, Valor = valor, ParentCodigo = parent, Orden = orden, Activo = activo, MetadataJson = meta };

    [Fact]
    public async Task GetCatalogo_MapeaFiltraInactivosYOrdena()
    {
        var (svc, _, cat) = Build();
        cat.GetItemsAsync("CAT", EmpresaA, null, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CatalogoItemDto>>.Ok(new List<CatalogoItemDto>
            {
                Item("B", "Beta", orden: 2),
                Item("Z", "Zeta", orden: 3, activo: false),
                Item("A", "Alfa", orden: 1),
            }));

        var items = await svc.GetCatalogoAsync("CAT", EmpresaA);

        items.Select(i => i.Value).Should().Equal("A", "B"); // ordenado, sin inactivos
        items[0].Label.Should().Be("Alfa");
    }

    [Fact]
    public async Task GetCatalogo_UsaCachePorInstancia()
    {
        var (svc, _, cat) = Build();
        cat.GetItemsAsync("CAT", EmpresaA, null, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CatalogoItemDto>>.Ok(new List<CatalogoItemDto> { Item("A", "Alfa") }));

        await svc.GetCatalogoAsync("CAT", EmpresaA);
        await svc.GetCatalogoAsync("CAT", EmpresaA);

        await cat.Received(1).GetItemsAsync("CAT", EmpresaA, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolverMunicipio2024_DevuelveParentDelDistrito()
    {
        var (svc, _, cat) = Build();
        cat.GetItemsAsync("DISTRITO_ES", EmpresaA, null, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CatalogoItemDto>>.Ok(new List<CatalogoItemDto>
            {
                Item("03", "Ayutuxtepeque", parent: "23"),
            }));

        (await svc.ResolverMunicipio2024Async("03", EmpresaA)).Should().Be("23");
        (await svc.ResolverMunicipio2024Async("99", EmpresaA)).Should().BeNull();
        (await svc.ResolverMunicipio2024Async("", EmpresaA)).Should().BeNull();
    }

    [Fact]
    public async Task BuscarClientes_FiltraPorEmpresaBusquedaYEstado()
    {
        var (svc, _, _) = Build(db =>
        {
            db.Clientes.Add(new Cliente { EmpresaId = EmpresaA, Nombre = "Droguería Sur", NumeroDocumento = "0614-1", TipoDocumentoCodigo = "36", EstadoCodigo = "ACTIVO" });
            db.Clientes.Add(new Cliente { EmpresaId = EmpresaA, Nombre = "Inversiones Norte", NumeroDocumento = "0614-2", TipoDocumentoCodigo = "36", EstadoCodigo = "ACTIVO" });
            db.Clientes.Add(new Cliente { EmpresaId = EmpresaA, Nombre = "Cliente Inactivo", NumeroDocumento = "0614-3", TipoDocumentoCodigo = "36", EstadoCodigo = "INACTIVO" });
            db.Clientes.Add(new Cliente { EmpresaId = 99, Nombre = "Droguería Otra", NumeroDocumento = "9-9", TipoDocumentoCodigo = "36", EstadoCodigo = "ACTIVO" });
        });

        var todos = await svc.BuscarClientesAsync(EmpresaA, null);
        todos.Should().HaveCount(2); // solo activos de EmpresaA

        var drog = await svc.BuscarClientesAsync(EmpresaA, "Drogue");
        drog.Should().ContainSingle().Which.Label.Should().Be("Droguería Sur");
    }

    [Fact]
    public async Task GetSucursales_DevuelveActivasDeLaEmpresa()
    {
        var (svc, _, _) = Build(db =>
        {
            db.Sucursales.Add(new Sucursal { EmpresaId = EmpresaA, Codigo = "0001", Nombre = "Casa Matriz", EstadoCodigo = "ACTIVO" });
            db.Sucursales.Add(new Sucursal { EmpresaId = EmpresaA, Codigo = "0002", Nombre = "Sucursal Cerrada", EstadoCodigo = "INACTIVO" });
        });

        var list = await svc.GetSucursalesAsync(EmpresaA);
        list.Should().ContainSingle().Which.Label.Should().Be("Casa Matriz");
    }

    [Fact]
    public async Task BuscarProductos_FiltraPorNombreOCodigo()
    {
        var (svc, _, _) = Build(db =>
        {
            db.Productos.Add(new Producto { EmpresaId = EmpresaA, CodigoInterno = "PROD-1", Nombre = "Licencia Anual", PrecioUnitario = 100m, EstadoCodigo = "ACTIVO" });
            db.Productos.Add(new Producto { EmpresaId = EmpresaA, CodigoInterno = "SRV-2", Nombre = "Consultoría", PrecioUnitario = 50m, EstadoCodigo = "ACTIVO" });
        });

        (await svc.BuscarProductosAsync(EmpresaA, "PROD-1")).Should().ContainSingle().Which.Label.Should().Be("Licencia Anual");
        (await svc.BuscarProductosAsync(EmpresaA, "Consul")).Should().ContainSingle();
    }
}
