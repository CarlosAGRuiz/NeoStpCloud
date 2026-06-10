using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Conta;

/// <summary>
/// Asiento contable (doble partida). Los asientos automáticos se vinculan a su documento
/// origen (Origen + OrigenId) y solo se anulan con REVERSA (asiento espejo), nunca borrado.
/// </summary>
public class AsientoContable : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Correlativo interno por empresa, ej. ASI-000001.</summary>
    public string Numero { get; set; } = null!;

    public DateOnly Fecha { get; set; }
    public string Concepto { get; set; } = null!;

    /// <summary>VENTA_DTE | COBRO | COMPRA | PAGO_PROVEEDOR | GASTO | MANUAL | REVERSA.</summary>
    public string Origen { get; set; } = OrigenesAsiento.Manual;
    public int? OrigenId { get; set; }

    /// <summary>ACTIVO | REVERSADO.</summary>
    public string EstadoCodigo { get; set; } = AsientoEstados.Activo;

    /// <summary>Si este asiento es una reversa, apunta al asiento original.</summary>
    public int? ReversaDeId { get; set; }

    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }

    public ICollection<AsientoContableLinea> Lineas { get; set; } = new List<AsientoContableLinea>();
}

public class AsientoContableLinea : AuditableEntity
{
    public int AsientoContableId { get; set; }
    public AsientoContable Asiento { get; set; } = null!;

    public int CuentaContableId { get; set; }
    public CuentaContable Cuenta { get; set; } = null!;

    public decimal Debe { get; set; }
    public decimal Haber { get; set; }
    public string? Detalle { get; set; }
}

public static class OrigenesAsiento
{
    public const string VentaDte = "VENTA_DTE";
    public const string Cobro = "COBRO";
    public const string Compra = "COMPRA";
    public const string PagoProveedor = "PAGO_PROVEEDOR";
    public const string Gasto = "GASTO";
    public const string Manual = "MANUAL";
    public const string Reversa = "REVERSA";

    public static readonly string[] All = [VentaDte, Cobro, Compra, PagoProveedor, Gasto, Manual, Reversa];
}

public static class AsientoEstados
{
    public const string Activo = "ACTIVO";
    public const string Reversado = "REVERSADO";
}
