using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte.Eventos;

/// <summary>
/// Sprint 15.3 — verifica que el PDF de evento se genera para los 4 tipos sin
/// excepciones y produce bytes con firma PDF válida (%PDF-).
/// </summary>
public class DteEventoPdfServiceTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 20;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"evento-pdf-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa
        {
            Id = EmpresaA, Nit = "0614-010100-101-1", RazonSocial = "Empresa Demo S.A.",
            NombreComercial = "Demo", EstadoCodigo = "ACTIVA",
            Direccion = "Av Demo 123, San Salvador", Telefono = "2222-2222", Correo = "demo@empresa.sv",
        });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "0002-B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static DteDocumento SeedDoc(NeoStpDbContext db, int empresaId, string nc, string tipo = "01")
    {
        var doc = new DteDocumento
        {
            EmpresaId = empresaId, TipoDteCodigo = tipo, NumeroControl = nc,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = "PROCESADO", SelloRecibido = "SELLO-DTE",
            CreatedAt = DateTime.UtcNow,
        };
        db.DteDocumentos.Add(doc);
        db.SaveChanges();
        return doc;
    }

    private static int SeedEvento(NeoStpDbContext db, int empresaId, string tipo, string estado,
        string? sello = null, string? motivo = null, string? numeroControlRef = null,
        (int docId, string rol, string nc)[]? relacionados = null,
        (string estadoMh, string codMsg, string? sello)? respuesta = null)
    {
        var ev = new DteEvento
        {
            EmpresaId = empresaId,
            TipoEventoCodigo = tipo,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            Version = 2,
            AmbienteCodigo = "PRUEBAS",
            FechaTransmision = DateTime.UtcNow,
            EstadoCodigo = estado,
            SelloRecibido = sello,
            MotivoLibre = motivo,
            NumeroControlReferencia = numeroControlRef,
            Json = new DteEventoJson { JsonSinFirmar = "{}", JwsFirmado = "h.p.s" },
        };
        db.DteEventos.Add(ev);
        db.SaveChanges();

        if (relacionados is not null)
        {
            foreach (var (docId, rol, nc) in relacionados)
            {
                db.DteEventoDocumentosRelacionados.Add(new DteEventoDocumentoRelacionado
                {
                    EventoId = ev.Id, DocumentoId = docId, RolCodigo = rol, NumeroControlSnapshot = nc,
                });
            }
        }
        if (respuesta is { } r)
        {
            db.DteEventoRespuestas.Add(new DteEventoRespuestaHacienda
            {
                EventoId = ev.Id,
                RespuestaCrudaJson = $"{{\"estado\":\"{r.estadoMh}\"}}",
                Estado = r.estadoMh, CodigoMsg = r.codMsg, DescripcionMsg = "Mensaje MH",
                SelloRecibido = r.sello, RecibidoAt = DateTime.UtcNow,
            });
        }
        db.SaveChanges();
        return ev.Id;
    }

    [Theory]
    [InlineData(TipoEventoCodigos.Invalidacion)]
    [InlineData(TipoEventoCodigos.Contingencia)]
    [InlineData(TipoEventoCodigos.Retorno)]
    [InlineData(TipoEventoCodigos.OperacionesEspeciales)]
    public async Task Generar_ParaCadaTipo_ProducePdfValido(string tipo)
    {
        await using var db = NewDb();
        var doc = SeedDoc(db, EmpresaA, "DTE-01-M001P001-000000000000001");
        var id = SeedEvento(db, EmpresaA, tipo, DteEventoEstadoCodigos.Procesado,
            sello: "SELLO-XYZ",
            motivo: "Detalle de prueba",
            numeroControlRef: doc.NumeroControl,
            relacionados: new[] { (doc.Id, DteEventoRolCodigos.Anulado, doc.NumeroControl) },
            respuesta: ("PROCESADO", "001", "SELLO-XYZ"));

        var svc = new DteEventoPdfService(db);
        var result = await svc.GenerarAsync(EmpresaA, id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().NotBeEmpty();
        result.Value.Content.Length.Should().BeGreaterThan(1500, "un PDF de 1 página debe ocupar al menos ~1.5KB");
        // Cabecera PDF: %PDF-
        result.Value.Content[..5].Should().Equal(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D });
        result.Value.FileName.Should().StartWith("evento-");
        result.Value.FileName.Should().EndWith(".pdf");
        result.Value.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Generar_RespetaScopeEmpresa()
    {
        await using var db = NewDb();
        var id = SeedEvento(db, EmpresaB, TipoEventoCodigos.Invalidacion, DteEventoEstadoCodigos.Procesado, sello: "S");

        var svc = new DteEventoPdfService(db);
        var result = await svc.GenerarAsync(EmpresaA, id);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("EVENTO_NOT_FOUND");
    }

    [Fact]
    public async Task Generar_SinRelacionadosNiRespuestas_AunGeneraPdfMinimo()
    {
        await using var db = NewDb();
        var id = SeedEvento(db, EmpresaA, TipoEventoCodigos.OperacionesEspeciales, DteEventoEstadoCodigos.Borrador);

        var svc = new DteEventoPdfService(db);
        var result = await svc.GenerarAsync(EmpresaA, id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Length.Should().BeGreaterThan(1000);
    }
}
