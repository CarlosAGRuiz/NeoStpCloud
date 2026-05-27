namespace NeoSTP.Application.Empresas.Dtos;

public class EmpresaDto
{
    public int Id { get; set; }
    public string Nit { get; set; } = null!;
    public string? Nrc { get; set; }
    public string RazonSocial { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? LogoUrl { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVA";
    public DateTime CreatedAt { get; set; }

    // Resumen rápido
    public int Sucursales { get; set; }
    public int PuntosVenta { get; set; }
    public int Usuarios { get; set; }
    public string? PlanActualCodigo { get; set; }
    public string? PlanActualNombre { get; set; }
    public DateTime? PlanFechaFin { get; set; }
}

public class CreateEmpresaRequest
{
    public string Nit { get; set; } = null!;
    public string? Nrc { get; set; }
    public string RazonSocial { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
}

public class UpdateEmpresaRequest
{
    public string? Nrc { get; set; }
    public string RazonSocial { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? LogoUrl { get; set; }
    public string? EstadoCodigo { get; set; }
}

public class AsignarPlanRequest
{
    public int PlanId { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}

public class EmpresaModuloDto
{
    public int ModuloId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; }
    public bool IncluidoEnPlan { get; set; }
    public DateTime? FechaActivacion { get; set; }
}

public class LicenciaDto
{
    public int EmpresaId { get; set; }
    public string EmpresaNombre { get; set; } = null!;
    public string EmpresaEstado { get; set; } = null!;
    public int? PlanId { get; set; }
    public string? PlanCodigo { get; set; }
    public string? PlanNombre { get; set; }
    public DateTime? PlanFechaInicio { get; set; }
    public DateTime? PlanFechaFin { get; set; }
    public string? PlanEstado { get; set; }
    public bool Vigente { get; set; }
    public IReadOnlyList<EmpresaModuloDto> Modulos { get; set; } = Array.Empty<EmpresaModuloDto>();

    public int? LimiteUsuarios { get; set; }
    public int? LimiteSucursales { get; set; }
    public int? LimitePuntosVenta { get; set; }
    public int? LimiteDteMensual { get; set; }

    public int UsuariosUsados { get; set; }
    public int SucursalesUsadas { get; set; }
    public int PuntosVentaUsados { get; set; }
}
