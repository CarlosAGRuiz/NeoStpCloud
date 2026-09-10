using NeoSTP.Web.Models;

namespace NeoSTP.Tests.Unit.Web;

public class DteEnvironmentBadgeTests
{
    [Theory]
    [InlineData(true, "PRODUCCION", "PRODUCCIÓN · HACIENDA", true)]
    [InlineData(true, "PRUEBAS", "PRUEBAS · HACIENDA", false)]
    [InlineData(true, null, "AMBIENTE SIN CONFIGURAR", false)]
    [InlineData(true, "invalid", "AMBIENTE SIN CONFIGURAR", false)]
    [InlineData(false, "PRODUCCION", "MOCK", false)]
    public void UsesExplicitCompanyEnvironment(bool http, string? ambiente, string label, bool production)
    {
        var badge = DteEnvironmentBadge.Resolve(http, ambiente);
        Assert.Equal(label, badge.Label);
        Assert.Equal(production, badge.IsProduction);
    }
}
