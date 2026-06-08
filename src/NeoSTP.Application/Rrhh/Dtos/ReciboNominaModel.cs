namespace NeoSTP.Application.Rrhh.Dtos;

/// <summary>Datos para el recibo/boleta de pago de un empleado en un período de planilla.</summary>
public sealed class ReciboNominaModel
{
    public string EmpresaNombre { get; set; } = string.Empty;
    public string PeriodoEtiqueta { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string EstadoCodigo { get; set; } = string.Empty;

    public string EmpleadoCodigo { get; set; } = string.Empty;
    public string EmpleadoNombre { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public string? IsssNumero { get; set; }
    public string? AfpInstitucion { get; set; }
    public string? AfpNumero { get; set; }

    public decimal SalarioMensual { get; set; }
    public decimal Devengado { get; set; }
    public decimal Isss { get; set; }
    public decimal Afp { get; set; }
    public decimal Renta { get; set; }
    public decimal OtrosDescuentos { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal SalarioNeto { get; set; }
}
