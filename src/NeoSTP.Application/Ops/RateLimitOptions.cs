namespace NeoSTP.Application.Ops;

/// <summary>
/// Opciones del rate limiting (sección "Hardening:RateLimit").
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "Hardening:RateLimit";

    /// <summary>
    /// Activa/desactiva el middleware de cuotas y el registro de uso. Default true.
    /// Cuando está activo, cada petición a /api se contabiliza en Core_ApiUsageLog y
    /// se evalúa contra las reglas de Core_ApiQuotas.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
