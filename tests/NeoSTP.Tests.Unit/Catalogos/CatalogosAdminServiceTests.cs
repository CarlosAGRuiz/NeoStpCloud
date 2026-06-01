using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Catalogos;

/// <summary>
/// Sprint 13.2 — CatalogosService: CRUD admin, multi-tenant, reglas de catálogos
/// del sistema, cascada padre/hijo.
/// </summary>
public class CatalogosAdminServiceTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 20;

    private static (CatalogosService svc, NeoStpDbContext db) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cat-admin-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        seed?.Invoke(db);
        db.SaveChanges();
        var audit = Substitute.For<IAuditoriaService>();
        return (new CatalogosService(db, audit), db);
    }

    // -------------------------------------------------------------- Catálogos

    [Fact]
    public async Task CreateAsync_OnEmpresa_PersistsAsNonSystem()
    {
        var (svc, db) = Build();

        var result = await svc.CreateAsync(EmpresaA, new CreateCatalogoRequest
        {
            Codigo = "estados_pedido", Nombre = "Estados de pedido",
        }, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Codigo.Should().Be("ESTADOS_PEDIDO");
        result.Value.EsSistema.Should().BeFalse();
        result.Value.EmpresaId.Should().Be(EmpresaA);
        result.Value.Version.Should().Be(1);
        (await db.Catalogos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithNullEmpresa_PersistsAsSystemCatalog()
    {
        var (svc, _) = Build();

        var result = await svc.CreateAsync(empresaId: null, new CreateCatalogoRequest
        {
            Codigo = "TIPO_X", Nombre = "Tipo X",
        }, "superadmin");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EsSistema.Should().BeTrue();
        result.Value.EmpresaId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsDuplicate_WhenSameCodigoAndVersion()
    {
        var (svc, _) = Build(db => db.Catalogos.Add(new Catalogo
        {
            Codigo = "X", Nombre = "X", EmpresaId = EmpresaA, Version = 1,
        }));

        var result = await svc.CreateAsync(EmpresaA, new CreateCatalogoRequest
        {
            Codigo = "x", Nombre = "X dup", Version = 1,
        }, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CAT_DUPLICATE");
    }

    [Fact]
    public async Task CreateAsync_AllowsSameCodigo_WithDifferentVersion()
    {
        var (svc, db) = Build(d => d.Catalogos.Add(new Catalogo
        {
            Codigo = "MUNICIPIO_ES", Nombre = "Municipios v1", EmpresaId = null, EsSistema = true, Version = 1,
        }));

        var result = await svc.CreateAsync(empresaId: null, new CreateCatalogoRequest
        {
            Codigo = "MUNICIPIO_ES", Nombre = "Municipios v2", Version = 2,
        }, "superadmin");

        result.IsSuccess.Should().BeTrue();
        (await db.Catalogos.CountAsync(c => c.Codigo == "MUNICIPIO_ES")).Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidation_OnMissingCodigo()
    {
        var (svc, _) = Build();

        var result = await svc.CreateAsync(EmpresaA, new CreateCatalogoRequest
        {
            Codigo = "  ", Nombre = "X",
        }, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task UpdateAsync_RejectsInactivar_OnSystemCatalog()
    {
        var (svc, _) = Build(d => d.Catalogos.Add(new Catalogo
        {
            Codigo = "AMBIENTE_DTE", Nombre = "Ambiente", EsSistema = true, Activo = true, EmpresaId = null,
        }));

        var result = await svc.UpdateAsync(empresaId: null, "AMBIENTE_DTE", new UpdateCatalogoRequest
        {
            Nombre = "Ambiente", Activo = false,
        }, "superadmin");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CAT_SYSTEM_NOT_EDITABLE");
    }

    [Fact]
    public async Task UpdateAsync_ScopedByEmpresa_DoesNotTouchOtherTenant()
    {
        var (svc, db) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Codigo = "X", Nombre = "X-A", EmpresaId = EmpresaA });
            d.Catalogos.Add(new Catalogo { Codigo = "X", Nombre = "X-B", EmpresaId = EmpresaB });
        });

        var result = await svc.UpdateAsync(EmpresaA, "X", new UpdateCatalogoRequest { Nombre = "renombrado-A" }, "tester");

        result.IsSuccess.Should().BeTrue();
        var b = await db.Catalogos.AsNoTracking().SingleAsync(c => c.EmpresaId == EmpresaB);
        b.Nombre.Should().Be("X-B"); // intocable
    }

    // ------------------------------------------------------------------ Ítems

    [Fact]
    public async Task CreateItemAsync_ReturnsDuplicate_WhenItemCodigoExists()
    {
        var (svc, _) = Build(d =>
        {
            var cat = new Catalogo { Id = 1, Codigo = "FORMA_PAGO", Nombre = "FP", EmpresaId = EmpresaA };
            d.Catalogos.Add(cat);
            d.CatalogoItems.Add(new CatalogoItem { CatalogoId = 1, Codigo = "01", Valor = "Efectivo" });
        });

        var result = await svc.CreateItemAsync(EmpresaA, "FORMA_PAGO", new CreateCatalogoItemRequest
        {
            Codigo = "01", Valor = "duplicado",
        }, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CAT_ITEM_DUPLICATE");
    }

    [Fact]
    public async Task CreateItemAsync_ReturnsParentNotFound_WhenParentMissing()
    {
        var (svc, _) = Build(d => d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "MUNICIPIO_ES", Nombre = "Municipios", EmpresaId = null }));

        var result = await svc.CreateItemAsync(empresaId: null, "MUNICIPIO_ES", new CreateCatalogoItemRequest
        {
            Codigo = "SS-99", Valor = "X", ParentCodigo = "SS",
        }, "superadmin");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CAT_PARENT_NOT_FOUND");
    }

    [Fact]
    public async Task DeleteItemAsync_RejectsSystemItem()
    {
        var (svc, _) = Build(d =>
        {
            var cat = new Catalogo { Id = 1, Codigo = "AMBIENTE_DTE", Nombre = "Ambiente", EmpresaId = null, EsSistema = true };
            d.Catalogos.Add(cat);
            d.CatalogoItems.Add(new CatalogoItem { Id = 10, CatalogoId = 1, Codigo = "00", Valor = "Pruebas", EsSistema = true });
        });

        var result = await svc.DeleteItemAsync(empresaId: null, "AMBIENTE_DTE", 10, "superadmin");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CAT_ITEM_SYSTEM");
    }

    [Fact]
    public async Task DeleteItemAsync_RejectsItemWithChildren()
    {
        var (svc, _) = Build(d =>
        {
            var cat = new Catalogo { Id = 1, Codigo = "MUNICIPIO_ES", Nombre = "Municipios", EmpresaId = null };
            d.Catalogos.Add(cat);
            d.CatalogoItems.Add(new CatalogoItem { Id = 100, CatalogoId = 1, Codigo = "SS", Valor = "San Salvador" });
            d.CatalogoItems.Add(new CatalogoItem { Id = 101, CatalogoId = 1, Codigo = "SS-01", Valor = "Soyapango", ParentCodigo = "SS" });
        });

        var result = await svc.DeleteItemAsync(empresaId: null, "MUNICIPIO_ES", 100, "superadmin");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CAT_ITEM_HAS_CHILDREN");
    }

    [Fact]
    public async Task UpdateItemAsync_RejectsSelfParent()
    {
        var (svc, _) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "X", Nombre = "X", EmpresaId = EmpresaA });
            d.CatalogoItems.Add(new CatalogoItem { Id = 99, CatalogoId = 1, Codigo = "AAA", Valor = "A" });
        });

        var result = await svc.UpdateItemAsync(EmpresaA, "X", 99, new UpdateCatalogoItemRequest
        {
            Valor = "A", ParentCodigo = "AAA",
        }, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CAT_PARENT_SELF");
    }

    [Fact]
    public async Task GetItemsAsync_FiltersByParent()
    {
        var (svc, _) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "MUNICIPIO_ES", Nombre = "M", EmpresaId = null });
            d.CatalogoItems.AddRange(
                new CatalogoItem { CatalogoId = 1, Codigo = "SS", Valor = "San Salvador" },
                new CatalogoItem { CatalogoId = 1, Codigo = "SS-01", Valor = "Soyapango", ParentCodigo = "SS" },
                new CatalogoItem { CatalogoId = 1, Codigo = "SS-02", Valor = "Mejicanos", ParentCodigo = "SS" },
                new CatalogoItem { CatalogoId = 1, Codigo = "LL-01", Valor = "Santa Tecla", ParentCodigo = "LL" });
        });

        var result = await svc.GetItemsAsync("MUNICIPIO_ES", empresaId: null, parentCodigo: "SS");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value!.Select(i => i.Codigo).Should().Contain(new[] { "SS-01", "SS-02" });
    }

    [Fact]
    public async Task GetItemsAsync_RootSentinel_ReturnsOnlyTopLevel()
    {
        var (svc, _) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "MUNICIPIO_ES", Nombre = "M", EmpresaId = null });
            d.CatalogoItems.AddRange(
                new CatalogoItem { CatalogoId = 1, Codigo = "SS", Valor = "San Salvador" },
                new CatalogoItem { CatalogoId = 1, Codigo = "LL", Valor = "La Libertad" },
                new CatalogoItem { CatalogoId = 1, Codigo = "SS-01", Valor = "Soyapango", ParentCodigo = "SS" });
        });

        var result = await svc.GetItemsAsync("MUNICIPIO_ES", empresaId: null, parentCodigo: "__ROOT__");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value!.Select(i => i.Codigo).Should().BeEquivalentTo(new[] { "SS", "LL" });
    }
}
