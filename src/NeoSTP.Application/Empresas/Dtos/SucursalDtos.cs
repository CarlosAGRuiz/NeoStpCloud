namespace NeoSTP.Application.Empresas.Dtos;

public class SucursalDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? CodigoEstablecimientoMh { get; set; }
    public string? TipoEstablecimientoCodigo { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
    public int PuntosVenta { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSucursalRequest
{
    public int? EmpresaId { get; set; }   // requerido solo para SuperAdmin
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? CodigoEstablecimientoMh { get; set; }
    public string? TipoEstablecimientoCodigo { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
}

public class UpdateSucursalRequest
{
    public string Nombre { get; set; } = null!;
    public string? CodigoEstablecimientoMh { get; set; }
    public string? TipoEstablecimientoCodigo { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
    public string? EstadoCodigo { get; set; }
}

public class PuntoVentaDto
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int EmpresaId { get; set; }
    public string SucursalCodigo { get; set; } = null!;
    public string SucursalNombre { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? CodigoPuntoVentaMh { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
    public DateTime CreatedAt { get; set; }
}

public class CreatePuntoVentaRequest
{
    public int SucursalId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? CodigoPuntoVentaMh { get; set; }
}

public class UpdatePuntoVentaRequest
{
    public string Nombre { get; set; } = null!;
    public string? CodigoPuntoVentaMh { get; set; }
    public string? EstadoCodigo { get; set; }
}
