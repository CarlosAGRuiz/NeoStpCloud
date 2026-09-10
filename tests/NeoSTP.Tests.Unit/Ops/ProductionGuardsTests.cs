using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Scan;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Notificaciones;
using NeoSTP.Infrastructure.Scan;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

public class ProductionGuardsTests
{
    private static Dictionary<string, string?> ValidValues() => new()
    {
        ["Email:Provider"] = "Smtp",
        ["Billing:Provider"] = "Transferencia",
        ["Scan:Provider"] = "Gemini",
        ["WhatsApp:Provider"] = "Meta",
        ["Push:Provider"] = "Fcm",
        ["Hacienda:Client"] = "Http",
        ["Dte:Signer"] = "HaciendaCert",
    };

    private static IConfiguration Config(params (string Key, string? Value)[] changes)
    {
        var values = ValidValues();
        foreach (var (key, value) in changes) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IHostEnvironment Env(string name = "Production")
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(name);
        return env;
    }

    public static IEnumerable<object?[]> InvalidProviders()
    {
        foreach (var (key, valid) in ValidValues())
        foreach (var value in new[] { null, "", " ", "Mock", "mOcK", "Disabled", "synthetic-secret-unknown", $" {valid} " })
            yield return [key, value];
    }

    [Theory]
    [MemberData(nameof(InvalidProviders))]
    public void Production_MissingOrFallbackProvider_IsRejectedWithoutExposingValue(string key, string? value)
    {
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(Config((key, value)), Env());

        var error = act.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().Contain("PRODUCTION_PROVIDERS_INVALID").And.Contain(key);
        error.Message.Should().NotContain("synthetic-secret-unknown");
        error.InnerException.Should().BeNull();
    }

    [Fact]
    public void Production_RegisteredProviders_Pass()
        => ProductionGuards.ValidarProvidersDeProduccion(Config(), Env());

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("synthetic-secret-invalid-bool")]
    [InlineData("")]
    public void Production_BypassCannotDisableGuard_EvenWithValidProviders(string bypass)
    {
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(
            Config(("Ops:PermitirMocksEnProduccion", bypass)), Env());

        var error = act.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().Contain("Ops:PermitirMocksEnProduccion")
            .And.NotContain("synthetic-secret-invalid-bool");
        error.InnerException.Should().BeNull();
    }

    [Fact]
    public void Production_BypassTrueAndMock_ReportsBothInvalidKeys()
    {
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(
            Config(("Ops:PermitirMocksEnProduccion", "true"), ("Email:Provider", "Mock")), Env());
        act.Should().Throw<InvalidOperationException>().WithMessage("*Ops:PermitirMocksEnProduccion*Email:Provider*");
    }

    [Fact]
    public void Production_ExplicitFalseBypass_Passes()
        => ProductionGuards.ValidarProvidersDeProduccion(Config(("Ops:PermitirMocksEnProduccion", "false")), Env());

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Production_BillingSelectionIsRequiredRegardlessOfWorkerActivation(string enabled)
    {
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(
            Config(("Worker:BillingProviderOperations:Enabled", enabled), ("Billing:Provider", null)), Env());
        act.Should().Throw<InvalidOperationException>().WithMessage("*Billing:Provider*");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void OutsideProduction_PreservesMockAndMissingConfiguration(string environment)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Mock",
            ["Ops:PermitirMocksEnProduccion"] = "synthetic-invalid",
        }).Build();
        ProductionGuards.ValidarProvidersDeProduccion(config, Env(environment));
    }

    public static IEnumerable<object[]> DiSelections()
    {
        yield return ["Email:Provider", "Smtp", typeof(IEmailSender), typeof(SmtpEmailSender)];
        yield return ["Scan:Provider", "Gemini", typeof(IScanExtractionService), typeof(GeminiScanExtractionService)];
        yield return ["WhatsApp:Provider", "Meta", typeof(IWhatsAppSender), typeof(MetaWhatsAppSender)];
        yield return ["Push:Provider", "Fcm", typeof(IPushSender), typeof(FcmPushSender)];
        yield return ["Hacienda:Client", "Http", typeof(IHaciendaReceptionClient), typeof(HttpHaciendaReceptionClient)];
        yield return ["Dte:Signer", "Pkcs12", typeof(IDteSignerService), typeof(Pkcs12DteSignerService)];
        yield return ["Dte:Signer", "HaciendaCert", typeof(IDteSignerService), typeof(HaciendaCertMhDteSignerService)];
    }

    [Theory]
    [MemberData(nameof(DiSelections))]
    public void Production_AcceptedSelectionMatchesActualDi_CaseInsensitive(
        string key, string value, Type contract, Type implementation)
    {
        var config = Config((key, value.ToLowerInvariant()));
        ProductionGuards.ValidarProvidersDeProduccion(config, Env());
        RegisterInfrastructure(config).Last(d => d.ServiceType == contract).ImplementationType.Should().Be(implementation);
    }

    [Theory]
    [MemberData(nameof(DiSelections))]
    public void Production_WhitespaceSelectionIsRejected_BecauseActualDiFallsBackToMock(
        string key, string value, Type contract, Type implementation)
    {
        var config = Config((key, $" {value} "));
        var descriptor = RegisterInfrastructure(config).Last(d => d.ServiceType == contract);
        descriptor.ImplementationType.Should().NotBe(implementation);
        descriptor.ImplementationType!.Name.Should().StartWith("Mock");
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(config, Env());
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData("stripe", typeof(StripeBillingProvider))]
    [InlineData("mercadopago", typeof(MercadoPagoBillingProvider))]
    [InlineData("wompi", typeof(WompiBillingProvider))]
    [InlineData("paypal", typeof(PayPalBillingProvider))]
    [InlineData("transferencia", typeof(TransferenciaPaymentProvider))]
    public void Production_BillingSelectionMatchesRegisteredProvider_WithoutConstructingOrEnablingIt(
        string selection, Type implementation)
    {
        var config = Config(("Billing:Provider", selection));
        ProductionGuards.ValidarProvidersDeProduccion(config, Env());
        RegisterInfrastructure(config).Should().ContainSingle(d =>
            d.ServiceType == typeof(IPaymentProvider) && d.ImplementationType == implementation);
        var options = new BillingOptions();
        config.GetSection("Billing").Bind(options);
        options.Checkout.Enabled.Should().BeFalse();
        options.Wompi.IsProduction.Should().BeFalse();
    }

    private static IServiceCollection RegisterInfrastructure(IConfiguration configuration)
    {
        // Inspect registration descriptors only: no service construction, sockets, SQL or secret reads.
        var config = new ConfigurationBuilder().AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:NeoStpDb"] = "Server=synthetic.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true",
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        return services;
    }
}
