using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Cobranza;

/// <summary>
/// Cuenta o pasarela de cobro de la empresa (transferencia bancaria, Wompi, Pagadito, ACH…)
/// usada para generar QR/enlaces de pago que la empresa comparte con sus clientes.
/// El panel web administra estas cuentas; la app solo genera y comparte el QR.
/// </summary>
public class CuentaCobro : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>TRANSFERENCIA | WOMPI | PAGADITO | ACH | OTRO.</summary>
    public string Tipo { get; set; } = "TRANSFERENCIA";

    public string Nombre { get; set; } = null!;
    public string? Banco { get; set; }
    public string? NumeroCuenta { get; set; }
    public string? Titular { get; set; }

    /// <summary>
    /// Plantilla de URL de pago (Wompi/Pagadito/etc.). Acepta {monto} y {referencia} como
    /// marcadores que se sustituyen al generar el QR. Si está vacía, el QR lleva un texto
    /// con los datos de transferencia.
    /// </summary>
    public string? UrlPago { get; set; }

    public string EstadoCodigo { get; set; } = "ACTIVO";
}

public static class CuentaCobroTipos
{
    public const string Transferencia = "TRANSFERENCIA";
    public const string Wompi = "WOMPI";
    public const string Pagadito = "PAGADITO";
    public const string Ach = "ACH";
    public const string Otro = "OTRO";

    public static readonly string[] All = [Transferencia, Wompi, Pagadito, Ach, Otro];
}
