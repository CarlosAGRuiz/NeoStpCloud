using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Profit;

/// <summary>
/// Gasto operativo registrado manualmente (alquiler, servicios, planilla, etc.).
/// Alimenta el cálculo de utilidad neta de NeoProfit. No es un documento fiscal.
/// </summary>
public class ProfitGasto : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public DateOnly Fecha { get; set; }

    /// <summary>Categoría libre del gasto (ALQUILER, SERVICIOS, PLANILLA, MARKETING, OTROS…).</summary>
    public string Categoria { get; set; } = "OTROS";

    public string Descripcion { get; set; } = null!;
    public string? Proveedor { get; set; }

    /// <summary>Monto del gasto (sin IVA si <see cref="IvaMonto"/> se desglosa aparte).</summary>
    public decimal Monto { get; set; }

    /// <summary>IVA del gasto (si es crédito fiscal deducible).</summary>
    public decimal IvaMonto { get; set; }

    /// <summary>True si el IVA del gasto es deducible (crédito fiscal).</summary>
    public bool IvaDeducible { get; set; }

    /// <summary>ACTIVO / INACTIVO (soft-delete, sin borrado físico).</summary>
    public string EstadoCodigo { get; set; } = "ACTIVO";

    /// <summary>Total del gasto (monto + IVA).</summary>
    public decimal Total => Monto + IvaMonto;
}
