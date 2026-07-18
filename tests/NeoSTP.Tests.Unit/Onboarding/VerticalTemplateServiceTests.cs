using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Onboarding;

/// <summary>Entrega 4 — plantillas de vertical: siembra idempotente de categorías por rubro.</summary>
public class VerticalTemplateServiceTests
{
    private const int Empresa = 98;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"vert-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "V", RazonSocial = "Vertical", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static VerticalTemplateService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    [Fact]
    public void Listar_IncluyeLosCincoRubros()
        => NewSvc(NewDb()).Listar().Select(p => p.Codigo)
            .Should().BeEquivalentTo(new[] { "FARMACIA", "FERRETERIA", "SALON", "TIENDA", "GENERAL" });

    [Fact]
    public async Task Aplicar_Farmacia_SiembraCategorias()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r = await svc.AplicarAsync(Empresa, "farmacia", "tester");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.CategoriasCreadas.Should().Be(7);
        var catalogo = await db.Catalogos.SingleAsync(c => c.Codigo == CatalogCodes.CategoriaProducto);
        catalogo.EmpresaId.Should().Be(Empresa);
        (await db.CatalogoItems.CountAsync(i => i.CatalogoId == catalogo.Id)).Should().Be(7);
        (await db.CatalogoItems.SingleAsync(i => i.Codigo == "CUIDADO_PERSONAL")).Valor.Should().Be("Cuidado personal");
    }

    [Fact]
    public async Task Aplicar_DosVeces_EsIdempotente()
    {
        var db = NewDb(); var svc = NewSvc(db);

        await svc.AplicarAsync(Empresa, "TIENDA", "t");
        var segunda = await svc.AplicarAsync(Empresa, "TIENDA", "t");

        segunda.Value!.CategoriasCreadas.Should().Be(0);
        segunda.Value.CategoriasExistentes.Should().Be(6);
        (await db.CatalogoItems.CountAsync()).Should().Be(6);
    }

    [Fact]
    public async Task Aplicar_SobreCategoriasPropias_SoloAgregaFaltantes()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.AplicarAsync(Empresa, "GENERAL", "t"); // PRODUCTOS + SERVICIOS

        var r = await svc.AplicarAsync(Empresa, "SALON", "t"); // 6 nuevas

        r.Value!.CategoriasCreadas.Should().Be(6);
        (await db.CatalogoItems.CountAsync()).Should().Be(8);
    }

    [Fact]
    public async Task Aplicar_CodigoInvalido_Falla()
        => (await NewSvc(NewDb()).AplicarAsync(Empresa, "PANADERIA", "t"))
            .ErrorCode.Should().Be("PLANTILLA_NOT_FOUND");
}
