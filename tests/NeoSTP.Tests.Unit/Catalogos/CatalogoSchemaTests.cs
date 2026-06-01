using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Infrastructure.Persistence;
using Xunit;

namespace NeoSTP.Tests.Unit.Catalogos;

/// <summary>
/// Sprint 13.1 — verifica el contrato de las nuevas columnas extendidas:
/// Catalogo.Version, Catalogo.MetadataJson y CatalogoItem.ParentCodigo.
/// </summary>
public class CatalogoSchemaTests
{
    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"catalogos-schema-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    [Fact]
    public void Catalogo_New_HasVersion1AndNoMetadata_ByDefault()
    {
        var c = new Catalogo { Codigo = "TIPO_FACTURA", Nombre = "Tipo Factura" };

        c.Version.Should().Be(1, "el versionado de catálogos arranca en 1");
        c.MetadataJson.Should().BeNull();
        c.Activo.Should().BeTrue();
        c.EsSistema.Should().BeFalse();
    }

    [Fact]
    public void CatalogoItem_New_HasNoParentCodigo_ByDefault()
    {
        var i = new CatalogoItem { Codigo = "A", Valor = "A" };

        i.ParentCodigo.Should().BeNull("los ítems sin padre representan el nivel raíz");
        i.MetadataJson.Should().BeNull();
        i.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Catalogo_RoundTrip_PersistsVersionAndMetadata()
    {
        await using var db = NewDb();
        db.Catalogos.Add(new Catalogo
        {
            Codigo = "DEPARTAMENTO_ES",
            Nombre = "Departamentos de El Salvador",
            EsSistema = true,
            Version = 2,
            MetadataJson = "{\"fuente\":\"MH\",\"publicado\":\"2026-01-01\"}",
        });
        await db.SaveChangesAsync();

        var loaded = await db.Catalogos.AsNoTracking().SingleAsync(c => c.Codigo == "DEPARTAMENTO_ES");
        loaded.Version.Should().Be(2);
        loaded.MetadataJson.Should().Contain("MH");
    }

    [Fact]
    public async Task CatalogoItems_QueryByParentCodigo_ReturnsOnlyChildren()
    {
        await using var db = NewDb();
        var cat = new Catalogo { Codigo = "MUNICIPIO_ES", Nombre = "Municipios" };
        db.Catalogos.Add(cat);
        await db.SaveChangesAsync();

        db.CatalogoItems.AddRange(
            new CatalogoItem { CatalogoId = cat.Id, Codigo = "SS-01", Valor = "San Salvador", ParentCodigo = "SS" },
            new CatalogoItem { CatalogoId = cat.Id, Codigo = "SS-02", Valor = "Soyapango", ParentCodigo = "SS" },
            new CatalogoItem { CatalogoId = cat.Id, Codigo = "LL-01", Valor = "Santa Tecla", ParentCodigo = "LL" });
        await db.SaveChangesAsync();

        var hijosSS = await db.CatalogoItems.AsNoTracking()
            .Where(i => i.CatalogoId == cat.Id && i.ParentCodigo == "SS")
            .OrderBy(i => i.Codigo)
            .ToListAsync();

        hijosSS.Should().HaveCount(2);
        hijosSS.Select(h => h.Codigo).Should().ContainInOrder("SS-01", "SS-02");
    }

    [Fact]
    public async Task CatalogoItems_NullParentCodigo_RepresentsRootLevel()
    {
        await using var db = NewDb();
        var cat = new Catalogo { Codigo = "DEPARTAMENTO_ES", Nombre = "Departamentos" };
        db.Catalogos.Add(cat);
        await db.SaveChangesAsync();

        db.CatalogoItems.Add(new CatalogoItem
        {
            CatalogoId = cat.Id,
            Codigo = "SS",
            Valor = "San Salvador",
            ParentCodigo = null,
        });
        await db.SaveChangesAsync();

        var roots = await db.CatalogoItems.AsNoTracking()
            .Where(i => i.CatalogoId == cat.Id && i.ParentCodigo == null)
            .ToListAsync();
        roots.Should().HaveCount(1);
        roots[0].Codigo.Should().Be("SS");
    }
}
