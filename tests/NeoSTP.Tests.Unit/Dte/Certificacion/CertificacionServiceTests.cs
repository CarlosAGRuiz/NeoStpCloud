using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Certificacion.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte.Certificacion;

/// <summary>
/// Sprint 14.2 — CertificacionDteService: cálculo de progreso, asociación de
/// documentos a escenarios, reintentos y aislamiento multi-tenant.
/// </summary>
public class CertificacionServiceTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 20;

    private static (CertificacionDteService svc, NeoStpDbContext db) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cert-svc-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        // EnsureCreated aplica HasData (matriz + 625 escenarios).
        db.Database.EnsureCreated();

        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0001-A", RazonSocial = "Empresa A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "0002-B", RazonSocial = "Empresa B", EstadoCodigo = "ACTIVA" });
        seed?.Invoke(db);
        db.SaveChanges();

        var audit = Substitute.For<IAuditoriaService>();
        return (new CertificacionDteService(db, audit), db);
    }

    private static int EscenarioIdFactura1(NeoStpDbContext db)
        => db.CertificacionEscenarios.AsNoTracking()
            .Single(e => e.Codigo == "FACTURA-01").Id;

    private static DteDocumento DteFactura(int empresaId, string numeroControl, string estado, string? sello = null)
        => new()
        {
            EmpresaId = empresaId,
            TipoDteCodigo = "01",
            NumeroControl = numeroControl,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = estado,
            SelloRecibido = sello,
            ProcesadoAt = sello is null ? null : DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

    // ----------------------------------------------------- Lectura / progreso

    [Fact]
    public async Task GetResumen_EmptyEmpresa_ReturnsZeros()
    {
        var (svc, _) = Build();

        var result = await svc.GetResumenAsync(EmpresaA);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Requeridos.Should().Be(625);
        result.Value.Completados.Should().Be(0);
        result.Value.EnProgreso.Should().Be(0);
        result.Value.ConError.Should().Be(0);
        result.Value.PorcentajeProgreso.Should().Be(0);
        result.Value.ListaParaAutorizacion.Should().BeFalse();
    }

    [Fact]
    public async Task GetMatriz_ReturnsAll15TiposOrderedByOrden()
    {
        var (svc, _) = Build();

        var result = await svc.GetMatrizAsync(EmpresaA);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(15);
        result.Value!.First().Orden.Should().Be(1);
        result.Value!.First().TipoDteCodigo.Should().Be("01");
        result.Value!.Last().TipoDteCodigo.Should().Be("OPERACIONES_ESPECIALES");
    }

    [Fact]
    public async Task GetResumen_CountsCompletedAndError_PerEmpresa()
    {
        var (svc, db) = Build();
        var escId = EscenarioIdFactura1(db);

        // Empresa A: 1 completada, 1 error en otro escenario.
        db.CertificacionPruebas.Add(new CertificacionPrueba
        {
            EmpresaId = EmpresaA, EscenarioId = escId,
            EstadoCodigo = CertificacionEstadoCodigos.Completado,
            SelloRecibido = "SELLO-A1", IntentoNumero = 1,
        });
        var escFactura2 = db.CertificacionEscenarios.AsNoTracking().Single(e => e.Codigo == "FACTURA-02").Id;
        db.CertificacionPruebas.Add(new CertificacionPrueba
        {
            EmpresaId = EmpresaA, EscenarioId = escFactura2,
            EstadoCodigo = CertificacionEstadoCodigos.Error, IntentoNumero = 1,
        });
        // Empresa B: 1 completada (no debe contar para A).
        db.CertificacionPruebas.Add(new CertificacionPrueba
        {
            EmpresaId = EmpresaB, EscenarioId = escId,
            EstadoCodigo = CertificacionEstadoCodigos.Completado,
            SelloRecibido = "SELLO-B1", IntentoNumero = 1,
        });
        await db.SaveChangesAsync();

        var a = await svc.GetResumenAsync(EmpresaA);
        var b = await svc.GetResumenAsync(EmpresaB);

        a.Value!.Completados.Should().Be(1);
        a.Value.ConError.Should().Be(1);
        a.Value.EnProgreso.Should().Be(0);
        b.Value!.Completados.Should().Be(1);
        b.Value.ConError.Should().Be(0, "los datos de empresa A no deben filtrarse a empresa B");
    }

    [Fact]
    public async Task GetResumen_TakesLatestIntento_WhenMultipleAttempts()
    {
        var (svc, db) = Build();
        var escId = EscenarioIdFactura1(db);

        // Intento 1: ERROR. Intento 2: COMPLETADO. Debe contar como COMPLETADO.
        db.CertificacionPruebas.AddRange(
            new CertificacionPrueba { EmpresaId = EmpresaA, EscenarioId = escId, EstadoCodigo = CertificacionEstadoCodigos.Error, IntentoNumero = 1 },
            new CertificacionPrueba { EmpresaId = EmpresaA, EscenarioId = escId, EstadoCodigo = CertificacionEstadoCodigos.Completado, IntentoNumero = 2 });
        await db.SaveChangesAsync();

        var resumen = await svc.GetResumenAsync(EmpresaA);

        resumen.Value!.Completados.Should().Be(1);
        resumen.Value.ConError.Should().Be(0);
    }

    // ---------------------------------------------------- Generar prueba

    [Fact]
    public async Task GenerarPrueba_FirstTime_ReturnsScenarioOne()
    {
        var (svc, _) = Build();

        var result = await svc.GenerarPruebaAsync("01", EmpresaA, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EscenarioCodigo.Should().Be("FACTURA-01");
        result.Value.EstadoCodigo.Should().Be(CertificacionEstadoCodigos.EnProgreso);
        result.Value.IntentoNumero.Should().Be(1);
    }

    [Fact]
    public async Task GenerarPrueba_SkipsCompletedAndInProgress_PicksNextPendiente()
    {
        var (svc, db) = Build();
        var f1 = EscenarioIdFactura1(db);
        var f2 = db.CertificacionEscenarios.AsNoTracking().Single(e => e.Codigo == "FACTURA-02").Id;

        db.CertificacionPruebas.AddRange(
            new CertificacionPrueba { EmpresaId = EmpresaA, EscenarioId = f1, EstadoCodigo = CertificacionEstadoCodigos.Completado, IntentoNumero = 1 },
            new CertificacionPrueba { EmpresaId = EmpresaA, EscenarioId = f2, EstadoCodigo = CertificacionEstadoCodigos.EnProgreso, IntentoNumero = 1 });
        await db.SaveChangesAsync();

        var result = await svc.GenerarPruebaAsync("01", EmpresaA, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EscenarioCodigo.Should().Be("FACTURA-03");
    }

    // ---------------------------------------------------- Marcar completado

    [Fact]
    public async Task MarcarCompletado_WithProcessedDte_SetsCompletadoAndSello()
    {
        var (svc, db) = Build();
        var escId = EscenarioIdFactura1(db);
        var doc = DteFactura(EmpresaA, "DTE-01-M001P001-000000000000001",
            DteEstadoCodigos.Procesado, sello: "SELLO-X");
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        var result = await svc.MarcarCompletadoAsync(doc.Id, new MarcarCompletadoRequest { EscenarioId = escId }, EmpresaA, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(CertificacionEstadoCodigos.Completado);
        result.Value.SelloRecibido.Should().Be("SELLO-X");
        result.Value.DteDocumentoId.Should().Be(doc.Id);
    }

    [Fact]
    public async Task MarcarCompletado_WithMismatchedTipoDte_ReturnsConflict()
    {
        var (svc, db) = Build();
        var escId = EscenarioIdFactura1(db); // matriz tipo "01"
        var doc = DteFactura(EmpresaA, "DTE-03-M001P001-000000000000002",
            DteEstadoCodigos.Procesado, sello: "SELLO-Y");
        doc.TipoDteCodigo = "03"; // CCF, no factura
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        var result = await svc.MarcarCompletadoAsync(doc.Id, new MarcarCompletadoRequest { EscenarioId = escId }, EmpresaA, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CERT_TIPO_MISMATCH");
    }

    [Fact]
    public async Task MarcarCompletado_RespectsEmpresaScope()
    {
        var (svc, db) = Build();
        var escId = EscenarioIdFactura1(db);
        var doc = DteFactura(EmpresaB, "DTE-01-M001P001-000000000000003",
            DteEstadoCodigos.Procesado, sello: "Z");
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();

        // Empresa A intenta marcar un DTE que es de Empresa B.
        var result = await svc.MarcarCompletadoAsync(doc.Id, new MarcarCompletadoRequest { EscenarioId = escId }, EmpresaA, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DTE_NOT_FOUND");
    }

    // ---------------------------------------------------- Reintentar

    [Fact]
    public async Task Reintentar_ErrorPrueba_OpensNewIntentoPendiente()
    {
        var (svc, db) = Build();
        var escId = EscenarioIdFactura1(db);
        var doc = DteFactura(EmpresaA, "DTE-01-M001P001-000000000000004",
            DteEstadoCodigos.Rechazado);
        db.DteDocumentos.Add(doc);
        db.CertificacionPruebas.Add(new CertificacionPrueba
        {
            EmpresaId = EmpresaA, EscenarioId = escId, DteDocumentoId = doc.Id,
            EstadoCodigo = CertificacionEstadoCodigos.Error, IntentoNumero = 1,
        });
        await db.SaveChangesAsync();

        var result = await svc.ReintentarAsync(doc.Id, EmpresaA, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IntentoNumero.Should().Be(2);
        result.Value.EstadoCodigo.Should().Be(CertificacionEstadoCodigos.Pendiente);
        result.Value.DteDocumentoId.Should().BeNull("el nuevo intento queda sin DTE asociado");
    }

    [Fact]
    public async Task GetErrores_FiltersByCodigoMhAndEmpresa()
    {
        var (svc, db) = Build();
        var escId = EscenarioIdFactura1(db);
        var prueba = new CertificacionPrueba { EmpresaId = EmpresaA, EscenarioId = escId, EstadoCodigo = CertificacionEstadoCodigos.Error };
        db.CertificacionPruebas.Add(prueba);
        await db.SaveChangesAsync();
        db.CertificacionErrores.AddRange(
            new CertificacionError { PruebaId = prueba.Id, CodigoMh = "095", Descripcion = "no autorizado" },
            new CertificacionError { PruebaId = prueba.Id, CodigoMh = "096", Descripcion = "esquema" });
        await db.SaveChangesAsync();

        var all = await svc.GetErroresAsync(EmpresaA);
        var only96 = await svc.GetErroresAsync(EmpresaA, "096");

        all.Value!.Should().HaveCount(2);
        only96.Value!.Should().HaveCount(1);
        only96.Value!.Single().CodigoMh.Should().Be("096");
    }
}
