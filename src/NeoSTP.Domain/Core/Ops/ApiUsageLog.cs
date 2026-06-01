using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Ops;

/// <summary>
/// Registro de una petición a la API/Web para observabilidad y para el conteo de
/// cuotas (rate limiting por ventana). Una fila por petición contabilizada.
/// </summary>
public class ApiUsageLog : AuditableEntity
{
    /// <summary>Empresa a la que se atribuye la petición (null = anónima / sistema).</summary>
    public int? EmpresaId { get; set; }

    /// <summary>Usuario autenticado (si aplica).</summary>
    public int? UsuarioId { get; set; }

    /// <summary>API Key usada (NeoConnect, futuro). Null si fue sesión de usuario.</summary>
    public int? ApiKeyId { get; set; }

    public string Metodo { get; set; } = null!;
    public string Ruta { get; set; } = null!;

    /// <summary>Módulo lógico al que pertenece el endpoint (NEODTE, CORE, ...).</summary>
    public string? Modulo { get; set; }

    public int StatusCode { get; set; }
    public long DuracionMs { get; set; }
    public string? IpOrigen { get; set; }

    public DateTime OcurrioAt { get; set; } = DateTime.UtcNow;
}
