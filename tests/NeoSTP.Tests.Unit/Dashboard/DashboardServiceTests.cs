using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Dashboard;

/// <summary>
/// Pruebas unitarias de DashboardService usando EF Core InMemory.
/// Cada test obtiene un contexto con base de datos aislada para evitar interferencia.
/// </summary>
public class DashboardServiceTests
{
    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    private static NeoStpDbContext BuildContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new NeoStpDbContext(opts);
    }

    private static DteDocumento MakeDoc(int empresaId, string estado, DateTime? fechaEmision = null, decimal totalPagar = 100m)
        => new()
        {
            EmpresaId = empresaId,
            TipoDteCodigo = TipoDteCodigos.FacturaConsumidorFinal,
            NumeroControl = $"DTE-01-00010001-{Guid.NewGuid():N}",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = estado,
            FechaEmision = (fechaEmision ?? DateTime.UtcNow).Date,
            TotalPagar = totalPagar,
        };

    private static Empresa MakeEmpresa(int id, string razonSocial = "Empresa Test")
        => new()
        {
            Id = id,
            Nit = $"0614{id:D10}",
            RazonSocial = razonSocial,
            EstadoCodigo = EstadoCodes.Activo,
        };

    // ─────────────────────────────────────────────────────────────
    //  Tests empresa dashboard
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardEmpresa_SinDocumentos_DevuelveCerosYTendencia30Dias()
    {
        await using var db = BuildContext(nameof(GetDashboardEmpresa_SinDocumentos_DevuelveCerosYTendencia30Dias));
        var svc = new DashboardService(db);

        var result = await svc.GetDashboardEmpresaAsync(1);

        result.DteHoy.Should().Be(0);
        result.DteMes.Should().Be(0);
        result.TotalPagarMes.Should().Be(0m);
        result.Pendientes.Should().Be(0);
        result.Procesados.Should().Be(0);
        result.TendenciaDiaria.Should().HaveCount(30);
        result.TendenciaDiaria.All(x => x.Cantidad == 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetDashboardEmpresa_ConDocumentosHoy_ContaCorrectamente()
    {
        await using var db = BuildContext(nameof(GetDashboardEmpresa_ConDocumentosHoy_ContaCorrectamente));
        var hoy = DateTime.UtcNow.Date;

        db.DteDocumentos.AddRange(
            MakeDoc(1, DteEstadoCodigos.Procesado, hoy, 500m),
            MakeDoc(1, DteEstadoCodigos.Procesado, hoy, 300m),
            MakeDoc(1, DteEstadoCodigos.Borrador,  hoy)
        );
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardEmpresaAsync(1);

        result.DteHoy.Should().Be(3);
        result.DteMes.Should().Be(3);
        result.Procesados.Should().Be(2);
        result.Pendientes.Should().Be(1);   // Borrador = pendiente
        result.TotalPagarMes.Should().Be(800m);
    }

    [Fact]
    public async Task GetDashboardEmpresa_NoMezclaEmpresas()
    {
        await using var db = BuildContext(nameof(GetDashboardEmpresa_NoMezclaEmpresas));
        var hoy = DateTime.UtcNow.Date;

        // empresa 1: 3 docs, empresa 2: 5 docs
        db.DteDocumentos.AddRange(
            MakeDoc(1, DteEstadoCodigos.Procesado, hoy),
            MakeDoc(1, DteEstadoCodigos.Procesado, hoy),
            MakeDoc(1, DteEstadoCodigos.Rechazado, hoy),
            MakeDoc(2, DteEstadoCodigos.Procesado, hoy),
            MakeDoc(2, DteEstadoCodigos.Procesado, hoy),
            MakeDoc(2, DteEstadoCodigos.Procesado, hoy),
            MakeDoc(2, DteEstadoCodigos.Procesado, hoy),
            MakeDoc(2, DteEstadoCodigos.Procesado, hoy)
        );
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);

        var r1 = await svc.GetDashboardEmpresaAsync(1);
        r1.DteMes.Should().Be(3);
        r1.Rechazados.Should().Be(1);

        var r2 = await svc.GetDashboardEmpresaAsync(2);
        r2.DteMes.Should().Be(5);
        r2.Rechazados.Should().Be(0);
    }

    [Fact]
    public async Task GetDashboardEmpresa_DocumentosMesAnterior_NoContanEnMesActual()
    {
        await using var db = BuildContext(nameof(GetDashboardEmpresa_DocumentosMesAnterior_NoContanEnMesActual));
        var hoy = DateTime.UtcNow.Date;
        var mesAnterior = hoy.AddMonths(-1);

        db.DteDocumentos.AddRange(
            MakeDoc(1, DteEstadoCodigos.Procesado, hoy),           // este mes
            MakeDoc(1, DteEstadoCodigos.Procesado, mesAnterior),   // mes anterior
            MakeDoc(1, DteEstadoCodigos.Procesado, mesAnterior)    // mes anterior
        );
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardEmpresaAsync(1);

        result.DteMes.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardEmpresa_TendenciaDiaria_TieneLongitud30()
    {
        await using var db = BuildContext(nameof(GetDashboardEmpresa_TendenciaDiaria_TieneLongitud30));
        var hoy = DateTime.UtcNow.Date;

        // Docs en días distintos dentro de los últimos 30 días
        for (var i = 0; i < 5; i++)
            db.DteDocumentos.Add(MakeDoc(1, DteEstadoCodigos.Procesado, hoy.AddDays(-i * 5)));
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardEmpresaAsync(1);

        result.TendenciaDiaria.Should().HaveCount(30);
        result.TendenciaDiaria.Select(x => x.Fecha).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetDashboardEmpresa_PorEstado_AgrupaCorrectamente()
    {
        await using var db = BuildContext(nameof(GetDashboardEmpresa_PorEstado_AgrupaCorrectamente));
        var hoy = DateTime.UtcNow.Date;

        db.DteDocumentos.AddRange(
            MakeDoc(1, DteEstadoCodigos.Procesado,  hoy, 100m),
            MakeDoc(1, DteEstadoCodigos.Procesado,  hoy, 200m),
            MakeDoc(1, DteEstadoCodigos.Rechazado,  hoy, 50m),
            MakeDoc(1, DteEstadoCodigos.Borrador,   hoy, 75m)
        );
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardEmpresaAsync(1);

        var procesado = result.PorEstado.FirstOrDefault(e => e.Estado == "PROCESADO");
        procesado.Should().NotBeNull();
        procesado!.Cantidad.Should().Be(2);
        procesado.TotalPagar.Should().Be(300m);

        result.PorEstado.Should().HaveCount(3); // PROCESADO, RECHAZADO, BORRADOR
    }

    // ─────────────────────────────────────────────────────────────
    //  Tests SuperAdmin dashboard
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardSuperAdmin_SinDatos_DevuelveCeros()
    {
        await using var db = BuildContext(nameof(GetDashboardSuperAdmin_SinDatos_DevuelveCeros));
        var svc = new DashboardService(db);

        var result = await svc.GetDashboardSuperAdminAsync();

        result.EmpresasActivas.Should().Be(0);
        result.EmpresasTotal.Should().Be(0);
        result.DteTotalMes.Should().Be(0);
        result.FacturacionTotalMes.Should().Be(0m);
        result.AlertasPlanProximoVencer.Should().BeEmpty();
        result.TopEmpresasDteMes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardSuperAdmin_ContaEmpresasActivas()
    {
        await using var db = BuildContext(nameof(GetDashboardSuperAdmin_ContaEmpresasActivas));

        db.Empresas.AddRange(
            MakeEmpresa(1, "Activa 1"),
            MakeEmpresa(2, "Activa 2"),
            new Empresa { Id = 3, Nit = "0614000000003", RazonSocial = "Inactiva", EstadoCodigo = EstadoCodes.Inactivo }
        );
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardSuperAdminAsync();

        result.EmpresasActivas.Should().Be(2);
        result.EmpresasTotal.Should().Be(3);
    }

    [Fact]
    public async Task GetDashboardSuperAdmin_TopEmpresas_OrdenadoPorDte()
    {
        await using var db = BuildContext(nameof(GetDashboardSuperAdmin_TopEmpresas_OrdenadoPorDte));
        var hoy = DateTime.UtcNow.Date;

        db.Empresas.AddRange(MakeEmpresa(10, "Empresa A"), MakeEmpresa(20, "Empresa B"));

        // empresa 10 → 3 docs, empresa 20 → 7 docs
        for (var i = 0; i < 3; i++) db.DteDocumentos.Add(MakeDoc(10, DteEstadoCodigos.Procesado, hoy));
        for (var i = 0; i < 7; i++) db.DteDocumentos.Add(MakeDoc(20, DteEstadoCodigos.Procesado, hoy));
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardSuperAdminAsync();

        result.TopEmpresasDteMes.Should().HaveCount(2);
        result.TopEmpresasDteMes[0].EmpresaId.Should().Be(20);  // más DTE primero
        result.TopEmpresasDteMes[0].DteCount.Should().Be(7);
        result.TopEmpresasDteMes[1].DteCount.Should().Be(3);
    }

    [Fact]
    public async Task GetDashboardSuperAdmin_AlertasPlanVencer_SoloPlanesConFechaFin()
    {
        await using var db = BuildContext(nameof(GetDashboardSuperAdmin_AlertasPlanVencer_SoloPlanesConFechaFin));
        var hoy = DateTime.UtcNow.Date;

        db.Empresas.Add(MakeEmpresa(1));
        db.Planes.Add(new Plan { Id = 1, Codigo = "BASICO", Nombre = "Básico", PrecioMensual = 50m });
        db.EmpresaPlanes.AddRange(
            new EmpresaPlan
            {
                EmpresaId = 1, PlanId = 1, EstadoCodigo = EstadoCodes.Activo,
                FechaInicio = hoy.AddMonths(-6), FechaFin = hoy.AddDays(10),  // vence en 10 días
            },
            new EmpresaPlan
            {
                EmpresaId = 1, PlanId = 1, EstadoCodigo = EstadoCodes.Activo,
                FechaInicio = hoy.AddMonths(-1), FechaFin = null,              // sin fecha de fin
            }
        );
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardSuperAdminAsync();

        result.AlertasPlanProximoVencer.Should().HaveCount(1);
        result.AlertasPlanProximoVencer[0].DiasRestantes.Should().Be(10);
    }

    [Fact]
    public async Task GetDashboardSuperAdmin_AlertasPlanVencer_NoIncluyePlanesLejos()
    {
        await using var db = BuildContext(nameof(GetDashboardSuperAdmin_AlertasPlanVencer_NoIncluyePlanesLejos));
        var hoy = DateTime.UtcNow.Date;

        db.Empresas.Add(MakeEmpresa(1));
        db.Planes.Add(new Plan { Id = 1, Codigo = "PRO", Nombre = "Pro", PrecioMensual = 100m });
        db.EmpresaPlanes.Add(new EmpresaPlan
        {
            EmpresaId = 1, PlanId = 1, EstadoCodigo = EstadoCodes.Activo,
            FechaInicio = hoy.AddMonths(-1), FechaFin = hoy.AddDays(60),  // más de 30 días
        });
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardSuperAdminAsync();

        result.AlertasPlanProximoVencer.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardEmpresa_PorTipo_NombreCorrectoDeCodigo()
    {
        await using var db = BuildContext(nameof(GetDashboardEmpresa_PorTipo_NombreCorrectoDeCodigo));
        var hoy = DateTime.UtcNow.Date;

        var doc = MakeDoc(1, DteEstadoCodigos.Procesado, hoy);
        doc.TipoDteCodigo = TipoDteCodigos.ComprobanteCreditoFiscal;
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        var svc = new DashboardService(db);
        var result = await svc.GetDashboardEmpresaAsync(1);

        result.PorTipo.Should().HaveCount(1);
        result.PorTipo[0].TipoCodigo.Should().Be("03");
        result.PorTipo[0].TipoNombre.Should().Be("Comprobante Crédito Fiscal");
    }
}
