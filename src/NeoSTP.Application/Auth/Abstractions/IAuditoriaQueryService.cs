using NeoSTP.Application.Common;

namespace NeoSTP.Application.Auth.Abstractions;

/// <summary>Registro de auditoría para consulta (solo lectura).</summary>
public sealed class AuditoriaDto
{
    public long Id { get; set; }
    public int? EmpresaId { get; set; }
    public int? UsuarioId { get; set; }
    public string? Username { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string? Entidad { get; set; }
    public string? EntidadId { get; set; }
    public string Resultado { get; set; } = "OK";
    public string? Detalle { get; set; }
    public string? IpAddress { get; set; }
    public string? TraceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Filtros de consulta de auditoría. EmpresaId null = todas (solo SuperAdmin).</summary>
public sealed class AuditoriaQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public int? EmpresaId { get; set; }
    public string? Search { get; set; }
    public string? Modulo { get; set; }
    public string? Accion { get; set; }
    public string? Username { get; set; }
    public string? Resultado { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

/// <summary>Consulta de la bitácora de auditoría con filtros, paginación y export (M3.4).</summary>
public interface IAuditoriaQueryService
{
    Task<PagedResult<AuditoriaDto>> ListAsync(AuditoriaQuery query, CancellationToken ct = default);

    /// <summary>Filas que coinciden con el filtro para exportar (sin paginar, con tope de seguridad).</summary>
    Task<IReadOnlyList<AuditoriaDto>> ExportAsync(AuditoriaQuery query, int max = 10000, CancellationToken ct = default);

    /// <summary>Valores distintos de Modulo presentes (para poblar el filtro). Acotado por empresa si aplica.</summary>
    Task<IReadOnlyList<string>> GetModulosAsync(int? empresaId, CancellationToken ct = default);
}
