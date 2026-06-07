using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using Xunit;

namespace NeoSTP.Tests.Unit.Auditoria;

/// <summary>M3.4 — consulta de auditoría: filtros, paginación, export y aislamiento por empresa.</summary>
public class AuditoriaQueryServiceTests
{
    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    private static Domain.Core.Auditoria.Auditoria Row(int? empresaId, string modulo, string accion,
        string resultado = "OK", string? username = "user", DateTime? when = null) => new()
    {
        EmpresaId = empresaId, Modulo = modulo, Accion = accion, Resultado = resultado,
        Username = username, CreatedAt = when ?? DateTime.UtcNow,
    };

    private static async Task<NeoStpDbContext> Seed()
    {
        var db = NewDb();
        db.Auditoria.AddRange(
            Row(1, "DTE", "EMITIR", when: new DateTime(2026, 5, 1)),
            Row(1, "COBROS", "REGISTRAR_PAGO", when: new DateTime(2026, 5, 10)),
            Row(1, "DTE", "ANULAR", resultado: "ERROR", username: "admin", when: new DateTime(2026, 5, 20)),
            Row(2, "DTE", "EMITIR", when: new DateTime(2026, 5, 15)));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task List_FiltraPorEmpresa_YOrdenaDescendente()
    {
        var db = await Seed();
        var svc = new AuditoriaQueryService(db);

        var r = await svc.ListAsync(new AuditoriaQuery { EmpresaId = 1 });

        r.Total.Should().Be(3);
        r.Items.First().Accion.Should().Be("ANULAR"); // el más reciente (id mayor)
    }

    [Fact]
    public async Task List_SinEmpresa_DevuelveTodas()
    {
        var db = await Seed();
        var svc = new AuditoriaQueryService(db);

        var r = await svc.ListAsync(new AuditoriaQuery());

        r.Total.Should().Be(4);
    }

    [Fact]
    public async Task List_FiltraPorModuloResultadoYUsuario()
    {
        var db = await Seed();
        var svc = new AuditoriaQueryService(db);

        (await svc.ListAsync(new AuditoriaQuery { Modulo = "DTE" })).Total.Should().Be(3);
        (await svc.ListAsync(new AuditoriaQuery { Resultado = "ERROR" })).Total.Should().Be(1);
        (await svc.ListAsync(new AuditoriaQuery { Username = "admin" })).Total.Should().Be(1);
    }

    [Fact]
    public async Task List_FiltraPorRangoDeFechas()
    {
        var db = await Seed();
        var svc = new AuditoriaQueryService(db);

        var r = await svc.ListAsync(new AuditoriaQuery
        {
            Desde = new DateTime(2026, 5, 5),
            Hasta = new DateTime(2026, 5, 16),
        });

        r.Total.Should().Be(2); // 10 (empresa1) y 15 (empresa2)
    }

    [Fact]
    public async Task Export_RespetaFiltro()
    {
        var db = await Seed();
        var svc = new AuditoriaQueryService(db);

        var filas = await svc.ExportAsync(new AuditoriaQuery { EmpresaId = 1, Modulo = "DTE" });

        filas.Should().HaveCount(2);
        filas.Should().OnlyContain(a => a.Modulo == "DTE" && a.EmpresaId == 1);
    }

    [Fact]
    public async Task GetModulos_DistinctYAcotadoPorEmpresa()
    {
        var db = await Seed();
        var svc = new AuditoriaQueryService(db);

        (await svc.GetModulosAsync(null)).Should().BeEquivalentTo(new[] { "COBROS", "DTE" });
        (await svc.GetModulosAsync(2)).Should().BeEquivalentTo(new[] { "DTE" });
    }
}
