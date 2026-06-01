using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NeoSTP.Infrastructure.Services.Catalogos;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Catalogos;

/// <summary>
/// Sprint 13.3 — parsers/exporters de catálogos y flujo end-to-end del servicio.
/// </summary>
public class CatalogoImportExportTests
{
    private const int EmpresaA = 10;

    private static (CatalogosService svc, NeoStpDbContext db) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cat-impexp-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        seed?.Invoke(db);
        db.SaveChanges();
        return (new CatalogosService(db, Substitute.For<IAuditoriaService>()), db);
    }

    private static Stream ToStream(string content)
        => new MemoryStream(Encoding.UTF8.GetBytes(content));

    // ---------------------------------------------------------------- Parsers

    [Fact]
    public void ParseCsv_ReadsHeaderAndRows_WithQuoting()
    {
        var csv = "Codigo,Valor,Descripcion,Orden,ParentCodigo,MetadataJson,Activo\n" +
                  "01,Efectivo,,1,,,true\n" +
                  "02,\"Tarjeta, crédito\",,2,,,true\n" +
                  "03,\"Quoted \"\"value\"\"\",,3,,,false\n";

        var rows = CatalogoItemParsers.ParseCsv(ToStream(csv));

        rows.Should().HaveCount(3);
        rows[0].Codigo.Should().Be("01");
        rows[0].Valor.Should().Be("Efectivo");
        rows[0].Orden.Should().Be(1);
        rows[0].Activo.Should().BeTrue();
        rows[1].Valor.Should().Be("Tarjeta, crédito");
        rows[2].Valor.Should().Be("Quoted \"value\"");
        rows[2].Activo.Should().BeFalse();
    }

    [Fact]
    public void ParseCsv_RejectsMissingMandatoryColumn()
    {
        var csv = "Codigo,Descripcion\n01,Solo descripción\n";

        var act = () => CatalogoItemParsers.ParseCsv(ToStream(csv));

        act.Should().Throw<FormatException>().WithMessage("*Valor*");
    }

    [Fact]
    public void ParseJson_ReadsArrayOfObjects()
    {
        var json = "[{\"codigo\":\"SS\",\"valor\":\"San Salvador\",\"activo\":true,\"metadataJson\":{\"region\":\"central\"}}]";

        var rows = CatalogoItemParsers.ParseJson(ToStream(json));

        rows.Should().HaveCount(1);
        rows[0].Codigo.Should().Be("SS");
        rows[0].Activo.Should().BeTrue();
        rows[0].MetadataJson.Should().Contain("\"region\"");
    }

    // -------------------------------------------------------- Service: Import

    [Fact]
    public async Task ImportItemsAsync_InsertsAndUpdates_AndCountsCorrectly()
    {
        var (svc, db) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "FORMA_PAGO", Nombre = "FP", EmpresaId = EmpresaA });
            d.CatalogoItems.Add(new CatalogoItem { CatalogoId = 1, Codigo = "01", Valor = "Efectivo viejo", Orden = 1 });
        });

        var csv = "Codigo,Valor,Orden,Activo\n" +
                  "01,Efectivo actualizado,1,true\n" +
                  "02,Tarjeta,2,true\n" +
                  "03,Transferencia,3,true\n";

        var result = await svc.ImportItemsAsync(EmpresaA, "FORMA_PAGO",
            new CatalogoImportRequest { Format = CatalogoFileFormat.Csv, Content = ToStream(csv) },
            "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(3);
        result.Value.Inserted.Should().Be(2);
        result.Value.Updated.Should().Be(1);
        result.Value.ErrorCount.Should().Be(0);

        var items = await db.CatalogoItems.AsNoTracking().Where(i => i.CatalogoId == 1).OrderBy(i => i.Codigo).ToListAsync();
        items.Should().HaveCount(3);
        items.Single(i => i.Codigo == "01").Valor.Should().Be("Efectivo actualizado");
    }

    [Fact]
    public async Task ImportItemsAsync_DryRun_DoesNotPersist()
    {
        var (svc, db) = Build(d => d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "X", Nombre = "X", EmpresaId = EmpresaA }));
        var csv = "Codigo,Valor\nA,Alfa\nB,Beta\n";

        var result = await svc.ImportItemsAsync(EmpresaA, "X",
            new CatalogoImportRequest { Format = CatalogoFileFormat.Csv, Content = ToStream(csv), DryRun = true },
            "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.DryRun.Should().BeTrue();
        result.Value.Inserted.Should().Be(2);
        (await db.CatalogoItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportItemsAsync_InsertOnly_SkipsExistingCodigos()
    {
        var (svc, db) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "X", Nombre = "X", EmpresaId = EmpresaA });
            d.CatalogoItems.Add(new CatalogoItem { CatalogoId = 1, Codigo = "A", Valor = "Original" });
        });

        var csv = "Codigo,Valor\nA,No-debe-pisar\nB,Beta\n";

        var result = await svc.ImportItemsAsync(EmpresaA, "X",
            new CatalogoImportRequest { Format = CatalogoFileFormat.Csv, Content = ToStream(csv), Mode = CatalogoImportMode.InsertOnly },
            "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Inserted.Should().Be(1);
        result.Value.Updated.Should().Be(0);
        result.Value.Skipped.Should().Be(1);
        (await db.CatalogoItems.AsNoTracking().SingleAsync(i => i.Codigo == "A")).Valor.Should().Be("Original");
    }

    [Fact]
    public async Task ImportItemsAsync_ReportsParentNotFound_AsErrorRow()
    {
        var (svc, _) = Build(d => d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "MUN", Nombre = "M", EmpresaId = EmpresaA }));
        var csv = "Codigo,Valor,ParentCodigo\nSS,San Salvador,\nSS-01,Soyapango,SX\n"; // SX no existe

        var result = await svc.ImportItemsAsync(EmpresaA, "MUN",
            new CatalogoImportRequest { Format = CatalogoFileFormat.Csv, Content = ToStream(csv) },
            "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Inserted.Should().Be(1);
        result.Value.ErrorCount.Should().Be(1);
        result.Value.Errors[0].Codigo.Should().Be("SS-01");
        result.Value.Errors[0].Message.Should().Contain("Padre");
    }

    [Fact]
    public async Task ImportItemsAsync_AllowsForwardParentReferenceInSameBatch()
    {
        var (svc, db) = Build(d => d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "MUN", Nombre = "M", EmpresaId = EmpresaA }));
        var csv = "Codigo,Valor,ParentCodigo\nSS,San Salvador,\nSS-01,Soyapango,SS\n"; // padre SS está antes en el lote

        var result = await svc.ImportItemsAsync(EmpresaA, "MUN",
            new CatalogoImportRequest { Format = CatalogoFileFormat.Csv, Content = ToStream(csv) },
            "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Inserted.Should().Be(2);
        result.Value.ErrorCount.Should().Be(0);
        (await db.CatalogoItems.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ImportItemsAsync_ReturnsFormatError_OnBadJson()
    {
        var (svc, _) = Build(d => d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "X", Nombre = "X", EmpresaId = EmpresaA }));

        var result = await svc.ImportItemsAsync(EmpresaA, "X",
            new CatalogoImportRequest { Format = CatalogoFileFormat.Json, Content = ToStream("{not-array}") },
            "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CAT_IMPORT_FORMAT");
    }

    // -------------------------------------------------------- Service: Export

    [Fact]
    public async Task ExportItemsAsync_Csv_RoundTripsThroughParser()
    {
        var (svc, _) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "FORMA_PAGO", Nombre = "FP", EmpresaId = EmpresaA, Version = 3 });
            d.CatalogoItems.AddRange(
                new CatalogoItem { CatalogoId = 1, Codigo = "01", Valor = "Efectivo", Orden = 1 },
                new CatalogoItem { CatalogoId = 1, Codigo = "02", Valor = "Tarjeta, crédito", Orden = 2, ParentCodigo = "01" });
        });

        var result = await svc.ExportItemsAsync(EmpresaA, "FORMA_PAGO", CatalogoFileFormat.Csv);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileName.Should().Be("FORMA_PAGO.v3.csv");
        result.Value.ContentType.Should().StartWith("text/csv");

        var rows = CatalogoItemParsers.ParseCsv(new MemoryStream(result.Value.Content));
        rows.Should().HaveCount(2);
        rows[1].Valor.Should().Be("Tarjeta, crédito");
        rows[1].ParentCodigo.Should().Be("01");
    }

    [Fact]
    public async Task ExportItemsAsync_Json_HasExpectedContentType()
    {
        var (svc, _) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "X", Nombre = "X", EmpresaId = EmpresaA });
            d.CatalogoItems.Add(new CatalogoItem { CatalogoId = 1, Codigo = "A", Valor = "Alpha", Orden = 1 });
        });

        var result = await svc.ExportItemsAsync(EmpresaA, "X", CatalogoFileFormat.Json);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContentType.Should().Be("application/json");
        Encoding.UTF8.GetString(result.Value.Content).Should().Contain("\"Alpha\"");
    }

    [Fact]
    public async Task ExportItemsAsync_Xlsx_RoundTripsThroughParser()
    {
        var (svc, _) = Build(d =>
        {
            d.Catalogos.Add(new Catalogo { Id = 1, Codigo = "X", Nombre = "X", EmpresaId = EmpresaA });
            d.CatalogoItems.Add(new CatalogoItem { CatalogoId = 1, Codigo = "A", Valor = "Alpha", Orden = 1, Activo = true });
        });

        var result = await svc.ExportItemsAsync(EmpresaA, "X", CatalogoFileFormat.Xlsx);

        result.IsSuccess.Should().BeTrue();
        var rows = CatalogoItemParsers.ParseXlsx(new MemoryStream(result.Value!.Content));
        rows.Should().HaveCount(1);
        rows[0].Codigo.Should().Be("A");
        rows[0].Valor.Should().Be("Alpha");
        rows[0].Activo.Should().BeTrue();
    }
}
