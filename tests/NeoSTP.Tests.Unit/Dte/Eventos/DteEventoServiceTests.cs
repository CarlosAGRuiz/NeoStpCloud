using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte.Eventos;

/// <summary>
/// Sprint 15.2 — DteEventoService: consulta (lista/detalle/json), multi-tenant
/// y delegación de creación a IDteDocumentosService.
/// </summary>
public class DteEventoServiceTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 20;

    private static (DteEventoService svc, NeoStpDbContext db, IDteDocumentosService docs) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"evento-svc-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0001-A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "0002-B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        seed?.Invoke(db);
        db.SaveChanges();
        var docs = Substitute.For<IDteDocumentosService>();
        return (new DteEventoService(db, docs), db, docs);
    }

    private static DteEvento Evento(int empresaId, string tipo, string estado, string? sello = null)
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
        };

    [Fact]
    public async Task GetList_FiltersByEmpresa()
    {
        var (svc, db, _) = Build(d =>
        {
            d.DteEventos.Add(Evento(EmpresaA, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, "S1"));
            d.DteEventos.Add(Evento(EmpresaA, TipoEventoCodigos.Contingencia, DteEventoEstadoCodigos.Enviado));
            d.DteEventos.Add(Evento(EmpresaB, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, "S2"));
        });

        var a = await svc.GetListAsync(EmpresaA);
        var b = await svc.GetListAsync(EmpresaB);

        a.Value!.Should().HaveCount(2);
        b.Value!.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetList_FiltersByTipoAndEstado()
    {
        var (svc, _, _) = Build(d =>
        {
            d.DteEventos.Add(Evento(EmpresaA, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, "S1"));
            d.DteEventos.Add(Evento(EmpresaA, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Rechazado));
            d.DteEventos.Add(Evento(EmpresaA, TipoEventoCodigos.Contingencia, DteEventoEstadoCodigos.Procesado, "S2"));
        });

        var soloInval = await svc.GetListAsync(EmpresaA, tipoEvento: "invalidacion");
        var soloProcesados = await svc.GetListAsync(EmpresaA, estado: "PROCESADO");

        soloInval.Value!.Should().HaveCount(2);
        soloProcesados.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_ReturnsDetailWithRelacionadosAndRespuestas()
    {
        DteEvento? ev = null;
        var (svc, db, _) = Build(d =>
        {
            var doc = new DteDocumento
            {
                EmpresaId = EmpresaA, TipoDteCodigo = "01",
                NumeroControl = "DTE-01-M001P001-000000000000001",
                CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
                EstadoCodigo = "PROCESADO", CreatedAt = DateTime.UtcNow,
            };
            d.DteDocumentos.Add(doc);
            ev = Evento(EmpresaA, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, "SELLO-X");
            ev.Json = new DteEventoJson { JsonSinFirmar = "{\"x\":1}", JwsFirmado = "h.p.s" };
            d.DteEventos.Add(ev);
            d.SaveChanges();
            d.DteEventoRespuestas.Add(new DteEventoRespuestaHacienda
            {
                EventoId = ev.Id, RespuestaCrudaJson = "{\"estado\":\"PROCESADO\"}",
                Estado = "PROCESADO", CodigoMsg = "001", SelloRecibido = "SELLO-X", RecibidoAt = DateTime.UtcNow,
            });
            d.DteEventoDocumentosRelacionados.Add(new DteEventoDocumentoRelacionado
            {
                EventoId = ev.Id, DocumentoId = doc.Id, RolCodigo = DteEventoRolCodigos.Anulado,
                NumeroControlSnapshot = doc.NumeroControl,
            });
        });

        var result = await svc.GetByIdAsync(EmpresaA, ev!.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TieneJson.Should().BeTrue();
        result.Value.TieneJws.Should().BeTrue();
        result.Value.Relacionados.Should().HaveCount(1);
        result.Value.Relacionados[0].RolCodigo.Should().Be(DteEventoRolCodigos.Anulado);
        result.Value.Respuestas.Should().HaveCount(1);
        result.Value.Respuestas[0].CodigoMsg.Should().Be("001");
    }

    [Fact]
    public async Task GetById_RespetaScopeEmpresa()
    {
        DteEvento? ev = null;
        var (svc, _, _) = Build(d =>
        {
            ev = Evento(EmpresaB, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, "S");
            d.DteEventos.Add(ev);
        });

        var result = await svc.GetByIdAsync(EmpresaA, ev!.Id);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("EVENTO_NOT_FOUND");
    }

    [Fact]
    public async Task GetJson_ReturnsJsonAndJws()
    {
        DteEvento? ev = null;
        var (svc, _, _) = Build(d =>
        {
            ev = Evento(EmpresaA, TipoEventoCodigos.Contingencia, DteEventoEstadoCodigos.Procesado, "S");
            ev.Json = new DteEventoJson { JsonSinFirmar = "{\"id\":\"abc\"}", JwsFirmado = "head.payload.sig" };
            d.DteEventos.Add(ev);
        });

        var result = await svc.GetJsonAsync(EmpresaA, ev!.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.JsonSinFirmar.Should().Contain("abc");
        result.Value.JwsFirmado.Should().Be("head.payload.sig");
    }

    [Fact]
    public async Task CrearInvalidacion_DelegatesToDocumentosService()
    {
        var (svc, _, docs) = Build();
        docs.TransmitirInvalidacionEventoAsync(EmpresaA, 5, 2, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<CrearEventoResultadoDto>.Ok(new CrearEventoResultadoDto { SelloOEstado = "OK", EventoId = 99 }));

        var result = await svc.CrearInvalidacionAsync(EmpresaA, new CrearEventoInvalidacionRequest
        {
            DocumentoId = 5, TipoAnulacion = 2,
            NombreResponsable = "Juan", TipoDocResponsable = "13", NumDocResponsable = "00000000-0",
        }, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EventoId.Should().Be(99);
        await docs.Received(1).TransmitirInvalidacionEventoAsync(EmpresaA, 5, 2, Arg.Any<string?>(), Arg.Any<string?>(),
            "Juan", "13", "00000000-0", "tester", Arg.Any<CancellationToken>());
    }
}
