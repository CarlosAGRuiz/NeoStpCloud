using NeoSTP.Application.Common;

namespace NeoSTP.Application.Conta;

/// <summary>
/// NEOCONTA (116) — base contable mínima auditable: catálogo de cuentas por empresa,
/// asientos automáticos derivados de documentos (idempotentes por Origen+OrigenId),
/// reversa (nunca borrado) y balanza simple por período.
/// </summary>
public interface IContabilidadService
{
    Task<Result<IReadOnlyList<CuentaContableDto>>> ListCuentasAsync(int empresaId, CancellationToken ct = default);

    /// <summary>
    /// Genera los asientos automáticos del período a partir de documentos reales
    /// (ventas DTE procesadas, cobros, compras, pagos a proveedor, gastos).
    /// Idempotente: documentos ya asentados no se duplican. Devuelve cuántos asientos creó.
    /// </summary>
    Task<Result<int>> GenerarAsientosPeriodoAsync(int empresaId, int anio, int mes, string? actor, CancellationToken ct = default);

    Task<Result<PagedResult<AsientoDto>>> ListAsientosAsync(int empresaId, int? anio, int? mes, PagedQuery query, CancellationToken ct = default);
    Task<Result<AsientoDto>> GetAsientoAsync(int empresaId, int id, CancellationToken ct = default);

    /// <summary>Reversa un asiento: crea el asiento espejo (debe↔haber) y marca el original REVERSADO.</summary>
    Task<Result<AsientoDto>> ReversarAsientoAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default);

    Task<Result<BalanzaDto>> BalanzaAsync(int empresaId, int anio, int mes, CancellationToken ct = default);
    Task<Result<byte[]>> BalanzaCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default);

    /// <summary>
    /// Exporta los asientos del período en formato plano (una fila por movimiento) para
    /// importarlos en un sistema contable externo (E7). Incluye las anuladas marcadas como
    /// tales para que el contador vea la reversa, no un descuadre.
    /// </summary>
    Task<Result<byte[]>> AsientosCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed class CuentaContableDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Tipo { get; set; } = null!;
    public bool Activa { get; set; }
}

public sealed class AsientoDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = null!;
    public DateOnly Fecha { get; set; }
    public string Concepto { get; set; } = null!;
    public string Origen { get; set; } = null!;
    public int? OrigenId { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public int? ReversaDeId { get; set; }
    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }
    public List<AsientoLineaDto> Lineas { get; set; } = [];
}

public sealed class AsientoLineaDto
{
    public string CuentaCodigo { get; set; } = null!;
    public string CuentaNombre { get; set; } = null!;
    public decimal Debe { get; set; }
    public decimal Haber { get; set; }
    public string? Detalle { get; set; }
}

public sealed class BalanzaDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public List<BalanzaCuentaDto> Cuentas { get; set; } = [];
    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }
    /// <summary>True si la balanza cuadra (Σdebe == Σhaber).</summary>
    public bool Cuadrada { get; set; }
}

public sealed class BalanzaCuentaDto
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Tipo { get; set; } = null!;
    public decimal Debe { get; set; }
    public decimal Haber { get; set; }
    public decimal SaldoDeudor { get; set; }
    public decimal SaldoAcreedor { get; set; }
}
