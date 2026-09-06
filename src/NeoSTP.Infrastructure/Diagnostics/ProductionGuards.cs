using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>Rechaza selecciones que caerían silenciosamente en un proveedor Mock.</summary>
public static class ProductionGuards
{
    // Mantener alineado con AddInfrastructure y PaymentProviderResolver: no recortar valores.
    // Reconocer un proveedor no habilita sus operaciones ni certifica sus credenciales.
    private static readonly (string Key, string[] Providers)[] ProviderMappings =
    [
        ("Email:Provider", ["Smtp"]),
        ("Billing:Provider", ["Stripe", "MercadoPago", "Wompi", "PayPal", "Transferencia"]),
        ("Scan:Provider", ["Gemini"]),
        ("WhatsApp:Provider", ["Meta"]),
        ("Push:Provider", ["Fcm"]),
        ("Hacienda:Client", ["Http"]),
        ("Dte:Signer", ["Pkcs12", "HaciendaCert"]),
    ];

    public static void ValidarProvidersDeProduccion(IConfiguration config, IHostEnvironment env)
    {
        if (!env.IsProduction()) return;

        var invalidos = new List<string>();
        var bypass = config["Ops:PermitirMocksEnProduccion"];
        if (bypass is not null && (!bool.TryParse(bypass, out var permitidos) || permitidos))
            invalidos.Add("Ops:PermitirMocksEnProduccion");

        foreach (var (key, providers) in ProviderMappings)
        {
            if (!providers.Contains(config[key], StringComparer.OrdinalIgnoreCase))
                invalidos.Add(key);
        }

        if (invalidos.Count > 0)
            throw new InvalidOperationException(
                $"PRODUCTION_PROVIDERS_INVALID: Arranque bloqueado por configuración de proveedores ({string.Join(", ", invalidos)}). " +
                "Configura proveedores reales registrados; no se permite omitir esta validación en Producción.");
    }
}
