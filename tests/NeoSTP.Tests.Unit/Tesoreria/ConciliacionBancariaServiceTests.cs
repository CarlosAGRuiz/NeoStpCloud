using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Tesoreria;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Tesoreria;

/// <summary>V2-D4 — conciliación bancaria: matcher puro, importación CSV y conciliar/desconciliar.</summary>
public class ConciliacionBancariaServiceTests
{
    private const int Empresa = 88;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"conc-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "X", EstadoCodigo = "ACTIVA" });
        db.CuentasTesoreria.Add(new CuentaTesoreria
        {
            Id = 1, EmpresaId = Empresa, Codigo = "BAC-001", Nombre = "BAC corriente", TipoCuenta = "BANCO",
        });
        db.SaveChanges();
        return db;
    }

    private static ConciliacionBancariaService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static MovimientoTesoreria Interno(int id, string fecha, decimal monto, string tipo, string? referencia = null, string estado = "CONFIRMADO")
        => new()
        {
            Id = id, EmpresaId = Empresa, CuentaId = 1, Fecha = DateOnly.Parse(fecha),
            Tipo = tipo, Monto = monto, Concepto = $"Mov {id}", Referencia = referencia, EstadoCodigo = estado,
        };

    private static MovimientoBancario Banco(int id, string fecha, decimal monto, string? referencia = null)
        => new()
        {
            Id = id, EmpresaId = Empresa, CuentaTesoreriaId = 1, Fecha = DateOnly.Parse(fecha),
            Monto = monto, Descripcion = $"Linea {id}", Referencia = referencia,
        };

    private static BulkImportRequest CsvRequest(string csv) => new()
    {
        Format = BulkFileFormat.Csv,
        Content = new MemoryStream(Encoding.UTF8.GetBytes(csv)),
    };

    // ── Matcher puro ─────────────────────────────────────────────────────────

    [Fact]
    public void Sugerir_EmparejaPorMontoSignoYFecha()
    {
        var banco = new[]
        {
            new BancoMatchRow(1, new DateOnly(2026, 6, 5), 150m, null),    // abono → INGRESO
            new BancoMatchRow(2, new DateOnly(2026, 6, 6), -80m, null),    // cargo → EGRESO
        };
        var internos = new[]
        {
            new InternoMatchRow(10, new DateOnly(2026, 6, 5), 150m, "INGRESO", null, "Cobro"),
            new InternoMatchRow(11, new DateOnly(2026, 6, 7), 80m, "EGRESO", null, "Pago proveedor"),
        };

        var s = ConciliacionCalculator.Sugerir(banco, internos, toleranciaDias: 3);

        s.Should().HaveCount(2);
        s.Single(x => x.MovimientoBancoId == 1).MovimientoTesoreriaId.Should().Be(10);
        s.Single(x => x.MovimientoBancoId == 1).Confianza.Should().Be("ALTA"); // misma fecha
        s.Single(x => x.MovimientoBancoId == 2).MovimientoTesoreriaId.Should().Be(11);
        s.Single(x => x.MovimientoBancoId == 2).Confianza.Should().Be("MEDIA"); // 1 día de diferencia
    }

    [Fact]
    public void Sugerir_NoCruzaSignosNiMontosDistintosNiFueraDeTolerancia()
    {
        var banco = new[]
        {
            new BancoMatchRow(1, new DateOnly(2026, 6, 5), 150m, null),
            new BancoMatchRow(2, new DateOnly(2026, 6, 5), -99m, null),
            new BancoMatchRow(3, new DateOnly(2026, 6, 20), 50m, null),
        };
        var internos = new[]
        {
            new InternoMatchRow(10, new DateOnly(2026, 6, 5), 150m, "EGRESO", null, "Signo invertido"),
            new InternoMatchRow(11, new DateOnly(2026, 6, 5), 100m, "EGRESO", null, "Monto distinto"),
            new InternoMatchRow(12, new DateOnly(2026, 6, 1), 50m, "INGRESO", null, "Fuera de tolerancia"),
        };

        var s = ConciliacionCalculator.Sugerir(banco, internos, toleranciaDias: 3);

        s.Should().BeEmpty();
    }

    [Fact]
    public void Sugerir_PrefiereReferenciaCoincidenteYNoReusaInternos()
    {
        // Dos líneas del banco por el mismo monto; un solo interno con referencia que matchea la segunda.
        var banco = new[]
        {
            new BancoMatchRow(1, new DateOnly(2026, 6, 5), 100m, "TRX-555"),
            new BancoMatchRow(2, new DateOnly(2026, 6, 5), 100m, "TRX-777"),
        };
        var internos = new[]
        {
            new InternoMatchRow(10, new DateOnly(2026, 6, 5), 100m, "INGRESO", "TRX-777", "Cobro con ref"),
        };

        var s = ConciliacionCalculator.Sugerir(banco, internos, toleranciaDias: 3);

        s.Should().ContainSingle();
        s[0].MovimientoBancoId.Should().Be(2);
        s[0].MovimientoTesoreriaId.Should().Be(10);
        s[0].Confianza.Should().Be("ALTA");
    }

    // ── Importación ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Importar_CsvConMontoFirmadoYCargoAbono_InsertaYReportaErrores()
    {
        var db = NewDb();
        var csv = "fecha,referencia,descripcion,monto\n" +
                  "2026-06-05,TRX-1,Deposito cliente,150.00\n" +
                  "06/06/2026,TRX-2,Pago cheque,-80.50\n" +
                  "fecha-mala,TRX-3,Linea invalida,10.00\n";

        var r = await NewSvc(db).ImportarAsync(Empresa, 1, CsvRequest(csv), "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Inserted.Should().Be(2);
        r.Value.ErrorCount.Should().Be(1);
        r.Value.Errors[0].Row.Should().Be(4);
        var movs = db.MovimientosBancarios.OrderBy(m => m.Fecha).ToList();
        movs.Should().HaveCount(2);
        movs[0].Monto.Should().Be(150.00m);
        movs[1].Monto.Should().Be(-80.50m);
        movs.Should().OnlyContain(m => m.EstadoCodigo == "NO_CONCILIADO" && m.CuentaTesoreriaId == 1);

        // Columnas cargo/abono separadas.
        var csv2 = "fecha,descripcion,cargo,abono\n2026-06-07,Comision,5.25,\n2026-06-07,Remesa,,200\n";
        var r2 = await NewSvc(db).ImportarAsync(Empresa, 1, CsvRequest(csv2), "tester");
        r2.Value!.Inserted.Should().Be(2);
        db.MovimientosBancarios.Single(m => m.Descripcion == "Comision").Monto.Should().Be(-5.25m);
        db.MovimientosBancarios.Single(m => m.Descripcion == "Remesa").Monto.Should().Be(200m);
    }

    [Fact]
    public async Task Importar_ReimportarMismoArchivo_OmiteDuplicados()
    {
        var db = NewDb();
        var csv = "fecha,referencia,descripcion,monto\n2026-06-05,TRX-1,Deposito,150.00\n";
        var svc = NewSvc(db);

        (await svc.ImportarAsync(Empresa, 1, CsvRequest(csv), "tester")).Value!.Inserted.Should().Be(1);
        var r2 = await svc.ImportarAsync(Empresa, 1, CsvRequest(csv), "tester");

        r2.Value!.Inserted.Should().Be(0);
        r2.Value.Skipped.Should().Be(1);
        db.MovimientosBancarios.Count().Should().Be(1);
    }

    [Fact]
    public async Task Importar_CuentaDeOtraEmpresa_Falla()
    {
        var db = NewDb();
        db.CuentasTesoreria.Add(new CuentaTesoreria { Id = 2, EmpresaId = 999, Codigo = "AJENA", Nombre = "Ajena", TipoCuenta = "BANCO" });
        db.SaveChanges();

        var r = await NewSvc(db).ImportarAsync(Empresa, 2, CsvRequest("fecha,monto,descripcion\n2026-06-05,10,X\n"), "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("CUENTA_TES_NOT_FOUND");
    }

    // ── Conciliar / desconciliar ─────────────────────────────────────────────

    [Fact]
    public async Task Conciliar_ValidaCuentaEstadoYMonto()
    {
        var db = NewDb();
        db.MovimientosBancarios.Add(Banco(1, "2026-06-05", 150m, "TRX-1"));
        db.MovimientosTesoreria.AddRange(
            Interno(10, "2026-06-05", 150m, "INGRESO"),
            Interno(11, "2026-06-05", 150m, "EGRESO"),            // signo incompatible con abono
            Interno(12, "2026-06-05", 150m, "INGRESO", estado: "ANULADO"));
        db.SaveChanges();
        var svc = NewSvc(db);

        (await svc.ConciliarAsync(Empresa, 1, 11, "tester")).ErrorCode.Should().Be("VALIDATION");
        (await svc.ConciliarAsync(Empresa, 1, 12, "tester")).ErrorCode.Should().Be("INVALID_STATE");

        var ok = await svc.ConciliarAsync(Empresa, 1, 10, "tester");
        ok.IsSuccess.Should().BeTrue();
        var banco = db.MovimientosBancarios.Single(m => m.Id == 1);
        banco.EstadoCodigo.Should().Be("CONCILIADO");
        banco.MovimientoTesoreriaId.Should().Be(10);
        banco.ConciliadoPor.Should().Be("tester");

        // Ya conciliada: segunda conciliación falla.
        (await svc.ConciliarAsync(Empresa, 1, 10, "tester")).ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Conciliar_InternoYaUsadoPorOtraLinea_Falla()
    {
        var db = NewDb();
        db.MovimientosBancarios.AddRange(Banco(1, "2026-06-05", 150m), Banco(2, "2026-06-05", 150m));
        db.MovimientosTesoreria.Add(Interno(10, "2026-06-05", 150m, "INGRESO"));
        db.SaveChanges();
        var svc = NewSvc(db);

        (await svc.ConciliarAsync(Empresa, 1, 10, "tester")).IsSuccess.Should().BeTrue();
        var r = await svc.ConciliarAsync(Empresa, 2, 10, "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Desconciliar_RestauraPendienteYPermiteReconciliar()
    {
        var db = NewDb();
        db.MovimientosBancarios.Add(Banco(1, "2026-06-05", 150m));
        db.MovimientosTesoreria.Add(Interno(10, "2026-06-05", 150m, "INGRESO"));
        db.SaveChanges();
        var svc = NewSvc(db);
        (await svc.ConciliarAsync(Empresa, 1, 10, "tester")).IsSuccess.Should().BeTrue();

        var r = await svc.DesconciliarAsync(Empresa, 1, "tester");

        r.IsSuccess.Should().BeTrue();
        var banco = db.MovimientosBancarios.Single(m => m.Id == 1);
        banco.EstadoCodigo.Should().Be("NO_CONCILIADO");
        banco.MovimientoTesoreriaId.Should().BeNull();
        (await svc.DesconciliarAsync(Empresa, 1, "tester")).ErrorCode.Should().Be("INVALID_STATE");
        (await svc.ConciliarAsync(Empresa, 1, 10, "tester")).IsSuccess.Should().BeTrue();
    }

    // ── Conciliación parcial N:1 (V2.5-S1) ───────────────────────────────────

    [Fact]
    public void SugerirCombinaciones_EncuentraParQueSumaExacto_SinRobarMatches1a1()
    {
        var banco = new[]
        {
            new BancoMatchRow(1, new DateOnly(2026, 6, 5), 500m, null),   // solo combinable: 300+200
            new BancoMatchRow(2, new DateOnly(2026, 6, 5), 300m, null),   // tiene match 1:1 con el 10
        };
        var internos = new[]
        {
            new InternoMatchRow(10, new DateOnly(2026, 6, 5), 300m, "INGRESO", null, "Cobro A"),
            new InternoMatchRow(11, new DateOnly(2026, 6, 5), 300m, "INGRESO", null, "Cobro B"),
            new InternoMatchRow(12, new DateOnly(2026, 6, 5), 200m, "INGRESO", null, "Cobro C"),
        };

        var combos = ConciliacionCalculator.SugerirCombinaciones(banco, internos, toleranciaDias: 3);

        // El 1:1 (línea 2 ↔ algún interno de 300) tiene prioridad; la combinación usa los restantes.
        combos.Should().ContainSingle();
        combos[0].MovimientoBancoId.Should().Be(1);
        combos[0].CombinacionIds.Should().HaveCount(2);
        combos[0].CombinacionIds.Should().Contain(12);
        combos[0].Confianza.Should().Be("MEDIA");
    }

    [Fact]
    public async Task ConciliarParcial_AcumulaHastaCompletarYRechazaExceso()
    {
        var db = NewDb();
        db.MovimientosBancarios.Add(Banco(1, "2026-06-05", 500m));
        db.MovimientosTesoreria.AddRange(
            Interno(10, "2026-06-05", 300m, "INGRESO"),
            Interno(11, "2026-06-05", 200m, "INGRESO"),
            Interno(12, "2026-06-05", 50m, "INGRESO"));
        db.SaveChanges();
        var svc = NewSvc(db);

        (await svc.ConciliarAsync(Empresa, 1, 10, "tester")).IsSuccess.Should().BeTrue();
        db.MovimientosBancarios.Single().EstadoCodigo.Should().Be("PARCIAL");

        // Exceso: 300 + 200 + 50 > 500 al intentar agregar el 12 después del 11.
        (await svc.ConciliarAsync(Empresa, 1, 11, "tester")).IsSuccess.Should().BeTrue();
        var banco = db.MovimientosBancarios.Single();
        banco.EstadoCodigo.Should().Be("CONCILIADO");
        db.ConciliacionDetalles.Count().Should().Be(2);

        (await svc.ConciliarAsync(Empresa, 1, 12, "tester")).ErrorCode.Should().Be("INVALID_STATE"); // ya completa

        // Nueva línea: exceso directo rechazado.
        db.MovimientosBancarios.Add(Banco(2, "2026-06-05", 40m));
        db.SaveChanges();
        (await svc.ConciliarAsync(Empresa, 2, 12, "tester")).ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task ConciliarCombinacion_AplicaVariosYQuitarDetalleRegresaEstado()
    {
        var db = NewDb();
        db.MovimientosBancarios.Add(Banco(1, "2026-06-05", -500m)); // cargo agrupado
        db.MovimientosTesoreria.AddRange(
            Interno(10, "2026-06-05", 300m, "EGRESO"),
            Interno(11, "2026-06-06", 200m, "EGRESO"));
        db.SaveChanges();
        var svc = NewSvc(db);

        var r = await svc.ConciliarCombinacionAsync(Empresa, 1, [10, 11], "tester");

        r.IsSuccess.Should().BeTrue();
        var banco = db.MovimientosBancarios.Single();
        banco.EstadoCodigo.Should().Be("CONCILIADO");
        banco.MovimientoTesoreriaId.Should().BeNull(); // N:1 no tiene "principal"
        db.ConciliacionDetalles.Count().Should().Be(2);

        (await svc.QuitarDetalleAsync(Empresa, 1, 11, "tester")).IsSuccess.Should().BeTrue();
        db.MovimientosBancarios.Single().EstadoCodigo.Should().Be("PARCIAL");
        (await svc.QuitarDetalleAsync(Empresa, 1, 10, "tester")).IsSuccess.Should().BeTrue();
        db.MovimientosBancarios.Single().EstadoCodigo.Should().Be("NO_CONCILIADO");
        db.ConciliacionDetalles.Should().BeEmpty();
    }

    [Fact]
    public async Task ConciliarCombinacion_RechazaRepetidosYSignoIncompatible()
    {
        var db = NewDb();
        db.MovimientosBancarios.Add(Banco(1, "2026-06-05", 500m));
        db.MovimientosTesoreria.AddRange(
            Interno(10, "2026-06-05", 300m, "INGRESO"),
            Interno(11, "2026-06-05", 200m, "EGRESO"));
        db.SaveChanges();
        var svc = NewSvc(db);

        (await svc.ConciliarCombinacionAsync(Empresa, 1, [10, 10], "tester")).ErrorCode.Should().Be("VALIDATION");
        (await svc.ConciliarCombinacionAsync(Empresa, 1, [10, 11], "tester")).ErrorCode.Should().Be("VALIDATION");
        db.ConciliacionDetalles.Should().BeEmpty(); // nada quedó a medias
    }

    [Fact]
    public async Task ConciliarSugeridos_AplicaSoloConfianzaAltaYResumenCuadra()
    {
        var db = NewDb();
        db.MovimientosBancarios.AddRange(
            Banco(1, "2026-06-05", 150m, "TRX-1"),   // ALTA: misma fecha
            Banco(2, "2026-06-06", -80m),            // MEDIA: 1 día de diferencia, sin referencia
            Banco(3, "2026-06-05", 999m));           // sin candidato
        db.MovimientosTesoreria.AddRange(
            Interno(10, "2026-06-05", 150m, "INGRESO", "TRX-1"),
            Interno(11, "2026-06-07", 80m, "EGRESO"));
        db.SaveChanges();
        var svc = NewSvc(db);

        var r = await svc.ConciliarSugeridosAsync(Empresa, 1, toleranciaDias: 3, actor: "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(1);
        db.MovimientosBancarios.Single(m => m.Id == 1).EstadoCodigo.Should().Be("CONCILIADO");
        db.MovimientosBancarios.Single(m => m.Id == 2).EstadoCodigo.Should().Be("NO_CONCILIADO");

        var resumen = (await svc.ResumenAsync(Empresa, 1)).Value!;
        resumen.TotalBanco.Should().Be(3);
        resumen.Conciliados.Should().Be(1);
        resumen.NoConciliados.Should().Be(2);
        resumen.MontoNoConciliado.Should().Be(80m + 999m);
        resumen.InternosSinConciliar.Should().Be(1); // el 11 sigue libre
    }
}
