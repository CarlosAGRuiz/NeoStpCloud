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
    private static Dictionary<string, string?> ValidValues(string environment = "Production")
    {
        var staging = string.Equals(environment, "Staging", StringComparison.Ordinal);
        var environmentId = staging ? "STAGING" : "PRODUCTION";
        var database = staging ? "NeoSTP_Staging" : "NeoSTP_Production";
        var dataRoot = Path.Combine(Path.GetTempPath(), "NeoSTP", environment);
        return new Dictionary<string, string?>
        {
            ["Deployment:EnvironmentId"] = environmentId,
            ["Deployment:DataRoot"] = dataRoot,
            ["ConnectionStrings:NeoStpDb"] = $"Server=synthetic.invalid;Database={database};Integrated Security=true;TrustServerCertificate=true",
            ["DataProtection:KeyRingPath"] = Path.Combine(dataRoot, "DataProtection"),
            ["Serilog:WriteTo:0:Name"] = "File",
            ["Serilog:WriteTo:0:Args:path"] = Path.Combine(dataRoot, "logs", "neostp-.log"),
            ["Email:Provider"] = "Smtp",
            ["Email:Smtp:Host"] = "smtp.synthetic.invalid",
            ["Email:Smtp:Port"] = "587",
            ["Email:Smtp:Username"] = "synthetic-user",
            ["Email:Smtp:Password"] = "synthetic-password",
            ["Email:From:Address"] = "noreply@synthetic.invalid",
            ["Billing:Provider"] = "Transferencia",
            ["Billing:Transferencia:Banco"] = "Synthetic Bank",
            ["Billing:Transferencia:TipoCuenta"] = "Checking",
            ["Billing:Transferencia:NumeroCuenta"] = "0000000000",
            ["Billing:Transferencia:Titular"] = "Synthetic Tenant",
            ["Billing:Stripe:SecretKey"] = "synthetic-stripe-secret",
            ["Billing:Stripe:PublishableKey"] = "synthetic-stripe-public",
            ["Billing:Stripe:WebhookSecret"] = "synthetic-stripe-webhook",
            ["Billing:MercadoPago:AccessToken"] = "synthetic-mp-token",
            ["Billing:MercadoPago:WebhookSecret"] = "synthetic-mp-webhook",
            ["Billing:Wompi:AppId"] = "synthetic-wompi-app",
            ["Billing:Wompi:ApiSecret"] = "synthetic-wompi-api",
            ["Billing:Wompi:WebhookSecret"] = "synthetic-wompi-webhook",
            ["Billing:Wompi:BaseUrl"] = "https://wompi.synthetic.invalid",
            ["Billing:Wompi:IdUrl"] = "https://identity.synthetic.invalid",
            ["Billing:PayPal:ClientId"] = "synthetic-paypal-client",
            ["Billing:PayPal:Secret"] = "synthetic-paypal-secret",
            ["Billing:PayPal:WebhookId"] = "synthetic-paypal-webhook",
            ["Billing:PayPal:BaseUrl"] = "https://paypal.synthetic.invalid",
            ["Scan:Provider"] = "Gemini",
            ["Scan:Gemini:ApiKey"] = "synthetic-gemini-key",
            ["Scan:Gemini:Model"] = "synthetic-model",
            ["Scan:Gemini:BaseUrl"] = "https://gemini.synthetic.invalid",
            ["Scan:Storage:Provider"] = "Database",
            ["WhatsApp:Provider"] = "Meta",
            ["WhatsApp:Meta:Token"] = "synthetic-meta-token",
            ["WhatsApp:Meta:PhoneNumberId"] = "0000000000",
            ["WhatsApp:Meta:BaseUrl"] = "https://meta.synthetic.invalid",
            ["WhatsApp:Meta:ApiVersion"] = "v20.0",
            ["WhatsApp:Meta:CodigoPaisDefecto"] = "503",
            ["Push:Provider"] = "Fcm",
            ["Push:Fcm:ProjectId"] = "synthetic-project",
            ["Push:Fcm:ClientEmail"] = "service@synthetic.invalid",
            ["Push:Fcm:PrivateKey"] = "synthetic-private-key",
            ["Push:Fcm:TokenUri"] = "https://oauth.synthetic.invalid/token",
            ["Push:Fcm:BaseUrl"] = "https://fcm.synthetic.invalid",
            ["Hacienda:Client"] = "Http",
            ["Hacienda:PruebasBaseUrl"] = "https://test-hacienda.synthetic.invalid",
            ["Hacienda:ProduccionBaseUrl"] = "https://hacienda.synthetic.invalid",
            ["Hacienda:TimeoutSeconds"] = "30",
            ["Dte:Signer"] = "HaciendaCert",
        };
    }

    private static IConfiguration Config(string environment = "Production", params (string Key, string? Value)[] changes)
    {
        var values = ValidValues(environment);
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
        foreach (var key in new[] { "Email:Provider", "Billing:Provider", "Scan:Provider",
                     "WhatsApp:Provider", "Push:Provider", "Hacienda:Client", "Dte:Signer" })
        {
            var valid = ValidValues()[key];
            foreach (var value in new[] { null, "", " ", "Mock", "mOcK", "synthetic-secret-unknown", $" {valid} " })
                yield return [key, value];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidProviders))]
    public void Deployed_MissingOrFallbackProvider_IsRejectedWithoutExposingValue(string key, string? value)
    {
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(Config(changes: [(key, value)]), Env());

        var error = act.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().Contain("DEPLOYMENT_CONFIGURATION_INVALID").And.Contain(key);
        error.Message.Should().NotContain("synthetic-secret-unknown");
        error.InnerException.Should().BeNull();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Deployed_RegisteredProviders_Pass(string environment)
        => ProductionGuards.ValidarProvidersDeProduccion(Config(environment), Env(environment));

    [Theory]
    [InlineData("Email:Provider")]
    [InlineData("Billing:Provider")]
    [InlineData("Scan:Provider")]
    [InlineData("WhatsApp:Provider")]
    [InlineData("Push:Provider")]
    public void Deployed_OptionalProvider_CanBeExplicitlyDisabled(string key)
        => ProductionGuards.ValidarProvidersDeProduccion(Config(changes: [(key, "Disabled")]), Env());

    [Theory]
    [InlineData("Hacienda:Client")]
    [InlineData("Dte:Signer")]
    public void Deployed_CoreFiscalProvider_CannotBeDisabled(string key)
    {
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(Config(changes: [(key, "Disabled")]), Env());
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData("Email:Smtp:Host")]
    [InlineData("Billing:Transferencia:NumeroCuenta")]
    [InlineData("Scan:Gemini:ApiKey")]
    [InlineData("WhatsApp:Meta:Token")]
    [InlineData("Push:Fcm:PrivateKey")]
    [InlineData("Hacienda:PruebasBaseUrl")]
    public void Deployed_ActiveProviderWithMissingRequiredSetting_IsRejected(string key)
    {
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(Config(changes: [(key, "")]), Env());
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Theory]
    [InlineData("Staging", "PRODUCTION", "NeoSTP_Staging", "Deployment:EnvironmentId")]
    [InlineData("Production", "PRODUCTION", "NeoSTP_Staging", "ConnectionStrings:NeoStpDb")]
    public void Deployed_EnvironmentBoundaryMismatch_IsRejected(
        string environment,
        string environmentId,
        string database,
        string expectedKey)
    {
        var connection = $"Server=synthetic.invalid;Database={database};Integrated Security=true";
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(
            Config(environment, ("Deployment:EnvironmentId", environmentId), ("ConnectionStrings:NeoStpDb", connection)),
            Env(environment));
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{expectedKey}*");
    }

    [Fact]
    public void Deployed_PathOutsideEnvironmentRoot_IsRejected()
    {
        var outside = Path.Combine(Path.GetTempPath(), "NeoSTP", "Other", "keys");
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(
            Config(changes: [("DataProtection:KeyRingPath", outside)]), Env());
        act.Should().Throw<InvalidOperationException>().WithMessage("*DataProtection:KeyRingPath*");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("synthetic-secret-invalid-bool")]
    [InlineData("")]
    public void Deployed_BypassCannotDisableGuard(string bypass)
    {
        var act = () => ProductionGuards.ValidarProvidersDeProduccion(
            Config(changes: [("Ops:PermitirMocksEnProduccion", bypass)]), Env());
        var error = act.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().Contain("Ops:PermitirMocksEnProduccion")
            .And.NotContain("synthetic-secret-invalid-bool");
    }

    [Fact]
    public void Deployed_ExplicitFalseBypass_Passes()
        => ProductionGuards.ValidarProvidersDeProduccion(
            Config(changes: [("Ops:PermitirMocksEnProduccion", "false")]), Env());

    [Theory]
    [InlineData("Development")]
    [InlineData("SyntheticCustomEnvironment")]
    public void OutsideDeployedEnvironments_PreservesMockConfiguration(string environment)
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
        yield return ["Email:Provider", "Disabled", typeof(IEmailSender), typeof(DisabledEmailSender)];
        yield return ["Scan:Provider", "Gemini", typeof(IScanExtractionService), typeof(GeminiScanExtractionService)];
        yield return ["Scan:Provider", "Disabled", typeof(IScanExtractionService), typeof(DisabledScanExtractionService)];
        yield return ["WhatsApp:Provider", "Meta", typeof(IWhatsAppSender), typeof(MetaWhatsAppSender)];
        yield return ["WhatsApp:Provider", "Disabled", typeof(IWhatsAppSender), typeof(DisabledWhatsAppSender)];
        yield return ["Push:Provider", "Fcm", typeof(IPushSender), typeof(FcmPushSender)];
        yield return ["Push:Provider", "Disabled", typeof(IPushSender), typeof(DisabledPushSender)];
        yield return ["Hacienda:Client", "Http", typeof(IHaciendaReceptionClient), typeof(HttpHaciendaReceptionClient)];
        yield return ["Dte:Signer", "Pkcs12", typeof(IDteSignerService), typeof(Pkcs12DteSignerService)];
        yield return ["Dte:Signer", "HaciendaCert", typeof(IDteSignerService), typeof(HaciendaCertMhDteSignerService)];
    }

    [Theory]
    [MemberData(nameof(DiSelections))]
    public void AcceptedSelectionMatchesActualDi_CaseInsensitive(
        string key,
        string value,
        Type contract,
        Type implementation)
    {
        var config = Config(changes: [(key, value.ToLowerInvariant())]);
        ProductionGuards.ValidarProvidersDeProduccion(config, Env());
        RegisterInfrastructure(config).Last(d => d.ServiceType == contract).ImplementationType.Should().Be(implementation);
    }

    [Theory]
    [InlineData("stripe", typeof(StripeBillingProvider))]
    [InlineData("mercadopago", typeof(MercadoPagoBillingProvider))]
    [InlineData("wompi", typeof(WompiBillingProvider))]
    [InlineData("paypal", typeof(PayPalBillingProvider))]
    [InlineData("transferencia", typeof(TransferenciaPaymentProvider))]
    [InlineData("disabled", typeof(DisabledPaymentProvider))]
    public void BillingSelectionMatchesRegisteredProvider(string selection, Type implementation)
    {
        var config = Config(changes: [("Billing:Provider", selection)]);
        ProductionGuards.ValidarProvidersDeProduccion(config, Env());
        RegisterInfrastructure(config).Should().ContainSingle(d =>
            d.ServiceType == typeof(IPaymentProvider) && d.ImplementationType == implementation);
    }

    private static IServiceCollection RegisterInfrastructure(IConfiguration configuration)
    {
        var values = configuration.AsEnumerable()
            .Where(entry => !entry.Key.StartsWith("DataProtection:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        values["ConnectionStrings:NeoStpDb"] =
            "Server=synthetic.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true";
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        return services;
    }
}
