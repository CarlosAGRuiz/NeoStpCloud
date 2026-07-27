using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Common;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Compras;

/// <summary>
/// Numeración de documentos internos. Antes las órdenes salían como
/// OC-20260726-97A39C684107448C: ilegible para el proveedor y sin noción de secuencia.
/// </summary>
public class CorrelativoServiceTests
{
    private static NeoStpDbContext NewDb() => new(
        new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"correl-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task PrimerNumero_EsUnoConFormatoLegible()
    {
        await using var db = NewDb();
        var svc = new CorrelativoService(db);

        var numero = await svc.SiguienteAsync(1, CorrelativoSeries.OrdenCompra);

        numero.Should().Be($"OC-{DateTime.UtcNow.Year}-000001");
    }

    [Fact]
    public async Task NumerosConsecutivos_SinSaltos()
    {
        await using var db = NewDb();
        var svc = new CorrelativoService(db);

        var emitidos = new List<string>();
        for (var i = 0; i < 5; i++)
            emitidos.Add(await svc.SiguienteAsync(1, CorrelativoSeries.OrdenCompra));

        var anio = DateTime.UtcNow.Year;
        emitidos.Should().Equal(
            $"OC-{anio}-000001", $"OC-{anio}-000002", $"OC-{anio}-000003",
            $"OC-{anio}-000004", $"OC-{anio}-000005");
    }

    [Fact]
    public async Task CadaEmpresaLlevaSuPropiaSecuencia()
    {
        await using var db = NewDb();
        var svc = new CorrelativoService(db);

        await svc.SiguienteAsync(1, CorrelativoSeries.OrdenCompra);
        await svc.SiguienteAsync(1, CorrelativoSeries.OrdenCompra);
        var otraEmpresa = await svc.SiguienteAsync(2, CorrelativoSeries.OrdenCompra);

        // La empresa 2 arranca en 1: no hereda el conteo de la 1.
        otraEmpresa.Should().EndWith("-000001");
    }

    [Fact]
    public async Task CadaSerieEsIndependiente()
    {
        await using var db = NewDb();
        var svc = new CorrelativoService(db);

        await svc.SiguienteAsync(1, CorrelativoSeries.OrdenCompra);
        var recepcion = await svc.SiguienteAsync(1, CorrelativoSeries.RecepcionCompra);

        recepcion.Should().Be($"RC-{DateTime.UtcNow.Year}-000001");
    }

    [Fact]
    public async Task NumerosNoSeRepiten_AunEmitiendoMuchos()
    {
        await using var db = NewDb();
        var svc = new CorrelativoService(db);

        var emitidos = new List<string>();
        for (var i = 0; i < 50; i++)
            emitidos.Add(await svc.SiguienteAsync(7, CorrelativoSeries.OrdenCompra));

        emitidos.Should().OnlyHaveUniqueItems();
        emitidos.Should().HaveCount(50);
    }

    [Fact]
    public async Task SerieLlevaElAnio_ParaQueReinicieCadaEnero()
    {
        await using var db = NewDb();
        var svc = new CorrelativoService(db);

        await svc.SiguienteAsync(1, CorrelativoSeries.OrdenCompra);

        var fila = await db.Correlativos.SingleAsync();
        fila.Serie.Should().Be($"OC-{DateTime.UtcNow.Year}");
    }

    [Fact]
    public async Task PrefijoVacio_Falla()
    {
        await using var db = NewDb();
        var svc = new CorrelativoService(db);

        var act = async () => await svc.SiguienteAsync(1, "  ");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
