using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Tesoreria;

/// <summary>
/// Cuenta de tesorería de la empresa: banco o caja. Mantiene un saldo corriente que se
/// recalcula con cada movimiento confirmado. Aislada por <see cref="EmpresaId"/>.
/// </summary>
public class CuentaTesoreria : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Código corto único por empresa (p. ej. CAJA, BAC-001).</summary>
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;

    /// <summary>BANCO | CAJA.</summary>
    public string TipoCuenta { get; set; } = TiposCuentaTesoreria.Caja;

    public string? Banco { get; set; }
    public string? NumeroCuenta { get; set; }
    public string MonedaCodigo { get; set; } = "USD";

    /// <summary>Saldo de apertura con el que se creó la cuenta.</summary>
    public decimal SaldoInicial { get; set; }

    /// <summary>Saldo corriente: inicial ± movimientos confirmados.</summary>
    public decimal SaldoActual { get; set; }

    public string EstadoCodigo { get; set; } = "ACTIVA";

    public ICollection<MovimientoTesoreria> Movimientos { get; set; } = new List<MovimientoTesoreria>();
}

public static class TiposCuentaTesoreria
{
    public const string Banco = "BANCO";
    public const string Caja = "CAJA";

    public static readonly string[] All = [Caja, Banco];
}

public static class EstadosCuentaTesoreria
{
    public const string Activa = "ACTIVA";
    public const string Inactiva = "INACTIVA";
}
