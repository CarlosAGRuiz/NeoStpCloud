using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NeoSTP.Infrastructure.Diagnostics;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>Entrega 1 (saneamiento) — guard fail-fast de providers Mock en Producción.</summary>
public class ProductionGuardsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => (string?)p.Value))
            .Build();

    private static IHostEnvironment Env(string name)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(name);
        return env;
    }

    [Fact]
    public void Produccion_ConProviderMock_Bloquea()
    {
        var config = Config(("Email:Provider", "Mock"), ("Push:Provider", "Fcm"));

        var act = () => ProductionGuards.ValidarProvidersDeProduccion(config, Env("Production"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Email:Provider*");
    }

    [Fact]
    public void Produccion_ConTodoReal_Pasa()
    {
        var config = Config(("Email:Provider", "Smtp"), ("Billing:Provider", "Stripe"));

        var act = () => ProductionGuards.ValidarProvidersDeProduccion(config, Env("Production"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Produccion_ConOverrideExplicito_Pasa()
    {
        var config = Config(("Email:Provider", "Mock"), ("Ops:PermitirMocksEnProduccion", "true"));

        var act = () => ProductionGuards.ValidarProvidersDeProduccion(config, Env("Production"));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void FueraDeProduccion_MockEsValido(string ambiente)
    {
        var config = Config(("Email:Provider", "Mock"), ("Push:Provider", "Mock"));

        var act = () => ProductionGuards.ValidarProvidersDeProduccion(config, Env(ambiente));

        act.Should().NotThrow();
    }
}
