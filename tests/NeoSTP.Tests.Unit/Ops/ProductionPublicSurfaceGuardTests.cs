using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NeoSTP.Infrastructure.Diagnostics;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class ProductionPublicSurfaceGuardTests
{
    [Fact]
    public void ValidPublicSurfaceAndDisabledOptionalProviders_Pass()
        => ProductionGuards.ValidarProvidersDeProduccion(Configuration(), Environment());

    [Theory]
    [InlineData("Jwt:Key", "short", "Jwt:Key")]
    [InlineData("Jwt:Key", "REPLACE_WITH_REAL_SECRET", "Jwt:Key")]
    [InlineData("AllowedHosts", "*", "AllowedHosts")]
    [InlineData("AllowedHosts", "localhost", "AllowedHosts")]
    [InlineData("Cors:AllowedOrigins:0", "http://app.neostp.com", "Cors:AllowedOrigins")]
    [InlineData("ApiBaseUrl", "https://localhost", "ApiBaseUrl")]
    public void UnsafePublicSetting_BlocksStartupWithoutEchoingValue(
        string key,
        string value,
        string expectedKey)
    {
        var action = () => ProductionGuards.ValidarProvidersDeProduccion(
            Configuration((key, value)), Environment());

        var error = action.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().Contain(expectedKey).And.NotContain(value);
        error.InnerException.Should().BeNull();
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] changes)
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "NeoSTP", "Production");
        var values = new Dictionary<string, string?>
        {
            ["Deployment:EnvironmentId"] = "PRODUCTION",
            ["Deployment:DataRoot"] = dataRoot,
            ["ConnectionStrings:NeoStpDb"] = "Server=synthetic.invalid;Database=NeoSTP_Production;Integrated Security=true",
            ["DataProtection:KeyRingPath"] = Path.Combine(dataRoot, "DataProtection"),
            ["Serilog:WriteTo:0:Name"] = "File",
            ["Serilog:WriteTo:0:Args:path"] = Path.Combine(dataRoot, "logs", "api-.log"),
            ["Email:Provider"] = "Disabled",
            ["Billing:Provider"] = "Disabled",
            ["Scan:Provider"] = "Disabled",
            ["Scan:Storage:Provider"] = "Database",
            ["WhatsApp:Provider"] = "Disabled",
            ["Push:Provider"] = "Disabled",
            ["Hacienda:Client"] = "Http",
            ["Hacienda:PruebasBaseUrl"] = "https://apitest.dtes.mh.gob.sv",
            ["Hacienda:ProduccionBaseUrl"] = "https://api.dtes.mh.gob.sv",
            ["Hacienda:TimeoutSeconds"] = "30",
            ["Dte:Signer"] = "HaciendaCert",
            ["Jwt:Key"] = "synthetic-key-with-at-least-32-bytes",
            ["Jwt:Issuer"] = "NeoSTP.Cloud",
            ["Jwt:Audience"] = "NeoSTP.Cloud.Clients",
            ["AllowedHosts"] = "api.neostp.com",
            ["Cors:AllowedOrigins:0"] = "https://app.neostp.com",
            ["ApiBaseUrl"] = "https://api.neostp.com"
        };
        foreach (var (key, value) in changes) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IHostEnvironment Environment()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        return environment;
    }
}
