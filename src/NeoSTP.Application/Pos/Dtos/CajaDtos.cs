using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Pos.Dtos;

public class SesionCajaDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = null!;
    public string EstadoCodigo { get; set; } = null!;
    public int? SucursalId { get; set; }
    public int? PuntoVentaId { get; set; }
    public DateTime AbiertaAt { get; set; }
    public decimal MontoInicial { get; set; }
    public string? AbiertaPor { get; set; }
    public DateTime? CerradaAt { get; set; }
    public decimal? MontoEsperado { get; set; }
    public decimal? MontoContado { get; set; }
    public decimal? Diferencia { get; set; }
    public string? CerradaPor { get; set; }
    public string? Nota { get; set; }

    // Totales del turno (ventas COMPLETADAS ligadas a la sesión).
    public int Ventas { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalEfectivo { get; set; }
    public decimal TotalTarjeta { get; set; }
    public decimal TotalOtros { get; set; }
    /// <summary>Efectivo esperado en vivo (fondo + ventas en efectivo) mientras la caja está abierta.</summary>
    public decimal EfectivoEsperado { get; set; }
}

public class AbrirCajaRequest
{
    [Range(0, 9_999_999)] public decimal MontoInicial { get; set; }
    public int? SucursalId { get; set; }
    public int? PuntoVentaId { get; set; }
    [StringLength(250)] public string? Nota { get; set; }
}

public class CerrarCajaRequest
{
    [Range(0, 9_999_999)] public decimal MontoContado { get; set; }
    [StringLength(250)] public string? Nota { get; set; }
}
