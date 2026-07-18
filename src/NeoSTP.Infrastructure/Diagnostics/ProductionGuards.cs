using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>
/// Verificaciones fail-fast de arranque para ambientes productivos.
/// Evita que un despliegue quede operando con providers Mock en silencio
/// (correo que no se envía, cobros que no se cobran, push que no llega).
/// </summary>
public static class ProductionGuards
{
    /// <summary>Claves de configuración cuyo valor "Mock" es inaceptable en Producción.</summary>
    private static readonly string[] ProviderKeys =
    {
        "Email:Provider",
        "Billing:Provider",
        "Scan:Provider",
        "WhatsApp:Provider",
        "Push:Provider",
    };

    /// <summary>
    /// Lanza <see cref="InvalidOperationException"/> si el ambiente es Producción y algún
    /// provider crítico sigue en Mock. Se puede permitir explícitamente (demo/staging con
    /// nombre Production) con <c>Ops:PermitirMocksEnProduccion = true</c>.
    /// </summary>
    public static void ValidarProvidersDeProduccion(IConfiguration config, IHostEnvironment env)
    {
        if (!env.IsProduction()) return;
        if (config.GetValue<bool>("Ops:PermitirMocksEnProduccion")) return;

        var enMock = ProviderKeys
            .Where(k => string.Equals(config[k], "Mock", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (enMock.Count > 0)
        {
            throw new InvalidOperationException(
                $"Arranque bloqueado: providers en Mock en Producción ({string.Join(", ", enMock)}). " +
                "Configura los providers reales o establece Ops:PermitirMocksEnProduccion=true de forma explícita.");
        }
    }
}
