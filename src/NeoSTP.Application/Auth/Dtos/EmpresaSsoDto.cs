namespace NeoSTP.Application.Auth.Dtos;

/// <summary>Configuración SSO de una empresa expuesta a la UI/admin (E3).</summary>
public sealed class EmpresaSsoDto
{
    public int EmpresaId { get; set; }
    public string ProveedorCodigo { get; set; } = null!;
    public bool Habilitado { get; set; }
    public string DominioCorreo { get; set; } = null!;
    public string? TenantIdExterno { get; set; }
    public bool AutoProvisionar { get; set; }
    public int? RolPorDefectoId { get; set; }
    public string? RolPorDefectoNombre { get; set; }
    public string? Notas { get; set; }
    public bool Configurado { get; set; }
}

/// <summary>Alta/edición de la configuración SSO de una empresa (E3).</summary>
public sealed class GuardarEmpresaSsoRequest
{
    public string ProveedorCodigo { get; set; } = null!;
    public bool Habilitado { get; set; }
    public string DominioCorreo { get; set; } = null!;
    public string? TenantIdExterno { get; set; }
    public bool AutoProvisionar { get; set; }
    public int? RolPorDefectoId { get; set; }
    public string? Notas { get; set; }
}
