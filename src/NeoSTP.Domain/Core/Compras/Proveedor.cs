using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Compras;

/// <summary>
/// Proveedor de la empresa (maestro de compras / cuentas por pagar). Aislado por EmpresaId.
/// </summary>
public class Proveedor : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Código corto único por empresa.</summary>
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;

    public string? Nit { get; set; }
    public string? Nrc { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }

    /// <summary>Plazo de crédito por defecto en días (0 = contado).</summary>
    public int PlazoDiasDefault { get; set; }

    public string EstadoCodigo { get; set; } = "ACTIVO";

    public ICollection<FacturaCompra> Facturas { get; set; } = new List<FacturaCompra>();
}

public static class ProveedorEstados
{
    public const string Activo = "ACTIVO";
    public const string Inactivo = "INACTIVO";
}
