using FluentAssertions;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class DisasterRecoveryScriptSafetyTests
{
    [Fact]
    public void PhysicalBackup_UsesSecretEnvironmentVerificationAndAtomicOffsiteCopy()
    {
        var script = File.ReadAllText(RepoPath("tools", "DisasterRecovery", "New-PhysicalBackup.ps1"));

        script.Should().Contain("NEOSTP_SQLSERVER_ADMIN_CONNECTION");
        script.Should().Contain("RESTORE VERIFYONLY");
        script.Should().Contain("Get-FileHash -Algorithm SHA256");
        script.Should().Contain(".partial");
        script.Should().Contain("OffsiteDirectory must be different");
        script.Should().NotContain("Password=");
        script.Should().NotContain("User Id=sa");
    }

    [Fact]
    public void RestoreDrill_OnlyTargetsSyntheticDatabaseAndVerifiesCleanup()
    {
        var script = File.ReadAllText(RepoPath("tools", "DisasterRecovery", "Invoke-RestoreDrill.ps1"));

        script.Should().Contain("TargetDatabase must start with NeoSTP_Drill_");
        script.Should().Contain("RESTORE VERIFYONLY");
        script.Should().Contain("DBCC CHECKDB");
        script.Should().Contain("already exists; refusing to overwrite");
        script.Should().Contain("cleanupVerified");
        script.Should().NotContain("NeoSTP_Production SET SINGLE_USER");
    }

    [Fact]
    public void CanonicalRunbook_DefinesPhysicalRestoreAndRecoveryGates()
    {
        var document = File.ReadAllText(RepoPath("docs", "DISASTER-RECOVERY.md"));

        document.Should().Contain("RPO").And.Contain("≤ 1 hora");
        document.Should().Contain("RTO").And.Contain("≤ 4 horas");
        document.Should().Contain("DataProtection");
        document.Should().Contain("DBCC CHECKDB");
        document.Should().Contain("MH PRUEBAS");
        document.Should().Contain("no cierran por sí solas el gate productivo");
    }

    private static string RepoPath(params string[] segments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "NeoSTP.slnx")))
                return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
