using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Infrastructure.Persistence;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte.Certificacion;

/// <summary>
/// Sprint 14.1 — contrato del esquema de certificación (entidades, defaults,
/// cascadas y semilla de la matriz oficial).
/// </summary>
public class CertificacionSchemaTests
{
    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cert-schema-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    [Fact]
    public void CertificacionPrueba_New_HasPendienteEstadoAndIntento1()
    {
        var p = new CertificacionPrueba { EmpresaId = 1, EscenarioId = 10001 };

        p.EstadoCodigo.Should().Be(CertificacionEstadoCodigos.Pendiente);
        p.IntentoNumero.Should().Be(1);
        p.DteDocumentoId.Should().BeNull();
        p.ProcesadoAt.Should().BeNull();
    }

    [Fact]
    public async Task Matriz_RoundTrip_PersistsAllProperties()
    {
        await using var db = NewDb();
        db.CertificacionMatriz.Add(new CertificacionMatriz
        {
            TipoDteCodigo = "01",
            Nombre = "Factura",
            Descripcion = "Test",
            EscenariosRequeridos = 90,
            Orden = 1,
        });
        await db.SaveChangesAsync();

        var loaded = await db.CertificacionMatriz.AsNoTracking().SingleAsync();
        loaded.TipoDteCodigo.Should().Be("01");
        loaded.EscenariosRequeridos.Should().Be(90);
        loaded.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Escenario_BelongsToMatriz_AndIsCascadeDeletedWithIt()
    {
        await using var db = NewDb();
        var matriz = new CertificacionMatriz
        {
            TipoDteCodigo = "01", Nombre = "Factura", EscenariosRequeridos = 1, Orden = 1,
        };
        db.CertificacionMatriz.Add(matriz);
        await db.SaveChangesAsync();

        db.CertificacionEscenarios.Add(new CertificacionEscenario
        {
            MatrizId = matriz.Id, Codigo = "FACTURA-01", Nombre = "Escenario 01", Orden = 1,
        });
        await db.SaveChangesAsync();

        (await db.CertificacionEscenarios.CountAsync()).Should().Be(1);

        db.CertificacionMatriz.Remove(matriz);
        await db.SaveChangesAsync();

        // InMemory respeta cascadas configuradas vía OnDelete.Cascade
        (await db.CertificacionEscenarios.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Prueba_LinksEscenarioAndCapturesErrores()
    {
        await using var db = NewDb();
        var matriz = new CertificacionMatriz { TipoDteCodigo = "01", Nombre = "Factura", EscenariosRequeridos = 1, Orden = 1 };
        db.CertificacionMatriz.Add(matriz);
        var escenario = new CertificacionEscenario { Matriz = matriz, Codigo = "FACTURA-01", Nombre = "x", Orden = 1 };
        db.CertificacionEscenarios.Add(escenario);
        await db.SaveChangesAsync();

        var prueba = new CertificacionPrueba
        {
            EmpresaId = 10, EscenarioId = escenario.Id,
            EstadoCodigo = CertificacionEstadoCodigos.Error,
        };
        db.CertificacionPruebas.Add(prueba);
        await db.SaveChangesAsync();

        db.CertificacionErrores.Add(new CertificacionError
        {
            PruebaId = prueba.Id, CodigoMh = "096",
            Descripcion = "El esquema no cumple normativa.",
        });
        await db.SaveChangesAsync();

        var loaded = await db.CertificacionPruebas
            .Include(p => p.Errores)
            .AsNoTracking()
            .SingleAsync();
        loaded.Errores.Should().HaveCount(1);
        loaded.Errores.First().CodigoMh.Should().Be("096");
    }

    [Fact]
    public async Task MatrizSeed_Contains15TiposWithCorrectQuantities()
    {
        // EnsureCreated aplica HasData en InMemory.
        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();

        var matrices = await db.CertificacionMatriz.AsNoTracking().OrderBy(m => m.Orden).ToListAsync();
        matrices.Should().HaveCount(15, "la matriz oficial Hacienda tiene 15 entradas (11 DTE + 4 eventos)");

        // Las cantidades del backlog deben coincidir exactamente.
        var esperadas = new Dictionary<string, int>
        {
            ["01"] = 90, ["03"] = 75, ["04"] = 50, ["05"] = 50, ["06"] = 25,
            ["07"] = 50, ["08"] = 75, ["09"] = 50, ["11"] = 90, ["14"] = 25, ["15"] = 25,
            ["INVALIDACION"] = 5, ["CONTINGENCIA"] = 5, ["RETORNO"] = 5, ["OPERACIONES_ESPECIALES"] = 5,
        };
        foreach (var (tipo, n) in esperadas)
        {
            matrices.Single(m => m.TipoDteCodigo == tipo).EscenariosRequeridos.Should().Be(n,
                $"la matriz para tipo {tipo} debe requerir {n} escenarios");
        }

        matrices.Sum(m => m.EscenariosRequeridos).Should().Be(625);
    }

    [Fact]
    public async Task EscenarioSeed_Contains625RowsLinkedToMatrices()
    {
        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();

        var escenarios = await db.CertificacionEscenarios.AsNoTracking().ToListAsync();
        escenarios.Should().HaveCount(625);

        // Cada matriz debe tener exactamente N escenarios.
        var groups = escenarios.GroupBy(e => e.MatrizId)
            .ToDictionary(g => g.Key, g => g.Count());
        var matrices = await db.CertificacionMatriz.AsNoTracking().ToListAsync();
        foreach (var m in matrices)
        {
            groups[m.Id].Should().Be(m.EscenariosRequeridos,
                $"el seed del tipo {m.TipoDteCodigo} debe generar {m.EscenariosRequeridos} escenarios");
        }
    }

    [Fact]
    public async Task EscenarioSeed_HasUniqueCodesPerMatriz()
    {
        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();

        var escenarios = await db.CertificacionEscenarios.AsNoTracking().ToListAsync();
        var duplicados = escenarios
            .GroupBy(e => new { e.MatrizId, e.Codigo })
            .Where(g => g.Count() > 1)
            .ToList();
        duplicados.Should().BeEmpty("el seed no debe generar códigos duplicados dentro de una matriz");
    }
}
