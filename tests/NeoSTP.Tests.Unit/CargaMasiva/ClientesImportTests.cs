using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.CargaMasiva;

/// <summary>
/// CM.1 — carga masiva de clientes: inserción, upsert por documento, reporte de
/// errores por fila y dry-run.
/// </summary>
public class ClientesImportTests
{
    private const int EmpresaA = 10;

    private static (ClientesService svc, NeoStpDbContext db) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"import-cli-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0614", RazonSocial = "Demo", EstadoCodigo = "ACTIVA" });
        seed?.Invoke(db);
        db.SaveChanges();
        return (new ClientesService(db, Substitute.For<IAuditoriaService>()), db);
    }

    private static BulkImportRequest Csv(string content, bool dryRun = false)
        => new() { Format = BulkFileFormat.Csv, DryRun = dryRun, Content = new MemoryStream(Encoding.UTF8.GetBytes(content)) };

    [Fact]
    public async Task Import_InsertaNuevosClientes()
    {
        var (svc, db) = Build();
        var csv = "TipoDocumento,NumeroDocumento,Nombre,Correo\n" +
                  "DUI,12345678-9,Cliente Uno,uno@x.com\n" +
                  "DUI,98765432-1,Cliente Dos,\n";

        var r = await svc.ImportAsync(EmpresaA, Csv(csv), "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Inserted.Should().Be(2);
        r.Value.ErrorCount.Should().Be(0);
        (await db.Clientes.CountAsync(c => c.EmpresaId == EmpresaA)).Should().Be(2);
    }

    [Fact]
    public async Task Import_ActualizaExistentePorDocumento()
    {
        var (svc, db) = Build(db => db.Clientes.Add(new Cliente
        {
            EmpresaId = EmpresaA, TipoDocumentoCodigo = "DUI", NumeroDocumento = "12345678-9",
            Nombre = "Nombre Viejo", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "ACTIVO",
        }));

        var csv = "TipoDocumento,NumeroDocumento,Nombre\nDUI,12345678-9,Nombre Nuevo\n";
        var r = await svc.ImportAsync(EmpresaA, Csv(csv), "tester");

        r.Value!.Updated.Should().Be(1);
        r.Value.Inserted.Should().Be(0);
        (await db.Clientes.SingleAsync()).Nombre.Should().Be("Nombre Nuevo");
    }

    [Fact]
    public async Task Import_FilaInvalida_SeReportaYNoDetiene()
    {
        var (svc, db) = Build();
        var csv = "TipoDocumento,NumeroDocumento,Nombre\n" +
                  "DUI,formato-malo,Cliente Malo\n" +
                  "DUI,12345678-9,Cliente Bueno\n";

        var r = await svc.ImportAsync(EmpresaA, Csv(csv), "tester");

        r.Value!.Inserted.Should().Be(1);
        r.Value.ErrorCount.Should().Be(1);
        r.Value.Errors[0].Row.Should().Be(2);
        (await db.Clientes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Import_DryRun_NoPersiste()
    {
        var (svc, db) = Build();
        var csv = "TipoDocumento,NumeroDocumento,Nombre\nDUI,12345678-9,Cliente Uno\n";

        var r = await svc.ImportAsync(EmpresaA, Csv(csv, dryRun: true), "tester");

        r.Value!.DryRun.Should().BeTrue();
        r.Value.Inserted.Should().Be(1);
        (await db.Clientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_DuplicadoDentroDelArchivo_NoDuplica()
    {
        var (svc, db) = Build();
        var csv = "TipoDocumento,NumeroDocumento,Nombre\n" +
                  "DUI,12345678-9,Primero\n" +
                  "DUI,12345678-9,Segundo\n";

        var r = await svc.ImportAsync(EmpresaA, Csv(csv), "tester");

        r.Value!.Inserted.Should().Be(1);
        r.Value.Updated.Should().Be(1); // la segunda fila actualiza la del mismo archivo
        (await db.Clientes.CountAsync()).Should().Be(1);
    }
}
