namespace NeoSTP.Application.Profit.Dtos;

/// <summary>Rango de fechas para las consultas financieras (inclusivo).</summary>
public sealed class ProfitPeriodoQuery
{
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
}

/// <summary>Dashboard financiero del período: ventas, IVA, costo, ganancia, gastos y utilidad neta.</summary>
public sealed class ProfitDashboardDto
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }

    // Ventas
    public decimal VentasGravadas { get; set; }
    public decimal VentasExentas { get; set; }
    public decimal VentasNoSujetas { get; set; }
    public decimal VentaNeta { get; set; }
    public int Documentos { get; set; }

    // Rentabilidad
    public decimal CostoVentas { get; set; }
    public decimal GananciaBruta { get; set; }
    public decimal MargenPorcentaje { get; set; }
    public int LineasSinCosto { get; set; }

    // Gastos / utilidad
    public decimal GastosTotal { get; set; }
    public decimal UtilidadNeta { get; set; }

    // Compras / IVA
    public decimal ComprasTotal { get; set; }
    public decimal IvaGenerado { get; set; }
    public decimal IvaCredito { get; set; }
    public decimal IvaNeto { get; set; }

    // Detalles
    public List<ProfitProductoDto> TopProductos { get; set; } = [];
    public List<ProfitClienteDto> TopClientes { get; set; } = [];
    public List<ProfitSucursalDto> PorSucursal { get; set; } = [];
    public List<ProfitTendenciaPuntoDto> Tendencia { get; set; } = [];
}

public sealed class ProfitProductoDto
{
    public int? ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal Venta { get; set; }
    public decimal Costo { get; set; }
    public decimal Ganancia { get; set; }
    public decimal MargenPorcentaje { get; set; }
    public bool CostoPendiente { get; set; }
}

public sealed class ProfitClienteDto
{
    public int? ClienteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Documentos { get; set; }
    public decimal Venta { get; set; }
}

public sealed class ProfitSucursalDto
{
    public int? SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Documentos { get; set; }
    public decimal Venta { get; set; }
}

public sealed class ProfitTendenciaPuntoDto
{
    public DateOnly Fecha { get; set; }
    public decimal Venta { get; set; }
    public int Documentos { get; set; }
}

// ─── Gastos ───────────────────────────────────────────────────────────────────

public sealed class ProfitGastoDto
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Proveedor { get; set; }
    public decimal Monto { get; set; }
    public decimal IvaMonto { get; set; }
    public bool IvaDeducible { get; set; }
    public bool ProveedorNoDomiciliado { get; set; }
    public decimal RetencionRentaMonto { get; set; }
    public decimal IvaImportacionMonto { get; set; }
    public decimal Total { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
}

public class CreateProfitGastoRequest
{
    public DateOnly? Fecha { get; set; }
    public string Categoria { get; set; } = "OTROS";
    public string Descripcion { get; set; } = string.Empty;
    public string? Proveedor { get; set; }
    public decimal Monto { get; set; }
    public decimal IvaMonto { get; set; }
    public bool IvaDeducible { get; set; }

    /// <summary>Proveedor no domiciliado (Facebook, Google…): sujeto a ISR 20% art. 158 CT e IVA de importación.</summary>
    public bool ProveedorNoDomiciliado { get; set; }
    /// <summary>ISR retenido al no domiciliado, a enterar a Hacienda.</summary>
    public decimal RetencionRentaMonto { get; set; }
    /// <summary>IVA 13% autoliquidado por la importación del servicio.</summary>
    public decimal IvaImportacionMonto { get; set; }
}

public sealed class UpdateProfitGastoRequest : CreateProfitGastoRequest { }

// ─── Compras ──────────────────────────────────────────────────────────────────

public sealed class ProfitCompraDto
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string? NumeroDocumento { get; set; }
    public string? Descripcion { get; set; }
    public decimal Subtotal { get; set; }
    public decimal IvaMonto { get; set; }
    public decimal Total { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
}

public class CreateProfitCompraRequest
{
    public DateOnly? Fecha { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string? NumeroDocumento { get; set; }
    public string? Descripcion { get; set; }
    public decimal Subtotal { get; set; }
    public decimal IvaMonto { get; set; }
}

public sealed class UpdateProfitCompraRequest : CreateProfitCompraRequest { }
