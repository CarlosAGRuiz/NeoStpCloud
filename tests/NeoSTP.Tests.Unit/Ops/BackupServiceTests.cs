using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>
/// Sprint 20.4 — almacenamiento local y servicio de respaldos (estados, checksum,
/// manejo de fallo de storage).
/// </summary>
public class BackupServiceTests
{
    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"backup-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = 10, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static LocalStorageService NewLocalStorage(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "neostp-test-" + Guid.NewGuid().ToString("N"));
        return new LocalStorageService(Options.Create(new BackupOptions { LocalPath = dir }));
    }

    [Fact]
    public async Task LocalStorage_GuardarAsync_EscribeArchivoYDevuelveTamano()
    {
        var storage = NewLocalStorage(out var dir);
        try
        {
            var content = Encoding.UTF8.GetBytes("hola mundo");
            using var ms = new MemoryStream(content);

            var result = await storage.GuardarAsync("archivo.txt", ms);

            File.Exists(result.Path).Should().BeTrue();
            result.SizeBytes.Should().Be(content.Length);
            (await File.ReadAllTextAsync(result.Path)).Should().Be("hola mundo");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task EjecutarBackup_ConStorageLocal_CompletaConChecksumYTamano()
    {
        await using var db = NewDb();
        var storage = NewLocalStorage(out var dir);
        try
        {
            var svc = new BackupService(db, storage, Substitute.For<IAuditoriaService>(), NullLogger<BackupService>.Instance);

            var r = await svc.EjecutarBackupAsync(null, "MANUAL", "tester");

            r.IsSuccess.Should().BeTrue();
            r.Value!.EstadoCodigo.Should().Be("COMPLETADO");
            r.Value.Checksum.Should().NotBeNullOrEmpty();
            r.Value.TamanoBytes.Should().BeGreaterThan(0);
            r.Value.StorageProvider.Should().Be("LOCAL");
            File.Exists(r.Value.StoragePath).Should().BeTrue();

            (await svc.ListarAsync()).Should().ContainSingle();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task EjecutarBackup_StorageQueFalla_DejaJobFallido()
    {
        await using var db = NewDb();
        var storage = new ExternalStorageService(Options.Create(new BackupOptions { StorageProvider = "S3" }));
        var svc = new BackupService(db, storage, Substitute.For<IAuditoriaService>(), NullLogger<BackupService>.Instance);

        var r = await svc.EjecutarBackupAsync(null, "MANUAL", "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("BACKUP_FAILED");
        var job = await db.BackupJobs.AsNoTracking().SingleAsync();
        job.EstadoCodigo.Should().Be("FALLIDO");
        job.Error.Should().NotBeNullOrEmpty();
        job.FinalizadoAt.Should().NotBeNull();
    }
}
