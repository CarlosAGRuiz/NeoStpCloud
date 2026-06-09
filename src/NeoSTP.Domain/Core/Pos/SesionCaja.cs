using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Pos;

/// <summary>
/// Sesión / corte de caja del POS. Se abre con un fondo inicial, acumula las ventas del turno
/// y se cierra contando el efectivo (esperado vs contado = diferencia). Aislada por EmpresaId.
/// </summary>
public class SesionCaja : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? SucursalId { get; set; }
    public int? PuntoVentaId { get; set; }

    /// <summary>Correlativo interno por empresa, p. ej. CAJA-000001.</summary>
    public string Numero { get; set; } = null!;

    /// <summary>ABIERTA | CERRADA.</summary>
    public string EstadoCodigo { get; set; } = SesionCajaEstados.Abierta;

    public DateTime AbiertaAt { get; set; } = DateTime.UtcNow;
    public decimal MontoInicial { get; set; }
    public string? AbiertaPor { get; set; }

    public DateTime? CerradaAt { get; set; }
    /// <summary>Efectivo esperado al cierre = fondo inicial + ventas en efectivo.</summary>
    public decimal? MontoEsperado { get; set; }
    /// <summary>Efectivo realmente contado al cierre.</summary>
    public decimal? MontoContado { get; set; }
    /// <summary>Contado − esperado (positivo = sobrante, negativo = faltante).</summary>
    public decimal? Diferencia { get; set; }
    public string? CerradaPor { get; set; }

    public string? Nota { get; set; }
}

public static class SesionCajaEstados
{
    public const string Abierta = "ABIERTA";
    public const string Cerrada = "CERRADA";

    public static readonly string[] All = [Abierta, Cerrada];
}
