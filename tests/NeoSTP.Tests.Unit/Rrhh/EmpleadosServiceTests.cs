using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Rrhh;

/// <summary>NEORRHH — EmpleadosService: alta con contrato vigente, preview de nómina y soft-delete.</summary>
public class EmpleadosServiceTests
{
    private const int Empresa = 80;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"rrhh-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "X", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static EmpleadosService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>(), Options.Create(new NominaOptions()));

    private static CreateEmpleadoRequest Req(string codigo = "E001", decimal salario = 1000m) => new()
    {
        Codigo = codigo, Nombres = "Juan", Apellidos = "Pérez", NumeroDocumento = "01234567-8",
        SalarioMensual = salario, PeriodicidadPago = "QUINCENAL", TipoContrato = "INDEFINIDO",
    };

    [Fact]
    public async Task Create_GeneraContratoVigente_YPreviewDeNomina()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.CreateAsync(Empresa, Req(salario: 1000m), "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.SalarioMensual.Should().Be(1000m);
        r.Value.NominaPreview.Should().NotBeNull();
        r.Value.NominaPreview!.IsssEmpleado.Should().Be(30.00m);
        r.Value.NominaPreview.SalarioNeto.Should().Be(837.05m); // ver NominaCalculatorTests

        (await db.ContratosLaborales.CountAsync(c => c.EstadoCodigo == "VIGENTE")).Should().Be(1);
    }

    [Fact]
    public async Task Create_CodigoDuplicado_Falla()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CreateAsync(Empresa, Req("E001"), "tester");

        var r = await svc.CreateAsync(Empresa, Req("E001"), "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("DUPLICATE");
    }

    [Fact]
    public async Task Update_CambiaSalarioDelContratoVigente()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var created = await svc.CreateAsync(Empresa, Req(salario: 1000m), "tester");

        var upd = new UpdateEmpleadoRequest { Codigo = "E001", Nombres = "Juan", Apellidos = "Pérez", NumeroDocumento = "01234567-8", SalarioMensual = 1500m, PeriodicidadPago = "MENSUAL", TipoContrato = "INDEFINIDO" };
        var r = await svc.UpdateAsync(Empresa, created.Value!.Id, upd, "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.SalarioMensual.Should().Be(1500m);
        r.Value.PeriodicidadPago.Should().Be("MENSUAL");
        (await db.ContratosLaborales.CountAsync(c => c.EstadoCodigo == "VIGENTE")).Should().Be(1);
    }

    [Fact]
    public async Task Inactivar_FinalizaContratoYFijaEgreso()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var created = await svc.CreateAsync(Empresa, Req(), "tester");

        var r = await svc.InactivarAsync(Empresa, created.Value!.Id, "tester");

        r.IsSuccess.Should().BeTrue();
        var e = await db.Empleados.FirstAsync();
        e.EstadoCodigo.Should().Be("INACTIVO");
        e.FechaEgreso.Should().NotBeNull();
        (await db.ContratosLaborales.CountAsync(c => c.EstadoCodigo == "VIGENTE")).Should().Be(0);
    }

    [Fact]
    public async Task Get_DeOtraEmpresa_NotFound()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var created = await svc.CreateAsync(Empresa, Req(), "tester");

        var r = await svc.GetAsync(999, created.Value!.Id);

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("EMPLEADO_NOT_FOUND");
    }
}
