using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Tesoreria;

/// <summary>
/// Movimiento de una cuenta de tesorería (ingreso o egreso). Puede vincularse a su origen
/// de negocio (planilla, gasto, cobro) para conciliar sin doble captura. Al confirmarse,
/// ajusta el saldo de la cuenta; al anularse, lo revierte.
/// </summary>
public class MovimientoTesoreria : AuditableEntity
{
    public int EmpresaId { get; set; }

    public int CuentaId { get; set; }
    public CuentaTesoreria Cuenta { get; set; } = null!;

    public DateOnly Fecha { get; set; }

    /// <summary>INGRESO | EGRESO.</summary>
    public string Tipo { get; set; } = TiposMovimientoTesoreria.Egreso;

    public decimal Monto { get; set; }
    public string Concepto { get; set; } = null!;
    public string? Referencia { get; set; }

    /// <summary>MANUAL | PLANILLA | GASTO | COMPRA | COBRO.</summary>
    public string Origen { get; set; } = OrigenesMovimientoTesoreria.Manual;

    /// <summary>Id de la entidad de origen (PlanillaPeriodo, ProfitGasto, PagoCliente…), si aplica.</summary>
    public int? OrigenId { get; set; }

    /// <summary>Saldo de la cuenta inmediatamente después de aplicar este movimiento (snapshot).</summary>
    public decimal SaldoResultante { get; set; }

    public string EstadoCodigo { get; set; } = "CONFIRMADO";
}

public static class TiposMovimientoTesoreria
{
    public const string Ingreso = "INGRESO";
    public const string Egreso = "EGRESO";

    public static readonly string[] All = [Ingreso, Egreso];
}

public static class OrigenesMovimientoTesoreria
{
    public const string Manual = "MANUAL";
    public const string Planilla = "PLANILLA";
    public const string Gasto = "GASTO";
    public const string Compra = "COMPRA";
    public const string Cobro = "COBRO";

    public static readonly string[] All = [Manual, Planilla, Gasto, Compra, Cobro];
}

public static class EstadosMovimientoTesoreria
{
    public const string Confirmado = "CONFIRMADO";
    public const string Anulado = "ANULADO";
}
