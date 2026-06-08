namespace NeoSTP.Application.Rrhh.Dtos;

/// <summary>Corrida de planilla en el listado.</summary>
public sealed class PlanillaPeriodoDto
{
    public int Id { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int Quincena { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string EstadoCodigo { get; set; } = "BORRADOR";
    public int Empleados { get; set; }
    public decimal TotalNeto { get; set; }
    public decimal TotalCostoPatronal { get; set; }
    public string Etiqueta => Quincena == 0 ? $"{Mes:00}/{Anio} (mensual)" : $"{Mes:00}/{Anio} · Q{Quincena}";
}

public sealed class PlanillaDetalleDto
{
    public string EmpleadoCodigo { get; set; } = string.Empty;
    public string EmpleadoNombre { get; set; } = string.Empty;
    public decimal SalarioMensual { get; set; }
    public decimal Devengado { get; set; }
    public decimal Isss { get; set; }
    public decimal Afp { get; set; }
    public decimal Renta { get; set; }
    public decimal OtrosDescuentos { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal SalarioNeto { get; set; }
    public decimal IsssPatronal { get; set; }
    public decimal AfpPatronal { get; set; }
    public decimal CostoPatronal { get; set; }
}

public sealed class PlanillaPeriodoDetalleDto
{
    public int Id { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int Quincena { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string EstadoCodigo { get; set; } = "BORRADOR";
    public int? ProfitGastoId { get; set; }

    public decimal TotalDevengado { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal TotalNeto { get; set; }
    public decimal TotalCostoPatronal { get; set; }

    public List<PlanillaDetalleDto> Detalles { get; set; } = new();

    public string Etiqueta => Quincena == 0 ? $"{Mes:00}/{Anio} (mensual)" : $"{Mes:00}/{Anio} · Q{Quincena}";
}

public sealed class CrearPlanillaRequest
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    /// <summary>0 = mensual, 1 = primera quincena, 2 = segunda quincena.</summary>
    public int Quincena { get; set; } = 1;
}
