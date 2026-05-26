namespace NeoSTP.Application.Auth.Abstractions;

public class AuditoriaEvent
{
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
}

public interface IAuditoriaService
{
    Task RegistrarAsync(AuditoriaEvent evento, CancellationToken ct = default);
}
