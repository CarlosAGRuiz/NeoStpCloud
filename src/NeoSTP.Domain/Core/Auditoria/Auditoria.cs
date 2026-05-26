namespace NeoSTP.Domain.Core.Auditoria;

public class Auditoria
{
    public long Id { get; set; }
    public int? EmpresaId { get; set; }
    public int? UsuarioId { get; set; }
    public string? Username { get; set; }

    public string Modulo { get; set; } = null!;
    public string Accion { get; set; } = null!;
    public string? Entidad { get; set; }
    public string? EntidadId { get; set; }

    public string? DatosAntes { get; set; }
    public string? DatosDespues { get; set; }

    public string Resultado { get; set; } = "OK";
    public string? Detalle { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? TraceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
