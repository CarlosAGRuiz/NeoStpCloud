using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Certificacion.Dtos;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte.Certificacion;

/// <summary>
/// Sprint 15.5 — MarcarCompletadoPorEventoAsync: asociación de eventos a
/// matrices de eventos (INVALIDACION/CONTINGENCIA/RETORNO/OPERACIONES_ESPECIALES)
/// con validación de tipo cruzado y semántica de promoción.
/// </summary>
public class CertificacionEventoServiceTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 20;

    private static (CertificacionDteService svc, NeoStpDbContext db) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cert-evento-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Database.EnsureCreated();
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0001-A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "0002-B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        seed?.Invoke(db);
        db.SaveChanges();
        var audit = Substitute.For<IAuditoriaService>();
        return (new CertificacionDteService(db, audit), db);
    }

    private static int EscenarioInvalidacion1(NeoStpDbContext db)
        => db.CertificacionEscenarios.AsNoTracking().Single(e => e.Codigo == "INV-01").Id;

    private static int EscenarioFactura1(NeoStpDbContext db)
        => db.CertificacionEscenarios.AsNoTracking().Single(e => e.Codigo == "FACTURA-01").Id;

    private static DteEvento NewEvento(int empresaId, string tipo, string estado, string? sello = null)
        => new()
        {
            EmpresaId = empresaId,
            TipoEventoCodigo = tipo,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            Version = 2,
            AmbienteCodigo = "PRUEBAS",
            FechaTransmision = DateTime.UtcNow,
            EstadoCodigo = estado,
            SelloRecibido = sello,
            FinalizadoAt = estado == DteEventoEstadoCodigos.Procesado ? DateTime.UtcNow : null,
        };

    [Fact]
    public async Task MarcarCompletado_EventoProcesadoConSello_PromueveACompletado()
    {
        var (svc, db) = Build();
        var ev = NewEvento(EmpresaA, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, "SELLO-INV");
        db.DteEventos.Add(ev);
        await db.SaveChangesAsync();
        var escId = EscenarioInvalidacion1(db);

        var result = await svc.MarcarCompletadoPorEventoAsync(ev.Id,
            new MarcarCompletadoRequest { EscenarioId = escId }, EmpresaA, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(CertificacionEstadoCodigos.Completado);
        result.Value.SelloRecibido.Should().Be("SELLO-INV");
        result.Value.NumeroControl.Should().Be(ev.CodigoGeneracion);
    }

    [Fact]
    public async Task MarcarCompletado_TipoEventoNoCoincideConMatriz_DevuelveMismatch()
    {
        var (svc, db) = Build();
        // Evento de Contingencia + escenario de Invalidación → mismatch
        var ev = NewEvento(EmpresaA, TipoEventoCodigos.Contingencia, DteEventoEstadoCodigos.Procesado, "S");
        db.DteEventos.Add(ev);
        await db.SaveChangesAsync();
        var escId = EscenarioInvalidacion1(db);

        var result = await svc.MarcarCompletadoPorEventoAsync(ev.Id,
            new MarcarCompletadoRequest { EscenarioId = escId }, EmpresaA, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CERT_TIPO_MISMATCH");
    }

    [Fact]
    public async Task MarcarCompletado_EscenarioDeFactura_RechazaEvento()
    {
        var (svc, db) = Build();
        var ev = NewEvento(EmpresaA, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, "S");
        db.DteEventos.Add(ev);
        await db.SaveChangesAsync();
        var escFactura = EscenarioFactura1(db);

        var result = await svc.MarcarCompletadoPorEventoAsync(ev.Id,
            new MarcarCompletadoRequest { EscenarioId = escFactura }, EmpresaA, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CERT_TIPO_MISMATCH");
    }

    [Fact]
    public async Task MarcarCompletado_RespetaScopeEmpresa()
    {
        var (svc, db) = Build();
        var ev = NewEvento(EmpresaB, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, "S");
        db.DteEventos.Add(ev);
        await db.SaveChangesAsync();
        var escId = EscenarioInvalidacion1(db);

        var result = await svc.MarcarCompletadoPorEventoAsync(ev.Id,
            new MarcarCompletadoRequest { EscenarioId = escId }, EmpresaA, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("EVENTO_NOT_FOUND");
    }

    [Fact]
    public async Task MarcarCompletado_EventoRechazado_DejaPruebaEnError()
    {
        var (svc, db) = Build();
        var ev = NewEvento(EmpresaA, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Rechazado);
        db.DteEventos.Add(ev);
        await db.SaveChangesAsync();
        var escId = EscenarioInvalidacion1(db);

        var result = await svc.MarcarCompletadoPorEventoAsync(ev.Id,
            new MarcarCompletadoRequest { EscenarioId = escId }, EmpresaA, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(CertificacionEstadoCodigos.Error);
        result.Value.SelloRecibido.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GetResumen_CuentaEventosCompletados_EnSumaGlobal()
    {
        var (svc, db) = Build();
        var ev = NewEvento(EmpresaA, TipoEventoCodigos.Retorno, DteEventoEstadoCodigos.Procesado, "SELLO-R");
        db.DteEventos.Add(ev);
        await db.SaveChangesAsync();
        var escRetorno = db.CertificacionEscenarios.AsNoTracking().First(e => e.Codigo == "RETOR-01").Id;
        await svc.MarcarCompletadoPorEventoAsync(ev.Id,
            new MarcarCompletadoRequest { EscenarioId = escRetorno }, EmpresaA, "tester");

        var resumen = await svc.GetResumenAsync(EmpresaA);

        resumen.Value!.Completados.Should().Be(1);
    }
}
