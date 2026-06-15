using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Scan;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Scan;

/// <summary>V2.5-S4 — storage externo de escaneos y caché distribuida de lookups.</summary>
public class ScanBlobStorageTests : IDisposable
{
    private const int Empresa = 91;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"neostp-scan-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"scanblob-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "X", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private ScanService NewSvc(NeoStpDbContext db, IScanBlobStorage? storage)
    {
        var extraction = Substitute.For<IScanExtractionService>();
        extraction.ExtraerAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ScanExtraccion { Confianza = 0.9m });
        return new ScanService(db, extraction, Substitute.For<IProfitService>(),
            Substitute.For<IAuditoriaService>(), configuration: null, blobStorage: storage);
    }

    [Fact]
    public async Task FileSystemStorage_GuardaYLeePorClaveRelativa()
    {
        var storage = new FileSystemScanBlobStorage(_root);
        var contenido = new byte[] { 1, 2, 3, 4 };

        var clave = await storage.GuardarAsync(Empresa, "ticket.jpg", contenido);

        clave.Should().StartWith($"{Empresa}/").And.EndWith(".jpg").And.NotContain("\\");
        (await storage.LeerAsync(clave)).Should().BeEquivalentTo(contenido);
        (await storage.LeerAsync("../../etc/passwd")).Should().BeNull(); // sin traversal
        (await storage.LeerAsync($"{Empresa}/209901/no-existe.jpg")).Should().BeNull();

        var externo = Path.Combine(Path.GetTempPath(), $"neostp-fuera-{Guid.NewGuid():N}.bin");
        try
        {
            await File.WriteAllBytesAsync(externo, contenido);
            (await storage.LeerAsync(externo)).Should().BeNull(); // sin rutas absolutas fuera del root
        }
        finally
        {
            if (File.Exists(externo)) File.Delete(externo);
        }
    }

    [Fact]
    public async Task Subir_ConStorageExterno_NoGuardaBlobEnBd_YGetArchivoLoResuelve()
    {
        var db = NewDb();
        var storage = new FileSystemScanBlobStorage(_root);
        var svc = NewSvc(db, storage);
        var bytes = new byte[] { 9, 8, 7 };

        var r = await svc.SubirAsync(Empresa, new SubirScanRequest
        {
            ContenidoBase64 = Convert.ToBase64String(bytes),
            ContentType = "image/jpeg",
            Nombre = "captura.jpg",
        }, "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.TieneArchivo.Should().BeTrue();
        var fila = db.ScanDocumentos.Single();
        fila.ArchivoBlob.Should().BeNull();
        fila.ArchivoPath.Should().NotBeNullOrEmpty();

        var archivo = await svc.GetArchivoAsync(Empresa, fila.Id);
        archivo.Should().NotBeNull();
        archivo!.Contenido.Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task Subir_SinStorage_MantieneBlobEnBd()
    {
        var db = NewDb();
        var svc = NewSvc(db, storage: null);

        var r = await svc.SubirAsync(Empresa, new SubirScanRequest
        {
            ContenidoBase64 = Convert.ToBase64String(new byte[] { 5 }),
            Nombre = "x.png",
        }, "tester");

        r.IsSuccess.Should().BeTrue();
        var fila = db.ScanDocumentos.Single();
        fila.ArchivoBlob.Should().NotBeNull();
        fila.ArchivoPath.Should().BeNull();
    }

    [Fact]
    public async Task LookupService_CacheDistribuida_HitYInvalidacionPorVersion()
    {
        var db = NewDb();
        var distributed = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var catalogos = Substitute.For<ICatalogosService>();
        catalogos.GetItemsAsync("CAT-XX", Empresa, null, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CatalogoItemDto>>.Ok(
                [new CatalogoItemDto { Codigo = "1", Valor = "Uno", Activo = true }]));

        // Dos instancias scoped distintas comparten la L2.
        var a = new LookupService(db, catalogos, distributed);
        (await a.GetCatalogoAsync("CAT-XX", Empresa)).Should().ContainSingle(i => i.Label == "Uno");
        var b = new LookupService(db, catalogos, distributed);
        (await b.GetCatalogoAsync("CAT-XX", Empresa)).Should().ContainSingle();
        await catalogos.Received(1).GetItemsAsync("CAT-XX", Empresa, null, Arg.Any<CancellationToken>());

        // Invalidación: versión nueva → la siguiente lectura vuelve a la fuente.
        await new LookupCacheInvalidator(distributed).InvalidarCatalogosAsync();
        var c = new LookupService(db, catalogos, distributed);
        (await c.GetCatalogoAsync("CAT-XX", Empresa)).Should().ContainSingle();
        await catalogos.Received(2).GetItemsAsync("CAT-XX", Empresa, null, Arg.Any<CancellationToken>());
    }
}
