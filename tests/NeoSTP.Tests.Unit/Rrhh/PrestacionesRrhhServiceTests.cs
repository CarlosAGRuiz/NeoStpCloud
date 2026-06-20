using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Rrhh;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Rrhh;

public class PrestacionesRrhhServiceTests
{
    private const int EmpresaId = 30;

    private static (NeoStpDbContext Db, PrestacionesRrhhService Service, Empleado Empleado) NewScope()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"prestaciones-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.AddRange(
            new Empresa { Id = EmpresaId, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" },
            new Empresa { Id = 31, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        var empleado = new Empleado
        {
            EmpresaId = EmpresaId, Codigo = "E-1", Nombres = "Ana", Apellidos = "Demo",
            NumeroDocumento = "DOC-1", FechaIngreso = new DateOnly(2024, 1, 1), EstadoCodigo = "ACTIVO",
            Contratos =
            {
                new ContratoLaboral
                {
                    EmpresaId = EmpresaId, SalarioMensual = 900m, FechaInicio = new DateOnly(2024, 1, 1),
                    PeriodicidadPago = "QUINCENAL", EstadoCodigo = ContratoEstados.Vigente,
                }
            }
        };
        db.Empleados.Add(empleado);
        db.SaveChanges();
        return (db, new PrestacionesRrhhService(db, Substitute.For<IAuditoriaService>()), empleado);
    }

    [Fact]
    public async Task ResumenVacacion_DevengaYRestaDiasAprobados()
    {
        var (db, service, empleado) = NewScope();
        db.SolicitudesVacacion.Add(new SolicitudVacacion
        {
            EmpresaId = EmpresaId, EmpleadoId = empleado.Id, FechaInicio = new DateOnly(2025, 6, 1),
            FechaFin = new DateOnly(2025, 6, 5), Dias = 5, EstadoCodigo = VacacionEstados.Aprobada,
        });
        await db.SaveChangesAsync();

        var result = await service.GetVacacionResumenAsync(EmpresaId, empleado.Id, new DateOnly(2026, 1, 1));

        result.Value!.DiasDevengados.Should().Be(30);
        result.Value.DiasDisponibles.Should().Be(25);
    }

    [Fact]
    public async Task SolicitarVacacion_RechazaTraslape()
    {
        var (db, service, empleado) = NewScope();
        db.SolicitudesVacacion.Add(new SolicitudVacacion
        {
            EmpresaId = EmpresaId, EmpleadoId = empleado.Id, FechaInicio = new DateOnly(2026, 7, 1),
            FechaFin = new DateOnly(2026, 7, 5), Dias = 5, EstadoCodigo = VacacionEstados.Solicitada,
        });
        await db.SaveChangesAsync();

        var result = await service.SolicitarVacacionAsync(EmpresaId, new CrearSolicitudVacacionRequest
        {
            EmpleadoId = empleado.Id, FechaInicio = new DateOnly(2026, 7, 5), FechaFin = new DateOnly(2026, 7, 7)
        }, "tester");

        result.ErrorCode.Should().Be("VACACION_TRASLAPE");
    }

    [Fact]
    public async Task AprobarVacacion_CalculaPrima()
    {
        var (_, service, empleado) = NewScope();
        var creada = await service.SolicitarVacacionAsync(EmpresaId, new CrearSolicitudVacacionRequest
        {
            EmpleadoId = empleado.Id, FechaInicio = new DateOnly(2026, 7, 1), FechaFin = new DateOnly(2026, 7, 5)
        }, "tester");

        var result = await service.AprobarVacacionAsync(
            EmpresaId, creada.Value!.Id, new ResolverSolicitudVacacionRequest(), "supervisor");

        result.IsSuccess.Should().BeTrue();
        result.Value!.PrimaMonto.Should().Be(45m);
        result.Value.EstadoCodigo.Should().Be(VacacionEstados.Aprobada);
    }

    [Fact]
    public async Task OperacionDeOtraEmpresa_NoExponeEmpleado()
    {
        var (_, service, empleado) = NewScope();

        var result = await service.GetVacacionResumenAsync(31, empleado.Id);

        result.ErrorCode.Should().Be("EMPLEADO_NOT_FOUND");
    }

    [Fact]
    public async Task CalcularAguinaldo_EsIdempotente_YNoSobrescribeAprobado()
    {
        var (db, service, _) = NewScope();
        var primero = await service.CalcularAguinaldosAsync(EmpresaId, 2026, "tester");
        await service.AprobarAguinaldosAsync(EmpresaId, 2026, "tester");
        var segundo = await service.CalcularAguinaldosAsync(EmpresaId, 2026, "tester");

        primero.Value.Should().ContainSingle();
        segundo.Value.Should().ContainSingle(x => x.EstadoCodigo == AguinaldoEstados.Aprobado);
        (await db.AguinaldosCalculados.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdatePolitica_RechazaTramosInvertidos()
    {
        var (_, service, _) = NewScope();

        var result = await service.UpdatePoliticaAsync(EmpresaId, new UpdatePoliticaPrestacionesRequest
        {
            AguinaldoAniosTramoMedio = 10, AguinaldoAniosTramoLargo = 3
        }, "tester");

        result.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task UpdatePolitica_RechazaValoresFueraDeRangoAunqueNoHayaModelBinding()
    {
        var (_, service, _) = NewScope();

        var result = await service.UpdatePoliticaAsync(EmpresaId, new UpdatePoliticaPrestacionesRequest
        {
            MesesParaVacacion = 0
        }, "tester");

        result.ErrorCode.Should().Be("VALIDATION");
    }
}
