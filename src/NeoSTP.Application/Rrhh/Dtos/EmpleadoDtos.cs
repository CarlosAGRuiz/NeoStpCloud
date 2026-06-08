namespace NeoSTP.Application.Rrhh.Dtos;

/// <summary>Empleado en el listado.</summary>
public sealed class EmpleadoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public decimal SalarioMensual { get; set; }
    public string PeriodicidadPago { get; set; } = "QUINCENAL";
    public string EstadoCodigo { get; set; } = "ACTIVO";
}

/// <summary>Detalle del empleado + contrato vigente + vista previa de nómina mensual.</summary>
public sealed class EmpleadoDetalleDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = "DUI";
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? IsssNumero { get; set; }
    public string? AfpInstitucion { get; set; }
    public string? AfpNumero { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public DateOnly? FechaEgreso { get; set; }
    public string? Cargo { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();

    // Contrato vigente
    public string TipoContrato { get; set; } = "INDEFINIDO";
    public decimal SalarioMensual { get; set; }
    public string PeriodicidadPago { get; set; } = "QUINCENAL";

    /// <summary>Vista previa del cálculo de nómina mensual con el salario vigente.</summary>
    public NominaResultado? NominaPreview { get; set; }
}

public class CreateEmpleadoRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = "DUI";
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? IsssNumero { get; set; }
    public string? AfpInstitucion { get; set; }
    public string? AfpNumero { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public DateOnly? FechaIngreso { get; set; }
    public string? Cargo { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }

    // Contrato inicial
    public string TipoContrato { get; set; } = "INDEFINIDO";
    public decimal SalarioMensual { get; set; }
    public string PeriodicidadPago { get; set; } = "QUINCENAL";
}

public sealed class UpdateEmpleadoRequest : CreateEmpleadoRequest { }
