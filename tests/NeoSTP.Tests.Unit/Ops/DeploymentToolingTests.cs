using FluentAssertions;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class DeploymentToolingTests
{
    [Fact]
    public void MigrationRelease_ConfiguresSqlcmdSessionAndRejectsIncompleteArtifact()
    {
        var script = File.ReadAllText(RepoFile("tools", "Database", "New-MigrationRelease.ps1"));

        foreach (var option in new[]
                 {
                     "SET ANSI_NULLS ON;",
                     "SET ANSI_PADDING ON;",
                     "SET ANSI_WARNINGS ON;",
                     "SET ARITHABORT ON;",
                     "SET CONCAT_NULL_YIELDS_NULL ON;",
                     "SET QUOTED_IDENTIFIER ON;",
                     "SET NUMERIC_ROUNDABORT OFF;",
                 })
            script.Should().Contain(option);

        script.Should().Contain("Compare-Object -ReferenceObject $expectedMigrations");
        script.Should().Contain("Generated SQL does not cover the migration manifest");
        script.Should().Contain("sourceTreeDirty = $sourceTreeDirty");
    }

    [Fact]
    public void PriceVatMigration_UsesDynamicSqlForSameBatchBackfills()
    {
        var migration = File.ReadAllText(RepoFile("src",
            "NeoSTP.Infrastructure", "Persistence", "Migrations",
            "20260911184623_P2_PriceVatSemantics.cs"));

        migration.Should().Contain("EXEC(N'UPDATE Crm_CotizacionLineas");
        migration.Should().Contain("EXEC(N'UPDATE Dte_DocumentoDetalles");
    }

    [Fact]
    public void LocalStaging_UsesDedicatedRuntimeLoginAndExplicitWorkerGate()
    {
        var installer = File.ReadAllText(RepoFile("tools", "Deployment", "Install-LocalStaging.ps1"));
        var launcher = File.ReadAllText(RepoFile("tools", "Deployment", "Start-LocalStagingApp.ps1"));

        installer.Should().Contain("$appLogin = 'neostp_staging_app'");
        installer.Should().Contain("WorkerEnabled = $EnableWorker.IsPresent");
        installer.Should().Contain("LOCAL_STAGING_DIRTY_SOURCE_NOT_ALLOWED");
        installer.Should().Contain("LOCAL_STAGING_LISTENER_RELEASE_MISMATCH");
        installer.Should().Contain("Stop-Process -Id $_.Id -Force");
        installer.Should().Contain("$processPath.StartsWith($releasesRoot");
        installer.Should().Contain("LOCAL_STAGING_TASK_NOT_RUNNING");
        installer.Should().Contain("CloudflareIngressConfigured = $cloudflareIngressConfigured");
        installer.Should().NotContain("CloudflareIngressPending = $true");
        installer.Should().Contain("$sqlOutput = & sqlcmd @sqlcmdArgs 2>&1");
        installer.Should().NotContain("& sqlcmd @sqlcmdArgs | Out-Null");
        installer.Should().NotContain("ConnectionString = $runtimeBuilder.ConnectionString");
        launcher.Should().Contain("Unprotect-Text $secrets.DatabasePassword");
        launcher.Should().Contain("$deployment.WorkerEnabled");
    }

    [Fact]
    public void LocalStagingTunnel_IsSeparatedFromProductionAndHasClosedFallback()
    {
        var tunnel = File.ReadAllText(RepoFile("tools", "Deployment", "Configure-LocalStagingTunnel.ps1"));

        tunnel.Should().Contain("$TunnelName = 'neostp-staging-local'");
        tunnel.Should().Contain("service: http://127.0.0.1:$WebPort");
        tunnel.Should().Contain("service: http://127.0.0.1:$ApiPort");
        tunnel.Should().Contain("service: http_status:404");
        tunnel.Should().Contain("route dns --overwrite-dns $tunnelId $hostname");
        tunnel.Should().Contain("CommandLine.Contains($configPath");
        tunnel.Should().Contain("--resolve \"${hostname}:443:$address\"");
        tunnel.Should().NotContain("TunnelName = 'neostp-local'");
    }

    [Fact]
    public void ProductionRelease_IsCleanImmutableAndVerifiableBeforeCutover()
    {
        var generator = File.ReadAllText(RepoFile("tools", "Deployment", "New-WindowsProductionRelease.ps1"));
        var verifier = File.ReadAllText(RepoFile("tools", "Deployment", "Test-WindowsProductionRelease.ps1"));

        generator.Should().Contain("PRODUCTION_RELEASE_DIRTY_SOURCE_NOT_ALLOWED");
        generator.Should().Contain("appsettings.Local*.json");
        generator.Should().Contain("PRODUCTION_RELEASE_FORBIDDEN_ARTIFACT_FOUND");
        generator.Should().Contain("release.manifest.json");
        generator.Should().Contain("NeoSTP.Worker");
        generator.Should().Contain("Move-Item -LiteralPath $temporaryRoot -Destination $finalRoot");

        verifier.Should().Contain("PRODUCTION_RELEASE_MUST_BE_OUTSIDE_REPOSITORY");
        verifier.Should().Contain("PRODUCTION_RELEASE_MIGRATION_HASH_MISMATCH");
        verifier.Should().Contain("PRODUCTION_DATABASE_NAME_INVALID");
        verifier.Should().Contain("$builder.InitialCatalog = $Database");
        verifier.Should().Contain("PRODUCTION_DATABASE_MIGRATIONS_MISMATCH");
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NeoSTP.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the test must execute below the repository root");
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}
