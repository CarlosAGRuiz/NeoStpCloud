using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Unit.Dte;

public sealed class DteCertificateProtectionMigratorTests
{
    [Fact]
    public async Task Inspect_verifies_active_and_historical_envelopes_by_tenant()
    {
        await using var db = Database();
        var protector = Protector();
        var active = protector.ProtectBytes([1, 2, 3], "Empresa:7");
        var history = protector.ProtectBytes([4, 5, 6], "Empresa:7");
        db.DteConfiguracion.Add(new DteConfiguracion
        {
            Id = 70, EmpresaId = 7, CertificadoBlob = active,
        });
        db.DteConfiguracionVersiones.Add(new DteConfiguracionVersion
        {
            Id = 71, EmpresaId = 7, ConfiguracionId = 70,
            Motivo = "SYNTHETIC", CertificadoBlob = history,
        });
        await db.SaveChangesAsync();
        var migrator = new DteCertificateProtectionMigrator(
            db, protector, NullLogger<DteCertificateProtectionMigrator>.Instance);

        var report = await migrator.InspectAsync(requireAllProtected: true);

        report.ActiveTotal.Should().Be(1);
        report.HistoryTotal.Should().Be(1);
        report.Protected.Should().Be(2);
        report.Legacy.Should().Be(0);
    }

    [Fact]
    public async Task Strict_inspection_rejects_remaining_plaintext()
    {
        await using var db = Database();
        db.DteConfiguracion.Add(new DteConfiguracion
        {
            Id = 80, EmpresaId = 8, CertificadoBlob = [1, 2, 3],
        });
        await db.SaveChangesAsync();
        var migrator = new DteCertificateProtectionMigrator(
            db, Protector(), NullLogger<DteCertificateProtectionMigrator>.Instance);

        var action = () => migrator.InspectAsync(requireAllProtected: true);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DTE_CERTIFICATE_LEGACY_REMAINING:1");
    }

    private static NeoStpDbContext Database()
        => new(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cert-protection-{Guid.NewGuid()}")
            .Options);

    private static DataProtectionSecretProtector Protector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("NeoSTP.Tests.CertificateMigration");
        var provider = services.BuildServiceProvider();
        return new DataProtectionSecretProtector(
            provider.GetRequiredService<IDataProtectionProvider>());
    }
}
