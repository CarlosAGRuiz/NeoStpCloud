using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Agenda;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Domain.Core.Rrhh;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Agenda;

/// <summary>Entrega 6 — NEOAGENDA: citas, traslapes por empleado y comisiones.</summary>
public class AgendaServiceTests
{
    private const int Empresa = 100;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"agenda-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "S", RazonSocial = "Salón", EstadoCodigo = "ACTIVA" });
        db.Empleados.Add(new Empleado
        {
            Id = 1, EmpresaId = Empresa, Codigo = "E1", Nombres = "Ana", Apellidos = "López",
            TipoDocumento = "DUI", NumeroDocumento = "1-1", FechaIngreso = new DateOnly(2025, 1, 1),
            ComisionPorcentaje = 10m,
        });
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = Empresa, CodigoInterno = "CORTE", Nombre = "Corte de cabello",
            PrecioUnitario = 15m, TipoItem = "SERVICIO", EstadoCodigo = "ACTIVO", UnidadMedidaCodigo = "59",
        });
        db.SaveChanges();
        return db;
    }

    private static AgendaService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static CrearCitaRequest Cita(DateTime inicio, int duracion = 30, int? empleadoId = 1) => new()
    {
        ClienteNombre = "Marta", ServicioProductoId = 1, EmpleadoId = empleadoId,
        FechaInicio = inicio, DuracionMinutos = duracion,
    };

    private static readonly DateTime Base = new(2026, 8, 3, 9, 0, 0); // lunes 9:00

    [Fact]
    public async Task Crear_CongelaPrecioDelServicio()
    {
        var svc = NewSvc(NewDb());

        var r = await svc.CrearAsync(Empresa, Cita(Base), "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.Precio.Should().Be(15m);
        r.Value.ServicioNombre.Should().Be("Corte de cabello");
        r.Value.EmpleadoNombre.Should().Be("Ana López");
        r.Value.FechaFin.Should().Be(Base.AddMinutes(30));
    }

    [Fact]
    public async Task Crear_Traslape_MismoEmpleado_Falla()
    {
        var svc = NewSvc(NewDb());
        (await svc.CrearAsync(Empresa, Cita(Base, 60), "t")).IsSuccess.Should().BeTrue();

        (await svc.CrearAsync(Empresa, Cita(Base.AddMinutes(30)), "t")).ErrorCode.Should().Be("CITA_TRASLAPADA");
        // Franja siguiente (10:00) sí es válida.
        (await svc.CrearAsync(Empresa, Cita(Base.AddMinutes(60)), "t")).IsSuccess.Should().BeTrue();
        // Sin empleado no valida traslape.
        (await svc.CrearAsync(Empresa, Cita(Base.AddMinutes(30), empleadoId: null), "t")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelada_LiberaLaFranja()
    {
        var db = NewDb(); var svc = NewSvc(db);
        var primera = await svc.CrearAsync(Empresa, Cita(Base), "t");
        await svc.CambiarEstadoAsync(Empresa, primera.Value!.Id, "CANCELADA", "t");

        (await svc.CrearAsync(Empresa, Cita(Base), "t")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Reprogramar_ValidaTraslapeYEstado()
    {
        var svc = NewSvc(NewDb());
        var a = await svc.CrearAsync(Empresa, Cita(Base, 30), "t");
        var b = await svc.CrearAsync(Empresa, Cita(Base.AddHours(2), 30), "t");

        (await svc.ReprogramarAsync(Empresa, b.Value!.Id, Base.AddMinutes(15), null, "t"))
            .ErrorCode.Should().Be("CITA_TRASLAPADA");
        (await svc.ReprogramarAsync(Empresa, b.Value.Id, Base.AddHours(3), 45, "t")).IsSuccess.Should().BeTrue();

        await svc.CambiarEstadoAsync(Empresa, a.Value!.Id, "COMPLETADA", "t");
        (await svc.ReprogramarAsync(Empresa, a.Value.Id, Base.AddHours(5), null, "t"))
            .ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Comisiones_SoloCitasCompletadasEnRango()
    {
        var svc = NewSvc(NewDb());
        var c1 = await svc.CrearAsync(Empresa, Cita(Base), "t");                 // completada
        var c2 = await svc.CrearAsync(Empresa, Cita(Base.AddHours(1)), "t");    // completada
        var c3 = await svc.CrearAsync(Empresa, Cita(Base.AddHours(2)), "t");    // cancelada (no cuenta)
        await svc.CambiarEstadoAsync(Empresa, c1.Value!.Id, "COMPLETADA", "t");
        await svc.CambiarEstadoAsync(Empresa, c2.Value!.Id, "COMPLETADA", "t");
        await svc.CambiarEstadoAsync(Empresa, c3.Value!.Id, "CANCELADA", "t");

        var r = await svc.ComisionesAsync(Empresa,
            DateOnly.FromDateTime(Base), DateOnly.FromDateTime(Base));

        var ana = r.Value!.Single();
        ana.EmpleadoNombre.Should().Be("Ana López");
        ana.CitasCompletadas.Should().Be(2);
        ana.TotalServicios.Should().Be(30m);
        ana.MontoComision.Should().Be(3m); // 10% de 30

        // Fuera de rango no devuelve nada.
        var vacio = await svc.ComisionesAsync(Empresa,
            DateOnly.FromDateTime(Base).AddDays(5), DateOnly.FromDateTime(Base).AddDays(6));
        vacio.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Crear_SinClienteNiServicio_Validation()
    {
        var svc = NewSvc(NewDb());

        (await svc.CrearAsync(Empresa, new CrearCitaRequest { ServicioNombre = "Corte", FechaInicio = Base }, "t"))
            .ErrorCode.Should().Be("VALIDATION"); // sin cliente
        (await svc.CrearAsync(Empresa, new CrearCitaRequest { ClienteNombre = "X", FechaInicio = Base }, "t"))
            .ErrorCode.Should().Be("VALIDATION"); // sin servicio
        (await svc.CrearAsync(Empresa, new CrearCitaRequest { ClienteNombre = "X", ServicioNombre = "Y", FechaInicio = Base, DuracionMinutos = 2 }, "t"))
            .ErrorCode.Should().Be("VALIDATION"); // duración inválida
    }
}
