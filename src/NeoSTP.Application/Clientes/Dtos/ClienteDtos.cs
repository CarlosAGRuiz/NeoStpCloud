namespace NeoSTP.Application.Clientes.Dtos;

public class ClienteDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string TipoDocumentoCodigo { get; set; } = null!;
    public string? NumeroDocumento { get; set; }
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
    /// <summary>Código del catálogo PAIS (CAT-020). Null = El Salvador.</summary>
    public string? PaisCodigo { get; set; }
    /// <summary>Tipo de persona (CAT-029): 1 natural, 2 jurídica.</summary>
    public int? TipoPersona { get; set; }
    public bool EsExtranjero { get; set; }
    public string? Correo { get; set; }
    public string? Telefono { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
    /// <summary>Etiqueta CRM: VIP | FRECUENTE | null.</summary>
    public string? Etiqueta { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateClienteRequest
{
    public string TipoDocumentoCodigo { get; set; } = "DUI";
    /// <summary>Opcional para clientes extranjeros (con PaisCodigo distinto de El Salvador).</summary>
    public string? NumeroDocumento { get; set; }
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
    /// <summary>Código del catálogo PAIS (CAT-020). Null = El Salvador.</summary>
    public string? PaisCodigo { get; set; }
    /// <summary>Tipo de persona (CAT-029): 1 natural, 2 jurídica.</summary>
    public int? TipoPersona { get; set; }
}

public class UpdateClienteRequest : CreateClienteRequest
{
    public string EstadoCodigo { get; set; } = "ACTIVO";
}
