using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Tesoreria;

/// <summary>
/// Línea de estado de cuenta bancario importada (CSV/XLSX) para conciliación contra los
/// movimientos internos de tesorería. Monto con signo: positivo = abono (ingreso),
/// negativo = cargo (egreso). Aislado por <see cref="EmpresaId"/>.
/// </summary>
public class MovimientoBancario : AuditableEntity
{
    public int EmpresaId { get; set; }

    public int CuentaTesoreriaId { get; set; }
    public CuentaTesoreria Cuenta { get; set; } = null!;

    public DateOnly Fecha { get; set; }
    public string? Referencia { get; set; }
    public string Descripcion { get; set; } = null!;

    /// <summary>Monto con signo según el banco: abono &gt; 0, cargo &lt; 0.</summary>
    public decimal Monto { get; set; }

    /// <summary>NO_CONCILIADO | PARCIAL | CONCILIADO.</summary>
    public string EstadoCodigo { get; set; } = EstadosConciliacion.NoConciliado;

    /// <summary>
    /// Movimiento interno principal cuando la conciliación es 1:1 (compatibilidad).
    /// El detalle completo (incluido N:1) vive en <see cref="Detalles"/>.
    /// </summary>
    public int? MovimientoTesoreriaId { get; set; }
    public MovimientoTesoreria? MovimientoTesoreria { get; set; }

    public DateTime? ConciliadoAt { get; set; }
    public string? ConciliadoPor { get; set; }

    public ICollection<ConciliacionDetalle> Detalles { get; set; } = new List<ConciliacionDetalle>();
}

/// <summary>
/// Aplicación de un movimiento interno a una línea bancaria (V2.5-S1). Permite que un
/// depósito/cargo agrupado del banco concilie contra varios movimientos de tesorería:
/// la línea queda PARCIAL hasta que la suma de detalles iguala su monto.
/// </summary>
public class ConciliacionDetalle : AuditableEntity
{
    public int EmpresaId { get; set; }

    public int MovimientoBancarioId { get; set; }
    public MovimientoBancario MovimientoBancario { get; set; } = null!;

    public int MovimientoTesoreriaId { get; set; }
    public MovimientoTesoreria MovimientoTesoreria { get; set; } = null!;

    /// <summary>Monto del movimiento interno aplicado (siempre positivo).</summary>
    public decimal Monto { get; set; }
}

public static class EstadosConciliacion
{
    public const string NoConciliado = "NO_CONCILIADO";
    public const string Parcial = "PARCIAL";
    public const string Conciliado = "CONCILIADO";

    public static readonly string[] All = [NoConciliado, Parcial, Conciliado];
}
