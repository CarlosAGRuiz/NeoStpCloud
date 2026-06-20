using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Rrhh;

/// <summary>
/// Empleado de la empresa (RRHH/Nómina). Datos personales e identificación; las condiciones
/// laborales (salario, periodicidad) viven en su <see cref="ContratoLaboral"/> vigente.
/// </summary>
public class Empleado : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? SucursalId { get; set; }

    /// <summary>Código interno del empleado (único por empresa).</summary>
    public string Codigo { get; set; } = null!;

    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;

    /// <summary>Tipo de documento (DUI, NIT, PASAPORTE…).</summary>
    public string TipoDocumento { get; set; } = "DUI";
    public string NumeroDocumento { get; set; } = null!;
    public string? Nit { get; set; }

    // Seguridad social
    public string? IsssNumero { get; set; }
    public string? AfpInstitucion { get; set; }
    public string? AfpNumero { get; set; }

    public DateOnly? FechaNacimiento { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public DateOnly? FechaEgreso { get; set; }

    public string? Cargo { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }

    /// <summary>ACTIVO / INACTIVO (soft-delete; al inactivar se fija FechaEgreso).</summary>
    public string EstadoCodigo { get; set; } = "ACTIVO";

    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();

    public ICollection<ContratoLaboral> Contratos { get; set; } = new List<ContratoLaboral>();
    public ICollection<SolicitudVacacion> Vacaciones { get; set; } = new List<SolicitudVacacion>();
    public ICollection<AguinaldoCalculo> Aguinaldos { get; set; } = new List<AguinaldoCalculo>();
}
