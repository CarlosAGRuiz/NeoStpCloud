namespace NeoSTP.Application.Cobranza.Dtos;

/// <summary>Una factura/CCF con saldo pendiente (cuenta por cobrar).</summary>
public sealed class CobroPendienteDto
{
    public int DteDocumentoId { get; set; }
    public string TipoDteCodigo { get; set; } = string.Empty;
    public string NumeroControl { get; set; } = string.Empty;
    public DateOnly FechaEmision { get; set; }
    public DateOnly Vencimiento { get; set; }
    public int? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Pagado { get; set; }
    public decimal Saldo { get; set; }
    /// <summary>PENDIENTE | VENCIDO.</summary>
    public string EstadoCobro { get; set; } = string.Empty;
    public int DiasVencido { get; set; }
}

/// <summary>Resumen de cartera para el dashboard.</summary>
public sealed class CobranzaResumenDto
{
    public decimal TotalPendiente { get; set; }
    public decimal TotalVencido { get; set; }
    public int FacturasPendientes { get; set; }
    public int FacturasVencidas { get; set; }
    public int ClientesConDeuda { get; set; }
}

/// <summary>Saldo consolidado de un cliente + sus facturas pendientes.</summary>
public sealed class SaldoClienteDto
{
    public int? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public decimal TotalPendiente { get; set; }
    public decimal TotalVencido { get; set; }
    public List<CobroPendienteDto> Facturas { get; set; } = [];
}

public sealed class PagoClienteDto
{
    public int Id { get; set; }
    public int DteDocumentoId { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Monto { get; set; }
    public string FormaPagoCodigo { get; set; } = "EFECTIVO";
    public string? Referencia { get; set; }
    public string? Nota { get; set; }
    public string? ComprobanteUrl { get; set; }
    public string EstadoCodigo { get; set; } = "CONFIRMADO";
    public DateTime CreatedAt { get; set; }
}

public sealed class RegistrarPagoRequest
{
    public DateOnly? Fecha { get; set; }
    public decimal Monto { get; set; }
    public string FormaPagoCodigo { get; set; } = "EFECTIVO";
    public string? Referencia { get; set; }
    public string? Nota { get; set; }
    public string? ComprobanteUrl { get; set; }
    /// <summary>Si true, el pago queda PENDIENTE_REVISION (no reduce saldo hasta confirmarse).</summary>
    public bool PendienteRevision { get; set; }
}

/// <summary>Filtros para el listado de cuentas por cobrar.</summary>
public sealed class CobranzaQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public int? ClienteId { get; set; }
    public bool SoloVencidas { get; set; }
}
