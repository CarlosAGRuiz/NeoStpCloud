using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Pos.Dtos;

public class ImpresoraPosDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Conexion { get; set; } = null!;
    public int AnchoMm { get; set; }
    public string? Ip { get; set; }
    public int Puerto { get; set; }
    public bool CorteAutomatico { get; set; }
    public bool EsPredeterminada { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? Notas { get; set; }
}

public class GuardarImpresoraRequest
{
    [Required, StringLength(80)] public string Nombre { get; set; } = null!;
    [Required] public string Conexion { get; set; } = "NAVEGADOR";
    public int AnchoMm { get; set; } = 80;
    [StringLength(60)] public string? Ip { get; set; }
    public int Puerto { get; set; } = 9100;
    public bool CorteAutomatico { get; set; } = true;
    public bool EsPredeterminada { get; set; }
    [StringLength(250)] public string? Notas { get; set; }
}
