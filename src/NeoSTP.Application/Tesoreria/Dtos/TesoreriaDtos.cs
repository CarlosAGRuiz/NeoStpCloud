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
