namespace NeoSTP.Application.Pos;

/// <summary>
/// NEOPOS — corte de caja (puro, testeable). Solo el efectivo afecta el conteo del cajón:
/// tarjeta/transferencia/QR no entran en el efectivo esperado.
/// </summary>
public static class CorteCajaCalculator
{
    /// <summary>Efectivo esperado al cierre = fondo inicial + ventas cobradas en efectivo.</summary>
    public static decimal Esperado(decimal montoInicial, decimal ventasEfectivo)
        => decimal.Round(montoInicial + ventasEfectivo, 2, MidpointRounding.AwayFromZero);

    /// <summary>Diferencia = contado − esperado (positivo = sobrante, negativo = faltante).</summary>
    public static decimal Diferencia(decimal contado, decimal esperado)
        => decimal.Round(contado - esperado, 2, MidpointRounding.AwayFromZero);
}
