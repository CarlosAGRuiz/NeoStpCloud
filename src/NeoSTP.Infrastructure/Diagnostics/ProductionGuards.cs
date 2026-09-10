using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>Rechaza cruces de ambiente, mocks y providers activos incompletos.</summary>
public static class ProductionGuards
{
    private sealed record ProviderRule(
        string Key,
        bool Optional,
        IReadOnlyDictionary<string, string[]> Providers);

    // Mantener alineado con AddInfrastructure y PaymentProviderResolver: no recortar valores.
    private static readonly ProviderRule[] ProviderMappings =
    [
        new("Email:Provider", true, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Smtp"] = ["Email:Smtp:Host", "Email:Smtp:Port", "Email:From:Address"]
        }),
        new("Billing:Provider", true, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe"] = ["Billing:Stripe:SecretKey", "Billing:Stripe:PublishableKey", "Billing:Stripe:WebhookSecret"],
            ["MercadoPago"] = ["Billing:MercadoPago:AccessToken", "Billing:MercadoPago:WebhookSecret"],
            ["Wompi"] = ["Billing:Wompi:AppId", "Billing:Wompi:ApiSecret", "Billing:Wompi:WebhookSecret", "Billing:Wompi:BaseUrl", "Billing:Wompi:IdUrl"],
            ["PayPal"] = ["Billing:PayPal:ClientId", "Billing:PayPal:Secret", "Billing:PayPal:WebhookId", "Billing:PayPal:BaseUrl"],
            ["Transferencia"] = ["Billing:Transferencia:Banco", "Billing:Transferencia:TipoCuenta", "Billing:Transferencia:NumeroCuenta", "Billing:Transferencia:Titular"]
        }),
        new("Scan:Provider", true, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gemini"] = ["Scan:Gemini:ApiKey", "Scan:Gemini:Model", "Scan:Gemini:BaseUrl"]
        }),
        new("WhatsApp:Provider", true, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Meta"] = ["WhatsApp:Meta:Token", "WhatsApp:Meta:PhoneNumberId", "WhatsApp:Meta:BaseUrl", "WhatsApp:Meta:ApiVersion", "WhatsApp:Meta:CodigoPaisDefecto"]
        }),
        new("Push:Provider", true, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Fcm"] = ["Push:Fcm:ProjectId", "Push:Fcm:ClientEmail", "Push:Fcm:PrivateKey", "Push:Fcm:TokenUri", "Push:Fcm:BaseUrl"]
        }),
        new("Hacienda:Client", false, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Http"] = ["Hacienda:PruebasBaseUrl", "Hacienda:ProduccionBaseUrl", "Hacienda:TimeoutSeconds"]
        }),
        new("Dte:Signer", false, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pkcs12"] = [],
            ["HaciendaCert"] = []
        }),
    ];

    private static readonly string[] HttpsSettings =
    [
        "Billing:Wompi:BaseUrl",
        "Billing:Wompi:IdUrl",
        "Billing:PayPal:BaseUrl",
        "Scan:Gemini:BaseUrl",
        "WhatsApp:Meta:BaseUrl",
        "Push:Fcm:TokenUri",
        "Push:Fcm:BaseUrl",
        "Hacienda:PruebasBaseUrl",
        "Hacienda:ProduccionBaseUrl"
    ];

    public static void ValidarProvidersDeProduccion(IConfiguration config, IHostEnvironment env)
    {
        if (!env.IsProduction() && !env.IsStaging()) return;

        var invalidos = new List<string>();
        ValidateEnvironmentBoundary(config, env, invalidos);

        var bypass = config["Ops:PermitirMocksEnProduccion"];
        if (bypass is not null && (!bool.TryParse(bypass, out var permitidos) || permitidos))
            invalidos.Add("Ops:PermitirMocksEnProduccion");

        foreach (var rule in ProviderMappings)
        {
            var providerValue = config[rule.Key];
            if (rule.Optional && string.Equals(providerValue, "Disabled", StringComparison.OrdinalIgnoreCase))
                continue;

            var provider = rule.Providers.Keys.FirstOrDefault(candidate =>
                string.Equals(candidate, providerValue, StringComparison.OrdinalIgnoreCase));
            if (provider is null)
            {
                invalidos.Add(rule.Key);
                continue;
            }

            foreach (var key in rule.Providers[provider])
                if (InvalidConfigurationValue(config[key])) invalidos.Add(key);
        }

        ValidateProviderShapes(config, invalidos);
        ValidatePublicSurface(config, invalidos);

        if (invalidos.Count > 0)
            throw new InvalidOperationException(
                $"DEPLOYMENT_CONFIGURATION_INVALID: arranque bloqueado ({string.Join(", ", invalidos.Distinct(StringComparer.Ordinal))}). " +
                "Configura límites de ambiente y providers reales completos o Disabled para capacidades opcionales.");
    }

    private static void ValidateEnvironmentBoundary(
        IConfiguration config,
        IHostEnvironment env,
        ICollection<string> invalidos)
    {
        var expectedId = env.IsProduction() ? "PRODUCTION" : "STAGING";
        var expectedDatabase = env.IsProduction() ? "NeoSTP_Production" : "NeoSTP_Staging";
        if (!string.Equals(config["Deployment:EnvironmentId"], expectedId, StringComparison.Ordinal))
            invalidos.Add("Deployment:EnvironmentId");

        try
        {
            var connection = new SqlConnectionStringBuilder(config.GetConnectionString("NeoStpDb"));
            if (!string.Equals(connection.InitialCatalog, expectedDatabase, StringComparison.OrdinalIgnoreCase))
                invalidos.Add("ConnectionStrings:NeoStpDb");
        }
        catch (Exception)
        {
            invalidos.Add("ConnectionStrings:NeoStpDb");
        }

        if (!TryNormalizeRoot(config["Deployment:DataRoot"], expectedId, out var dataRoot))
            invalidos.Add("Deployment:DataRoot");
        if (dataRoot is null) return;

        ValidateChildPath(config, "DataProtection:KeyRingPath", dataRoot, invalidos, required: true);

        var fileSink = config.GetSection("Serilog:WriteTo").GetChildren()
            .FirstOrDefault(section => string.Equals(section["Name"], "File", StringComparison.OrdinalIgnoreCase));
        if (fileSink is null || !IsChildPath(fileSink["Args:path"], dataRoot))
            invalidos.Add("Serilog:WriteTo:File:Args:path");

        if (string.Equals(config["Scan:Storage:Provider"], "FileSystem", StringComparison.OrdinalIgnoreCase))
            ValidateChildPath(config, "Scan:Storage:Root", dataRoot, invalidos, required: true);
        if (string.Equals(config["Hardening:Backup:StorageProvider"], "LOCAL", StringComparison.OrdinalIgnoreCase))
            ValidateChildPath(config, "Hardening:Backup:LocalPath", dataRoot, invalidos, required: true);
    }

    private static void ValidatePublicSurface(IConfiguration config, ICollection<string> invalidos)
    {
        var jwt = config.GetSection("Jwt");
        if (jwt.Exists())
        {
            var key = jwt["Key"];
            if (InvalidConfigurationValue(key) || Encoding.UTF8.GetByteCount(key!) < 32)
                invalidos.Add("Jwt:Key");
            if (InvalidConfigurationValue(jwt["Issuer"])) invalidos.Add("Jwt:Issuer");
            if (InvalidConfigurationValue(jwt["Audience"])) invalidos.Add("Jwt:Audience");
        }

        var allowedHosts = config["AllowedHosts"];
        if (allowedHosts is not null)
        {
            var hosts = allowedHosts.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (hosts.Length == 0 || hosts.Any(host =>
                    host is "*" or "localhost" or "127.0.0.1" or "::1"
                    || host.Contains('*')))
                invalidos.Add("AllowedHosts");
        }

        var cors = config.GetSection("Cors:AllowedOrigins");
        if (cors.Exists())
        {
            var origins = cors.Get<string[]>() ?? [];
            if (origins.Length == 0 || origins.Any(origin => !IsSecureEndpoint(origin)))
                invalidos.Add("Cors:AllowedOrigins");
        }

        var apiBaseUrl = config["ApiBaseUrl"];
        if (apiBaseUrl is not null && !IsSecureEndpoint(apiBaseUrl))
            invalidos.Add("ApiBaseUrl");
    }

    private static bool IsSecureEndpoint(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.IsDefaultPort
            && uri.Host is not ("localhost" or "127.0.0.1" or "::1");

    private static void ValidateProviderShapes(IConfiguration config, ICollection<string> invalidos)
    {
        var smtpUser = config["Email:Smtp:Username"];
        var smtpPassword = config["Email:Smtp:Password"];
        if (IsSelected(config, "Email:Provider", "Smtp")
            && string.IsNullOrWhiteSpace(smtpUser) != string.IsNullOrWhiteSpace(smtpPassword))
            invalidos.Add(string.IsNullOrWhiteSpace(smtpUser) ? "Email:Smtp:Username" : "Email:Smtp:Password");

        if (IsSelected(config, "Email:Provider", "Smtp")
            && (!int.TryParse(config["Email:Smtp:Port"], out var smtpPort) || smtpPort is < 1 or > 65535))
            invalidos.Add("Email:Smtp:Port");
        if (IsSelected(config, "Hacienda:Client", "Http")
            && (!int.TryParse(config["Hacienda:TimeoutSeconds"], out var timeout) || timeout is < 1 or > 300))
            invalidos.Add("Hacienda:TimeoutSeconds");

        foreach (var key in HttpsSettings)
        {
            var value = config[key];
            if (!string.IsNullOrWhiteSpace(value)
                && (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                invalidos.Add(key);
        }
    }

    private static bool TryNormalizeRoot(string? configuredPath, string expectedId, out string? dataRoot)
    {
        dataRoot = null;
        try
        {
            if (string.IsNullOrWhiteSpace(configuredPath) || !Path.IsPathFullyQualified(configuredPath))
                return false;
            dataRoot = Path.GetFullPath(configuredPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(new DirectoryInfo(dataRoot).Name, expectedId, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            dataRoot = null;
            return false;
        }
    }

    private static void ValidateChildPath(
        IConfiguration config,
        string key,
        string dataRoot,
        ICollection<string> invalidos,
        bool required)
    {
        var value = config[key];
        if ((required && string.IsNullOrWhiteSpace(value)) || (!string.IsNullOrWhiteSpace(value) && !IsChildPath(value, dataRoot)))
            invalidos.Add(key);
    }

    private static bool IsChildPath(string? configuredPath, string dataRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(configuredPath) || !Path.IsPathFullyQualified(configuredPath))
                return false;
            var child = Path.GetFullPath(configuredPath);
            var relative = Path.GetRelativePath(dataRoot, child);
            return relative.Length > 0
                && relative != "."
                && relative != ".."
                && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !Path.IsPathFullyQualified(relative);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsSelected(IConfiguration config, string key, string provider)
        => string.Equals(config[key], provider, StringComparison.OrdinalIgnoreCase);

    private static bool InvalidConfigurationValue(string? value)
        => string.IsNullOrWhiteSpace(value)
            || value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)
            || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}
