using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Portal;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Portal;

/// <summary>NEOPORTAL — tokens públicos: válido/expirado/revocado y aislamiento por empresa/cliente.</summary>
public class PortalServiceTests
{
    private const int Empresa = 90;
    private const int OtraEmpresa = 91;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"portal-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.AddRange(
            new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Emisora SA", EstadoCodigo = "ACTIVA" },
            new Empresa { Id = OtraEmpresa, Nit = "Y", RazonSocial = "Ajena SA", EstadoCodigo = "ACTIVA" });
        db.Clientes.Add(new Cliente { Id = 1, EmpresaId = Empresa, NumeroDocumento = "00000001-1", Nombre = "Cliente Portal" });
        db.DteDocumentos.AddRange(
            new DteDocumento
            {
                Id = 10, EmpresaId = Empresa, ClienteId = 1, TipoDteCodigo = "01",
                NumeroControl = "DTE-01-M001P001-000000000000001",
                CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
                EstadoCodigo = "PROCESADO", TotalPagar = 113m,
            },
            new DteDocumento
            {
                Id = 20, EmpresaId = OtraEmpresa, TipoDteCodigo = "01",
                NumeroControl = "DTE-01-M001P001-000000000000099",
                CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
                EstadoCodigo = "PROCESADO", TotalPagar = 50m,
            });
        db.SaveChanges();
        return db;
    }

    private static PortalService NewSvc(NeoStpDbContext db, ICobranzaService? cobranza = null)
    {
        var dteDocs = Substitute.For<IDteDocumentosService>();
        dteDocs.ObtenerArchivosAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<NeoSTP.Application.Dte.Dtos.DteArchivosDto>.Ok(new NeoSTP.Application.Dte.Dtos.DteArchivosDto
            { NumeroControl = "X", PdfFileName = "x.pdf", PdfContent = [1, 2, 3], JsonFileName = "x.json", JsonContent = "{}" }));
        var cob = cobranza ?? Substitute.For<ICobranzaService>();
        return new PortalService(db, dteDocs, cob, Substitute.For<ICobroQrService>(), Substitute.For<IAuditoriaService>());
    }

    [Fact]
    public async Task GenerarYResolver_Documento_Ok()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var enlace = await svc.GenerarEnlaceDocumentoAsync(Empresa, 10, new GenerarEnlacePortalRequest { DiasValidez = 7 }, "admin");
        enlace.IsSuccess.Should().BeTrue();
        enlace.Value!.Token.Should().NotBeNullOrEmpty();

        var doc = await svc.GetDocumentoAsync(enlace.Value.Token!);

        doc.IsSuccess.Should().BeTrue();
        doc.Value!.NumeroControl.Should().Be("DTE-01-M001P001-000000000000001");
        doc.Value.TotalPagar.Should().Be(113m);
        (await db.PortalAccesos.FirstAsync()).Accesos.Should().Be(1); // registró el acceso
    }

    [Fact]
    public async Task Token_NoSePersisteEnClaro()
    {
        var db = NewDb(); var svc = NewSvc(db);
        var enlace = await svc.GenerarEnlaceDocumentoAsync(Empresa, 10, new(), "admin");

        var acceso = await db.PortalAccesos.FirstAsync();

        acceso.TokenHash.Should().NotBe(enlace.Value!.Token);
        acceso.TokenHash.Should().HaveLength(64); // SHA-256 hex
    }

    [Fact]
    public async Task TokenInvalido_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r = await svc.GetDocumentoAsync("token-que-no-existe-1234567890");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("TOKEN_INVALIDO");
    }

    [Fact]
    public async Task TokenExpirado_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);
        var enlace = await svc.GenerarEnlaceDocumentoAsync(Empresa, 10, new(), "admin");
        var acceso = await db.PortalAccesos.FirstAsync();
        acceso.ExpiraAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var r = await svc.GetDocumentoAsync(enlace.Value!.Token!);

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("TOKEN_EXPIRADO");
    }

    [Fact]
    public async Task TokenRevocado_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);
        var enlace = await svc.GenerarEnlaceDocumentoAsync(Empresa, 10, new(), "admin");
        await svc.RevocarAsync(Empresa, enlace.Value!.Id, "admin");

        var r = await svc.GetDocumentoAsync(enlace.Value.Token!);

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("TOKEN_REVOCADO");
    }

    [Fact]
    public async Task Generar_DocumentoDeOtraEmpresa_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r = await svc.GenerarEnlaceDocumentoAsync(Empresa, 20, new(), "admin"); // doc de OtraEmpresa

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("DTE_NOT_FOUND");
    }

    [Fact]
    public async Task EstadoCuenta_ResuelveSaldoDelCliente()
    {
        var db = NewDb();
        var cobranza = Substitute.For<ICobranzaService>();
        cobranza.GetSaldoClienteAsync(Empresa, 1, Arg.Any<CancellationToken>())
            .Returns(Result<SaldoClienteDto>.Ok(new SaldoClienteDto { ClienteId = 1, ClienteNombre = "Cliente Portal", TotalPendiente = 113m }));
        var svc = NewSvc(db, cobranza);
        var enlace = await svc.GenerarEnlaceEstadoCuentaAsync(Empresa, 1, new(), "admin");

        var r = await svc.GetEstadoCuentaAsync(enlace.Value!.Token!);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Saldo.TotalPendiente.Should().Be(113m);
        r.Value.EmpresaNombre.Should().Be("Emisora SA");
    }

    [Fact]
    public async Task QrEstadoCuenta_FacturaDeOtroCliente_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);
        var enlace = await svc.GenerarEnlaceEstadoCuentaAsync(Empresa, 1, new(), "admin");

        // El doc 20 es de otra empresa (y no del cliente del token) → no debe poder pagarse desde este enlace.
        var r = await svc.GetQrPagoAsync(enlace.Value!.Token!, 20);

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("DTE_NOT_FOUND");
    }

    [Fact]
    public async Task TokenDocumento_NoSirveComoEstadoCuenta()
    {
        var db = NewDb(); var svc = NewSvc(db);
        var enlace = await svc.GenerarEnlaceDocumentoAsync(Empresa, 10, new(), "admin");

        var r = await svc.GetEstadoCuentaAsync(enlace.Value!.Token!);

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("TOKEN_INVALIDO");
    }
}
