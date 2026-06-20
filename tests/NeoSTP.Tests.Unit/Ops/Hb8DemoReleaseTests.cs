using FluentAssertions;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>HB-8 - el preflight, runbook y evidencia de demo/release permanecen operativos.</summary>
public class Hb8DemoReleaseTests
{
    private static string RepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NeoSTP.slnx")))
            dir = dir.Parent;

        dir.Should().NotBeNull("la prueba necesita ubicar la raiz del repositorio");
        return Path.Combine(new[] { dir!.FullName }.Concat(segments).ToArray());
    }

    [Fact]
    public void Hb8_Preflight_ValidaCodigoSecretosProvidersBuildTestsYServicios()
    {
        var script = File.ReadAllText(RepoPath("scripts", "demo-preflight.ps1"));

        script.Should().Contain("ValidateSet(\"Demo\", \"Release\")");
        script.Should().Contain("StaticOnly");
        script.Should().Contain("[switch]$Restore");
        script.Should().Contain("RequireServices");
        script.Should().Contain("AllowDirtyWorktree");
        script.Should().Contain("security.tracked-secrets");
        script.Should().Contain("security.jwt");
        script.Should().Contain("Scan.Storage.Provider");
        script.Should().Contain("build.solution");
        script.Should().Contain("test.solution");
        script.Should().Contain("/health/ready");
        script.Should().Contain("/openapi/v1.json");
        script.Should().Contain("EvidencePath");
    }

    [Fact]
    public void Hb8_Preflight_GeneraDecisionYNoSerializaValoresSecretos()
    {
        var script = File.ReadAllText(RepoPath("scripts", "demo-preflight.ps1"));

        script.Should().Contain("APTO_DEMO");
        script.Should().Contain("APTO_RELEASE");
        script.Should().Contain("APTO_CON_ADVERTENCIAS");
        script.Should().Contain("NO_APTO");
        script.Should().Contain("secretos no impresos");
        script.Should().NotContain("providers = $localConfig");
        script.Should().NotContain("jwtKey = $jwtKey");
    }

    [Fact]
    public void Hb8_RunbookYPlantilla_CubrenDemoReleaseBloqueosYEvidencia()
    {
        var runbook = File.ReadAllText(RepoPath("docs", "Runbook-Demo-Release.md"));
        var template = File.ReadAllText(RepoPath("docs", "templates", "Evidencia-Demo-Release.md"));

        runbook.Should().Contain("scripts/demo-preflight.ps1");
        runbook.Should().Contain("Criterios de NO DEMO / NO RELEASE");
        runbook.Should().Contain("Guion Comercial Recomendado");
        runbook.Should().Contain("Demo Tecnica API");
        runbook.Should().Contain("Migraciones");
        runbook.Should().Contain("Backup");
        runbook.Should().Contain("Post-demo / Post-release");
        runbook.Should().Contain("ADMIN");
        runbook.Should().Contain("CONTADOR");
        runbook.Should().Contain("mobile.dte.consulta");

        template.Should().Contain("Decision");
        template.Should().Contain("Commit");
        template.Should().Contain("Providers");
        template.Should().Contain("Health ready");
        template.Should().Contain("TraceId");
        template.Should().Contain("Nunca incluir passwords, tokens, certificados");
    }

    [Fact]
    public void Hb8_DocumentosPrincipales_EnlazanRunbookYPreflight()
    {
        File.ReadAllText(RepoPath("README.md"))
            .Should().Contain("Runbook-Demo-Release.md");
        File.ReadAllText(RepoPath("src", "NeoSTP.Api", "README.md"))
            .Should().Contain("Runbook-Demo-Release.md");
        File.ReadAllText(RepoPath("docs", "Plan-Hallazgos-Bugs-Demo.md"))
            .Should().Contain("scripts/demo-preflight.ps1");
        File.ReadAllText(RepoPath("docs", "Plan-Pruebas-Web-Api-Demos.md"))
            .Should().Contain("scripts/demo-preflight.ps1");
    }
}
