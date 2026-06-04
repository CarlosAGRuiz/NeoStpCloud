namespace NeoSTP.Application.Notificaciones.Dtos;

public sealed class AlertaDto
{
    public int Id { get; set; }
    public string TipoCodigo { get; set; } = string.Empty;
    public string Severidad { get; set; } = "INFO";
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string? EntidadTipo { get; set; }
    public int? EntidadId { get; set; }
    public string EstadoCodigo { get; set; } = "PENDIENTE";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResueltaAt { get; set; }
}

/// <summary>Conteo de alertas para el badge del dashboard.</summary>
public sealed class AlertaResumenDto
{
    public int Pendientes { get; set; }
    public int Criticas { get; set; }
    public int Advertencias { get; set; }
}

public sealed class AlertaQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    /// <summary>PENDIENTE | LEIDA | RESUELTA. Null = todas excepto resueltas.</summary>
    public string? EstadoCodigo { get; set; }
    public string? TipoCodigo { get; set; }
}

/// <summary>Datos para crear/upsert una alerta (uso interno: generación o módulos).</summary>
public sealed class CrearAlertaRequest
{
    public int EmpresaId { get; set; }
    public int? UsuarioId { get; set; }
    public string TipoCodigo { get; set; } = null!;
    public string Severidad { get; set; } = "INFO";
    public string Titulo { get; set; } = null!;
    public string Mensaje { get; set; } = null!;
    public string? EntidadTipo { get; set; }
    public int? EntidadId { get; set; }
    /// <summary>Clave de deduplicación; si se omite se deriva de TipoCodigo + EntidadId.</summary>
    public string? Clave { get; set; }
}

public sealed class RegistrarDispositivoRequest
{
    public string Token { get; set; } = null!;
    public string Plataforma { get; set; } = "ANDROID";
}

public sealed class PreferenciaNotificacionDto
{
    public string Canal { get; set; } = "PUSH";
    public bool NoMolestar { get; set; }
    public TimeOnly HoraInicio { get; set; } = new(7, 0);
    public TimeOnly HoraFin { get; set; } = new(21, 0);
}
