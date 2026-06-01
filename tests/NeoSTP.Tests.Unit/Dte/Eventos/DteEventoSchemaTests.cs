using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte.Eventos;

/// <summary>
/// Sprint 15.1 — contrato del esquema de eventos DTE persistidos: defaults,
/// cascadas, multi-tenant y unicidades.
/// </summary>
public class DteEventoSchemaTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 20;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"evento-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0001-A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "0002-B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static DteEvento NewEvento(int empresaId, string tipo = TipoEventoCodigos.Invalidacion)
        => new()
        {
            EmpresaId = empresaId,
            TipoEventoCodigo = tipo,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            Version = 3,
            AmbienteCodigo = "PRUEBAS",
            FechaTransmision = DateTime.UtcNow,
        };

    private static DteDocumento NewDocumento(int empresaId, string numeroControl)
        => new()
        {
            EmpresaId = empresaId,
            TipoDteCodigo = "01",
            NumeroControl = numeroControl,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = "PROCESADO",
            CreatedAt = DateTime.UtcNow,
        };

    [Fact]
    public void DteEvento_New_HasBorradorEstadoAndAmbientePruebas()
    {
        var e = new DteEvento();
        e.EstadoCodigo.Should().Be(DteEventoEstadoCodigos.Borrador);
        e.AmbienteCodigo.Should().Be("PRUEBAS");
        e.Version.Should().Be(3);
        e.SelloRecibido.Should().BeNull();
    }

    [Fact]
    public async Task Evento_RoundTrip_PersistsHeaderAndJson()
    {
        await using var db = NewDb();
        var evento = NewEvento(EmpresaA, TipoEventoCodigos.Contingencia);
        db.DteEventos.Add(evento);
        await db.SaveChangesAsync();

        db.DteEventoJson.Add(new DteEventoJson
        {
            EventoId = evento.Id,
            JsonSinFirmar = "{\"identificacion\":{\"version\":3}}",
        });
        await db.SaveChangesAsync();

        var loaded = await db.DteEventos
            .Include(e => e.Json)
            .AsNoTracking()
            .SingleAsync();
        loaded.TipoEventoCodigo.Should().Be(TipoEventoCodigos.Contingencia);
        loaded.Json.Should().NotBeNull();
        loaded.Json!.JsonSinFirmar.Should().Contain("version");
    }

    [Fact]
    public async Task Cascade_DeletingEvento_RemovesJsonRespuestasAndRelacionados()
    {
        await using var db = NewDb();
        var doc = NewDocumento(EmpresaA, "DTE-01-M001P001-000000000000001");
        db.DteDocumentos.Add(doc);
        var evento = NewEvento(EmpresaA);
        db.DteEventos.Add(evento);
        await db.SaveChangesAsync();

        db.DteEventoJson.Add(new DteEventoJson { EventoId = evento.Id, JsonSinFirmar = "{}" });
        db.DteEventoRespuestas.Add(new DteEventoRespuestaHacienda { EventoId = evento.Id, RespuestaCrudaJson = "{}" });
        db.DteEventoDocumentosRelacionados.Add(new DteEventoDocumentoRelacionado
        {
            EventoId = evento.Id, DocumentoId = doc.Id,
            RolCodigo = DteEventoRolCodigos.Anulado,
            NumeroControlSnapshot = doc.NumeroControl,
        });
        await db.SaveChangesAsync();

        db.DteEventos.Remove(evento);
        await db.SaveChangesAsync();

        (await db.DteEventoJson.CountAsync()).Should().Be(0);
        (await db.DteEventoRespuestas.CountAsync()).Should().Be(0);
        (await db.DteEventoDocumentosRelacionados.CountAsync()).Should().Be(0);
        (await db.DteDocumentos.CountAsync()).Should().Be(1, "el DTE referenciado no se borra al eliminar el evento");
    }

    [Fact]
    public async Task Evento_RespetaScopeEmpresa()
    {
        await using var db = NewDb();
        db.DteEventos.Add(NewEvento(EmpresaA, TipoEventoCodigos.Invalidacion));
        db.DteEventos.Add(NewEvento(EmpresaB, TipoEventoCodigos.Invalidacion));
        await db.SaveChangesAsync();

        var soloA = await db.DteEventos.AsNoTracking().Where(e => e.EmpresaId == EmpresaA).ToListAsync();
        var soloB = await db.DteEventos.AsNoTracking().Where(e => e.EmpresaId == EmpresaB).ToListAsync();
        soloA.Should().HaveCount(1);
        soloB.Should().HaveCount(1);
    }

    [Fact]
    public async Task DocumentosRelacionados_AdmiteMultiplesRolesPorEvento_PeroUnoPorDte()
    {
        await using var db = NewDb();
        var d1 = NewDocumento(EmpresaA, "DTE-01-M001P001-000000000000010");
        var d2 = NewDocumento(EmpresaA, "DTE-01-M001P001-000000000000011");
        db.DteDocumentos.AddRange(d1, d2);
        var ev = NewEvento(EmpresaA, TipoEventoCodigos.Contingencia);
        db.DteEventos.Add(ev);
        await db.SaveChangesAsync();

        db.DteEventoDocumentosRelacionados.AddRange(
            new DteEventoDocumentoRelacionado { EventoId = ev.Id, DocumentoId = d1.Id, RolCodigo = DteEventoRolCodigos.LoteContingencia },
            new DteEventoDocumentoRelacionado { EventoId = ev.Id, DocumentoId = d2.Id, RolCodigo = DteEventoRolCodigos.LoteContingencia });
        await db.SaveChangesAsync();

        (await db.DteEventoDocumentosRelacionados.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task UltimaRespuesta_DefineEstadoActual_PorOrdenRecibidoAt()
    {
        await using var db = NewDb();
        var ev = NewEvento(EmpresaA);
        db.DteEventos.Add(ev);
        await db.SaveChangesAsync();

        var ahora = DateTime.UtcNow;
        db.DteEventoRespuestas.AddRange(
            new DteEventoRespuestaHacienda { EventoId = ev.Id, RespuestaCrudaJson = "{\"e\":\"R\"}", Estado = "RECHAZADO", CodigoMsg = "096", RecibidoAt = ahora.AddMinutes(-10) },
            new DteEventoRespuestaHacienda { EventoId = ev.Id, RespuestaCrudaJson = "{\"e\":\"P\"}", Estado = "PROCESADO", CodigoMsg = "001", SelloRecibido = "OK-1", RecibidoAt = ahora });
        await db.SaveChangesAsync();

        var ultima = await db.DteEventoRespuestas.AsNoTracking()
            .Where(r => r.EventoId == ev.Id)
            .OrderByDescending(r => r.RecibidoAt)
            .FirstAsync();
        ultima.Estado.Should().Be("PROCESADO");
        ultima.SelloRecibido.Should().Be("OK-1");
    }
}
