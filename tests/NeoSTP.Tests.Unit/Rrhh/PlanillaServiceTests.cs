using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Rrhh;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Rrhh;

/// <summary>NEORRHH Sprint 2 — PlanillaService: corrida quincenal, cierre→gasto y anulación.</summary>
public class PlanillaServiceTests
{
    private const int Empresa = 90;

    private static NeoStpDbContext NewDb(int empleados = 2)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"planilla-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "X", EstadoCodigo = "ACTIVA" });
        for (int i = 1; i <= empleados; i++)
        {
            var e = new Empleado
            {
                EmpresaId = Empresa, Codigo = $"E{i:000}", Nombres = "Emp", Apellidos = $"#{i}",
                NumeroDocumento = $"DOC{i}", FechaIngreso = new DateOnly(2026, 1, 1), EstadoCodigo = "ACTIVO",
            };
            e.Contratos.Add(new ContratoLaboral { EmpresaId = Empresa, SalarioMensual = 1000m, PeriodicidadPago = "QUINCENAL", FechaInicio = new DateOnly(2026, 1, 1), EstadoCodigo = ContratoEstados.Vigente });
            db.Empleados.Add(e);
        }
        db.SaveChanges();
        return db;
    }

    private static (PlanillaService svc, IProfitService profit) NewSvc(NeoStpDbContext db)
    {
        var profit = Substitute.For<IProfitService>();
        profit.CreateGastoAsync(Arg.Any<int>(), Arg.Any<CreateProfitGastoRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfitGastoDto>.Ok(new ProfitGastoDto { Id = 555 }));
        profit.InactivarGastoAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        var svc = new PlanillaService(db, Substitute.For<IAuditoriaService>(), profit, Options.Create(new NominaOptions()));
        return (svc, profit);
    }

    private static CrearPlanillaRequest Q1() => new() { Anio = 2026, Mes = 6, Quincena = 1 };

    [Fact]
    public async Task Crear_QuincenaCalculaTodosLosActivos_ConTotales()
    {
        var db = NewDb(empleados: 2);
        var (svc, _) = NewSvc(db);

        var r = await svc.CrearAsync(Empresa, Q1(), "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EstadoCodigo.Should().Be("CALCULADA");
        r.Value.Detalles.Should().HaveCount(2);
        r.Value.FechaInicio.Should().Be(new DateOnly(2026, 6, 1));
        r.Value.FechaFin.Should().Be(new DateOnly(2026, 6, 15));
        // 2 empleados de 1000 mensual → quincena neto 418.52 c/u
        r.Value.TotalNeto.Should().Be(837.04m);
        r.Value.Detalles[0].SalarioNeto.Should().Be(418.52m);
    }

    [Fact]
    public async Task Crear_Duplicado_Falla()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);
        await svc.CrearAsync(Empresa, Q1(), "tester");

        var r = await svc.CrearAsync(Empresa, Q1(), "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("DUPLICATE");
    }

    [Fact]
    public async Task Cerrar_GeneraGastoPlanilla_YDejaCerrada()
    {
        var db = NewDb();
        var (svc, profit) = NewSvc(db);
        var creada = await svc.CrearAsync(Empresa, Q1(), "tester");

        var r = await svc.CerrarAsync(Empresa, creada.Value!.Id, "tester");

        r.IsSuccess.Should().BeTrue();
        await profit.Received(1).CreateGastoAsync(Empresa, Arg.Is<CreateProfitGastoRequest>(g => g.Categoria == "PLANILLA"), "tester", Arg.Any<CancellationToken>());
        var p = await db.PlanillaPeriodos.FirstAsync();
        p.EstadoCodigo.Should().Be("CERRADA");
        p.ProfitGastoId.Should().Be(555);
    }

    [Fact]
    public async Task Recalcular_Cerrada_InvalidState()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);
        var creada = await svc.CrearAsync(Empresa, Q1(), "tester");
        await svc.CerrarAsync(Empresa, creada.Value!.Id, "tester");

        var r = await svc.RecalcularAsync(Empresa, creada.Value.Id, "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task GetRecibo_DevuelveDatosDelEmpleado()
    {
        var db = NewDb(empleados: 1);
        var (svc, _) = NewSvc(db);
        var creada = await svc.CrearAsync(Empresa, Q1(), "tester");
        var empleadoId = (await db.Empleados.FirstAsync()).Id;

        var r = await svc.GetReciboAsync(Empresa, creada.Value!.Id, empleadoId);

        r.IsSuccess.Should().BeTrue();
        r.Value!.EmpleadoCodigo.Should().Be("E001");
        r.Value.SalarioNeto.Should().Be(418.52m);
        r.Value.PeriodoEtiqueta.Should().Contain("Q1");
    }

    [Fact]
    public async Task GetExportRows_IncluyeDatosDeSeguridadSocial()
    {
        var db = NewDb(empleados: 1);
        var e = await db.Empleados.FirstAsync();
        e.IsssNumero = "ISSS-1"; e.AfpInstitucion = "Crecer"; e.AfpNumero = "AFP-1";
        await db.SaveChangesAsync();
        var (svc, _) = NewSvc(db);
        var creada = await svc.CrearAsync(Empresa, Q1(), "tester");

        var r = await svc.GetExportRowsAsync(Empresa, creada.Value!.Id);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().ContainSingle();
        r.Value[0].IsssNumero.Should().Be("ISSS-1");
        r.Value[0].AfpInstitucion.Should().Be("Crecer");
    }

    [Fact]
    public async Task Anular_Cerrada_RevierteGasto()
    {
        var db = NewDb();
        var (svc, profit) = NewSvc(db);
        var creada = await svc.CrearAsync(Empresa, Q1(), "tester");
        await svc.CerrarAsync(Empresa, creada.Value!.Id, "tester");

        var r = await svc.AnularAsync(Empresa, creada.Value.Id, "tester");

        r.IsSuccess.Should().BeTrue();
        await profit.Received(1).InactivarGastoAsync(Empresa, 555, "tester", Arg.Any<CancellationToken>());
        (await db.PlanillaPeriodos.FirstAsync()).EstadoCodigo.Should().Be("ANULADA");
    }
}
