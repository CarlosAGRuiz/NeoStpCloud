namespace NeoSTP.Application.Clientes.Dtos;

public class ClienteDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string TipoDocumentoCodigo { get; set; } = null!;
    public string NumeroDocumento { get; set; } = null!;
    public string? Nrc { get; set; }
    public string Nombre { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string TipoContribuyenteCodigo { get; set; } = null!;
    public bool EsContribuyente { get; set; }
    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? DepartamentoCodigo { get; set; }
    public string? MunicipioCodigo { get; set; }
    public string? Direccion { get; set; }
    public string? Correo { get; set; }
    public string? Telefono { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
    public DateTime CreatedAt { get; set; }
}

public class CreateClienteRequest
{
    public string TipoDocumentoCodigo { get; set; } = "DUI";
    public string NumeroDocumento { get; set; } = null!;
    public string? Nrc { get; set; }
    public string Nombre { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string TipoContribuyenteCodigo { get; set; } = "CONSUMIDOR_FINAL";
    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? DepartamentoCodigo { get; set; }
    public string? MunicipioCodigo { get; set; }
    public string? Direccion { get; set; }
    public string? Correo { get; set; }
    public string? Telefono { get; set; }
}

public class UpdateClienteRequest : CreateClienteRequest
{
    public string EstadoCodigo { get; set; } = "ACTIVO";
}
