using NeoSTP.Application.Common;

namespace NeoSTP.Application.Ops;

/// <summary>DTO de una regla de cuota para administración.</summary>
public sealed record ApiQuotaDto
{
    public int Id { get; init; }
    public int? EmpresaId { get; init; }
    public string Ambito { get; init; } = null!;
    public string? AmbitoRef { get; init; }
    public int VentanaSegundos { get; init; }
    public int LimitePeticiones { get; init; }
    public bool Activo { get; init; }
    public string? Descripcion { get; init; }
}

/// <summary>Petición para crear una regla de cuota.</summary>
public sealed class CrearApiQuotaRequest
{
    public int? EmpresaId { get; set; }
    public string Ambito { get; set; } = "GLOBAL";
    public string? AmbitoRef { get; set; }
    public int VentanaSegundos { get; set; } = 60;
    public int LimitePeticiones { get; set; }
    public string? Descripcion { get; set; }
}

/// <summary>
/// Datos de la petición entrante necesarios para evaluar cuotas / rate limit.
/// </summary>
public sealed record QuotaContext
{
    public int? EmpresaId { get; init; }
    public int? UsuarioId { get; init; }
    public int? ApiKeyId { get; init; }

    /// <summary>Código de módulo lógico del endpoint (NEODTE, CORE, ...). Null si no se mapea.</summary>
    public string? Modulo { get; init; }

    /// <summary>SuperAdmin queda exento de las cuotas de empresa.</summary>
    public bool IsSuperAdmin { get; init; }
}

/// <summary>
/// Resultado de evaluar las cuotas aplicables a una petición.
/// </summary>
public sealed record QuotaDecision
{
    public bool Allowed { get; init; } = true;

    /// <summary>Límite de la cuota más restrictiva considerada.</summary>
    public int? Limit { get; init; }

    /// <summary>Peticiones restantes en la ventana (mínimo entre cuotas aplicables).</summary>
    public int? Remaining { get; init; }

    /// <summary>Segundos sugeridos para reintentar cuando se rechaza (Retry-After).</summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>Ámbito de la cuota que disparó el rechazo (para diagnóstico).</summary>
    public string? AmbitoExcedido { get; init; }

    public static QuotaDecision Permitir(int? limit = null, int? remaining = null) =>
        new() { Allowed = true, Limit = limit, Remaining = remaining };

    public static QuotaDecision Rechazar(string ambito, int limit, int retryAfter) =>
        new() { Allowed = false, AmbitoExcedido = ambito, Limit = limit, Remaining = 0, RetryAfterSeconds = retryAfter };
}

/// <summary>
/// Entrada para registrar una petición ya atendida (observabilidad + conteo de cuotas).
/// </summary>
public sealed record ApiUsageEntry
{
    public int? EmpresaId { get; init; }
    public int? UsuarioId { get; init; }
    public int? ApiKeyId { get; init; }
    public string Metodo { get; init; } = "GET";
    public string Ruta { get; init; } = "/";
    public string? Modulo { get; init; }
    public int StatusCode { get; init; }
    public long DuracionMs { get; init; }
    public string? IpOrigen { get; init; }
}

/// <summary>
/// Servicio de cuotas de la API (rate limiting por ventana deslizante).
/// Resuelve las reglas <c>Core_ApiQuotas</c> aplicables al contexto, cuenta el uso
/// reciente en <c>Core_ApiUsageLog</c> y decide si la petición se atiende o se rechaza
/// con HTTP 429. El registro de uso es best-effort: nunca debe romper la petición.
/// </summary>
public interface IApiQuotaService
{
    Task<QuotaDecision> EvaluarAsync(QuotaContext ctx, CancellationToken ct = default);
    Task RegistrarUsoAsync(ApiUsageEntry entry, CancellationToken ct = default);

    // -- Administración --
    Task<IReadOnlyList<ApiQuotaDto>> ListarAsync(CancellationToken ct = default);
    Task<Result<ApiQuotaDto>> CrearAsync(CrearApiQuotaRequest request, string? actor, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, string? actor, CancellationToken ct = default);
}
