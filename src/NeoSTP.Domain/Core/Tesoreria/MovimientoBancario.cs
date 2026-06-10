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

    /// <summary>NO_CONCILIADO | CONCILIADO.</summary>
    public string EstadoCodigo { get; set; } = EstadosConciliacion.NoConciliado;

    /// <summary>Movimiento interno de tesorería con el que se concilió, si aplica.</summary>
    public int? MovimientoTesoreriaId { get; set; }
    public MovimientoTesoreria? MovimientoTesoreria { get; set; }

    public DateTime? ConciliadoAt { get; set; }
    public string? ConciliadoPor { get; set; }
}

public static class EstadosConciliacion
{
    public const string NoConciliado = "NO_CONCILIADO";
    public const string Conciliado = "CONCILIADO";

    public static readonly string[] All = [NoConciliado, Conciliado];
}
