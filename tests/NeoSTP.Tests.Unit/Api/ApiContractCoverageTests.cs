using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NeoSTP.Api.Authorization;
using NeoSTP.Api.Controllers;
using Xunit;

namespace NeoSTP.Tests.Unit.Api;

public class ApiContractCoverageTests
{
    [Fact]
    public void CrmController_EstaProtegidoPorAuthModuloYPermisos()
    {
        typeof(CrmController).GetCustomAttributes().OfType<AuthorizeAttribute>().Should().NotBeEmpty();
        typeof(CrmController).GetCustomAttributes().OfType<AuthorizeAttribute>()
            .Should().Contain(a => a.Policy == $"{RequireModuleAttribute.PolicyPrefix}NEOCRM");

        var actions = typeof(CrmController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().Any(a => a is HttpMethodAttribute))
            .ToList();

        actions.Should().NotBeEmpty();
        actions.All(m => m.GetCustomAttributes().OfType<AuthorizeAttribute>()
            .Any(a => a.Policy is not null && a.Policy.StartsWith(RequirePermisoAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase)))
            .Should().BeTrue();
    }

    [Fact]
    public void ReadmeApi_DocumentaRutasCrm()
    {
        var root = FindRepoRoot();
        var readme = File.ReadAllText(Path.Combine(root, "src", "NeoSTP.Api", "README.md"));

        readme.Should().Contain("/api/crm/resumen");
        readme.Should().Contain("/api/crm/contactos");
        readme.Should().Contain("/api/crm/oportunidades");
        readme.Should().Contain("/api/crm/actividades");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NeoSTP.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("No se encontro la raiz del repo.");
    }
}
