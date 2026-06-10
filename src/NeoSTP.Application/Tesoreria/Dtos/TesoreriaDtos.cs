using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Tesoreria.Dtos;

public class CuentaTesoreriaDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string TipoCuenta { get; set; } = null!;
    public string? Banco { get; set; }
    public string? NumeroCuenta { get; set; }
    public string MonedaCodigo { get; set; } = "USD";
    public decimal SaldoInicial { get; set; }
    public decimal SaldoActual { get; set; }
    public string EstadoCodigo { get; set; } = null!;
}

public class CuentaTesoreriaDetalleDto : CuentaTesoreriaDto
{
    public List<MovimientoTesoreriaDto> Movimientos { get; set; } = [];
}

public class MovimientoTesoreriaDto
{
    public int Id { get; set; }
    public int CuentaId { get; set; }
    public string CuentaNombre { get; set; } = null!;
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = null!;
    public decimal Monto { get; set; }
    public string Concepto { get; set; } = null!;
    public string? Referencia { get; set; }
    public string Origen { get; set; } = null!;
    public int? OrigenId { get; set; }
    public decimal SaldoResultante { get; set; }
    public string EstadoCodigo { get; set; } = null!;
}

public class TesoreriaResumenDto
{
    public decimal SaldoTotal { get; set; }
    public decimal SaldoBancos { get; set; }
    public decimal SaldoCaja { get; set; }
    public int CuentasActivas { get; set; }
}

public class CreateCuentaTesoreriaRequest
{
    [Required, StringLength(20)]
    public string Codigo { get; set; } = null!;

    [Required, StringLength(120)]
    public string Nombre { get; set; } = null!;

    [Required]
    public string TipoCuenta { get; set; } = "CAJA";

    [StringLength(80)]
    public string? Banco { get; set; }

    [StringLength(40)]
    public string? NumeroCuenta { get; set; }

    public string MonedaCodigo { get; set; } = "USD";

    public decimal SaldoInicial { get; set; }
}

public class UpdateCuentaTesoreriaRequest
{
    [Required, StringLength(120)]
    public string Nombre { get; set; } = null!;

    [Required]
    public string TipoCuenta { get; set; } = "CAJA";

    [StringLength(80)]
    public string? Banco { get; set; }

    [StringLength(40)]
    public string? NumeroCuenta { get; set; }
}

// ── Conciliación bancaria (V2-D4) ───────────────────────────────────────────

public class MovimientoBancarioDto
{
    public int Id { get; set; }
    public int CuentaTesoreriaId { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Referencia { get; set; }
    public string Descripcion { get; set; } = null!;

    /// <summary>Monto con signo: abono &gt; 0, cargo &lt; 0.</summary>
    public decimal Monto { get; set; }

    public string EstadoCodigo { get; set; } = null!;
    public int? MovimientoTesoreriaId { get; set; }
    public string? MovimientoTesoreriaConcepto { get; set; }
    public DateTime? ConciliadoAt { get; set; }
    public string? ConciliadoPor { get; set; }
}

public class SugerenciaConciliacionDto
{
    public int MovimientoBancoId { get; set; }
    public int MovimientoTesoreriaId { get; set; }
    public string MovimientoTesoreriaConcepto { get; set; } = null!;
    public DateOnly MovimientoTesoreriaFecha { get; set; }

    /// <summary>ALTA (referencia coincide o misma fecha) | MEDIA.</summary>
    public string Confianza { get; set; } = null!;

    public int DiferenciaDias { get; set; }
}

public class ConciliacionResumenDto
{
    public int TotalBanco { get; set; }
    public int Conciliados { get; set; }
    public int NoConciliados { get; set; }

    /// <summary>Suma absoluta de las líneas del banco aún sin conciliar.</summary>
    public decimal MontoNoConciliado { get; set; }

    /// <summary>Movimientos internos confirmados de la cuenta sin línea bancaria vinculada.</summary>
    public int InternosSinConciliar { get; set; }
}

public class RegistrarMovimientoRequest
{
    [Required]
    public int CuentaId { get; set; }

    public DateOnly? Fecha { get; set; }

    [Required]
    public string Tipo { get; set; } = "EGRESO";

    [Range(0.01, 9_999_999)]
    public decimal Monto { get; set; }

    [Required, StringLength(200)]
    public string Concepto { get; set; } = null!;

    [StringLength(80)]
    public string? Referencia { get; set; }

    public string Origen { get; set; } = "MANUAL";
    public int? OrigenId { get; set; }
}
