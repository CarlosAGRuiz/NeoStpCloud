using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoSTP.Application.Billing;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Diagnostics;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class BillingDeploymentRegistrationTests
{
    [Fact]
    public void StagingEnvironment_DoesNotRegisterMockPaymentProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:NeoStpDb"] =
                    "Server=synthetic.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true",
                ["Billing:Provider"] = "Disabled"
            }).Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Staging");
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddInfrastructure(configuration, environment);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IPaymentProvider)
            && descriptor.ImplementationType == typeof(DisabledPaymentProvider));
        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(IPaymentProvider)
            && descriptor.ImplementationType == typeof(MockPaymentProvider));
    }
}
