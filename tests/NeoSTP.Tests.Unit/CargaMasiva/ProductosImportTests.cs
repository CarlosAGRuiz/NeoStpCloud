using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.CargaMasiva;

/// <summary>CM.2 — carga masiva de productos: inserción, upsert por código, errores y dry-run.</summary>
public class ProductosImportTests
{
    private const int EmpresaA = 10;

    private static (ProductosService svc, NeoStpDbContext db) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"import-prod-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0614", RazonSocial = "Demo", EstadoCodigo = "ACTIVA" });
        seed?.Invoke(db);
        db.SaveChanges();
        return (new ProductosService(db, Substitute.For<IAuditoriaService>()), db);
    }

    private static BulkImportRequest Csv(string content, bool dryRun = false)
        => new() { Format = BulkFileFormat.Csv, DryRun = dryRun, Content = new MemoryStream(Encoding.UTF8.GetBytes(content)) };

    [Fact]
    public async Task Import_InsertaProductos()
    {
        var (svc, db) = Build();
        var csv = "Codigo,Nombre,Tipo,UnidadMedida,Precio,AplicaIva\n" +
                  "PROD-1,Licencia Anual,SERVICIO,59,1250.00,si\n" +
                  "PROD-2,Soporte,SERVICIO,59,85,no\n";

        var r = await svc.ImportAsync(EmpresaA, Csv(csv), "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Inserted.Should().Be(2);
        var prod = await db.Productos.AsNoTracking().FirstAsync(p => p.CodigoInterno == "PROD-1");
        prod.PrecioUnitario.Should().Be(1250.00m);
        prod.AplicaIva.Should().BeTrue();
    }

    [Fact]
    public async Task Import_ActualizaPorCodigo()
    {
        var (svc, db) = Build(db => db.Productos.Add(new Producto
        {
            EmpresaId = EmpresaA, CodigoInterno = "PROD-1", Nombre = "Viejo", TipoItem = "BIEN",
            UnidadMedidaCodigo = "59", PrecioUnitario = 10m, EstadoCodigo = "ACTIVO",
        }));

        var csv = "Codigo,Nombre,Precio\nPROD-1,Nuevo,99.50\n";
        var r = await svc.ImportAsync(EmpresaA, Csv(csv), "tester");

        r.Value!.Updated.Should().Be(1);
        var prod = await db.Productos.SingleAsync();
        prod.Nombre.Should().Be("Nuevo");
        prod.PrecioUnitario.Should().Be(99.50m);
    }

    [Fact]
    public async Task Import_TipoInvalido_SeReporta()
    {
        var (svc, _) = Build();
        var csv = "Codigo,Nombre,Tipo,Precio\nPROD-X,Malo,COSA,10\nPROD-Y,Bueno,BIEN,10\n";

        var r = await svc.ImportAsync(EmpresaA, Csv(csv), "tester");

        r.Value!.Inserted.Should().Be(1);
        r.Value.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task Import_DryRun_NoPersiste()
    {
        var (svc, db) = Build();
        var csv = "Codigo,Nombre,Precio\nPROD-1,X,10\n";

        var r = await svc.ImportAsync(EmpresaA, Csv(csv, dryRun: true), "tester");

        r.Value!.Inserted.Should().Be(1);
        (await db.Productos.CountAsync()).Should().Be(0);
    }
}
