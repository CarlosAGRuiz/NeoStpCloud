using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>V2.5-S5 — purga de auditoría: respeta retención, borra por lotes y nunca baja de 30 días.</summary>
public class LimpiezaAuditoriaServiceTests
{
    private static NeoStpDbContext NewDb()
        => new(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"limpieza-{Guid.NewGuid()}").Options);

    private static void AddEvento(NeoStpDbContext db, int diasAtras)
        => db.Auditoria.Add(new NeoSTP.Domain.Core.Auditoria.Auditoria
        {
            Modulo = "TEST", Accion = "X", CreatedAt = DateTime.UtcNow.AddDays(-diasAtras),
        });

    [Fact]
    public async Task Purgar_BorraSoloLoMasViejoQueLaRetencion()
    {
        var db = NewDb();
        AddEvento(db, diasAtras: 400);
        AddEvento(db, diasAtras: 100);
        AddEvento(db, diasAtras: 1);
        db.SaveChanges();

        var purgados = await new LimpiezaAuditoriaService(db, NullLogger<LimpiezaAuditoriaService>.Instance)
            .PurgarAsync(retencionDias: 365);

        purgados.Should().Be(1);
        db.Auditoria.Count().Should().Be(2);
    }

    [Fact]
    public async Task Purgar_EnLotes_BorraTodoLoVencido()
    {
        var db = NewDb();
        for (var i = 0; i < 7; i++) AddEvento(db, diasAtras: 500);
        db.SaveChanges();

        var purgados = await new LimpiezaAuditoriaService(db, NullLogger<LimpiezaAuditoriaService>.Instance)
            .PurgarAsync(retencionDias: 365, batchSize: 100);

        purgados.Should().Be(7);
        db.Auditoria.Should().BeEmpty();
    }

    [Fact]
    public async Task Purgar_RetencionMenorA30Dias_SeElevaA30()
    {
        var db = NewDb();
        AddEvento(db, diasAtras: 10); // reciente: jamás debe purgarse con retención "1"
        AddEvento(db, diasAtras: 45);
        db.SaveChanges();

        var purgados = await new LimpiezaAuditoriaService(db, NullLogger<LimpiezaAuditoriaService>.Instance)
            .PurgarAsync(retencionDias: 1);

        purgados.Should().Be(1);
        db.Auditoria.Single().CreatedAt.Should().BeAfter(DateTime.UtcNow.AddDays(-30));
    }
}
