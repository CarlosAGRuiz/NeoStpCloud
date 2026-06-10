using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Conta;

/// <summary>
/// Cuenta del catálogo contable mínimo (NEOCONTA). Se siembra por empresa al primer uso
/// y puede ampliarse manualmente. Multiempresa estricto.
/// </summary>
public class CuentaContable : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Código corto, ej. 1101.</summary>
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;

    /// <summary>ACTIVO | PASIVO | PATRIMONIO | INGRESO | COSTO | GASTO.</summary>
    public string Tipo { get; set; } = TiposCuentaContable.Activo;

    public bool Activa { get; set; } = true;
}

public static class TiposCuentaContable
{
    public const string Activo = "ACTIVO";
    public const string Pasivo = "PASIVO";
    public const string Patrimonio = "PATRIMONIO";
    public const string Ingreso = "INGRESO";
    public const string Costo = "COSTO";
    public const string Gasto = "GASTO";

    public static readonly string[] All = [Activo, Pasivo, Patrimonio, Ingreso, Costo, Gasto];
}

/// <summary>Códigos del catálogo mínimo sembrado por empresa.</summary>
public static class CuentasContablesMinimas
{
    public const string Efectivo = "1101";
    public const string CuentasPorCobrar = "1102";
    public const string IvaCreditoFiscal = "1103";
    public const string CuentasPorPagar = "2101";
    public const string IvaDebitoFiscal = "2102";
    public const string Ventas = "4101";
    public const string Compras = "5101";
    public const string GastosOperacion = "6101";
}
