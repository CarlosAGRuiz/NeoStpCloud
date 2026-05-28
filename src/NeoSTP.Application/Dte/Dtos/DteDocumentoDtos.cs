namespace NeoSTP.Application.Dte.Dtos;

public class DteDocumentoListItemDto
{
    public int Id { get; set; }
    public string TipoDteCodigo { get; set; } = null!;
    public string NumeroControl { get; set; } = null!;
    public string CodigoGeneracion { get; set; } = null!;
    public DateTime FechaEmision { get; set; }
    public string? ReceptorNombre { get; set; }
    public string? ReceptorNumeroDocumento { get; set; }
    public decimal MontoTotalOperacion { get; set; }
    public decimal TotalPagar { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string AmbienteCodigo { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class DteDocumentoDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string TipoDteCodigo { get; set; } = null!;
    public int VersionDte { get; set; }
    public string AmbienteCodigo { get; set; } = null!;
    public string NumeroControl { get; set; } = null!;
    public string CodigoGeneracion { get; set; } = null!;
    public string? SelloRecibido { get; set; }

    public int ModeloFacturacion { get; set; }
    public int TipoTransmision { get; set; }
    public DateTime FechaEmision { get; set; }
    public TimeSpan HoraEmision { get; set; }
    public string TipoMonedaCodigo { get; set; } = "USD";

    public int? ClienteId { get; set; }
    public string? ReceptorTipoDocumento { get; set; }
    public string? ReceptorNumeroDocumento { get; set; }
    public string? ReceptorNrc { get; set; }
    public string? ReceptorNombre { get; set; }
    public string? ReceptorTipoContribuyente { get; set; }
    public string? ReceptorCodigoActividad { get; set; }
    public string? ReceptorActividadEconomica { get; set; }
    public string? ReceptorDepartamentoCodigo { get; set; }
    public string? ReceptorMunicipioCodigo { get; set; }
    public string? ReceptorDireccion { get; set; }
    public string? ReceptorCorreo { get; set; }
    public string? ReceptorTelefono { get; set; }

    public string CondicionOperacionCodigo { get; set; } = "1";
    public string? FormaPagoCodigo { get; set; }
    public int? PlazoDias { get; set; }

    public int? DocumentoRelacionadoId { get; set; }
    public string? NumeroDocumentoRelacionado { get; set; }
    public string? TipoDteRelacionado { get; set; }

    public string? Observaciones { get; set; }

    public decimal TotalNoSujeto { get; set; }
    public decimal TotalExenta { get; set; }
    public decimal TotalGravada { get; set; }
    public decimal SubTotalVentas { get; set; }
    public decimal TotalDescuento { get; set; }
    public decimal IvaTotal { get; set; }
    public decimal IvaRetenido { get; set; }
    public decimal ReteRenta { get; set; }
    public decimal SubTotal { get; set; }
    public decimal MontoTotalOperacion { get; set; }
    public decimal TotalNoGravado { get; set; }
    public decimal TotalPagar { get; set; }
    public string? TotalLetras { get; set; }

    public string EstadoCodigo { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? GeneradoAt { get; set; }
    public DateTime? ValidadoAt { get; set; }
    public DateTime? FirmadoAt { get; set; }
    public DateTime? EnviadoAt { get; set; }
    public DateTime? ProcesadoAt { get; set; }
    public DateTime? RespuestaAt { get; set; }

    public List<DteDocumentoDetalleDto> Detalles { get; set; } = new();
    public string? JsonDte { get; set; }
    public string? JsonFirmado { get; set; }
    public string? RespuestaHacienda { get; set; }
}

public class DteDocumentoDetalleDto
{
    public int Id { get; set; }
    public int NumeroLinea { get; set; }
    public int? ProductoId { get; set; }
    public int TipoItem { get; set; } = 1;
    public string Codigo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public string UnidadMedidaCodigo { get; set; } = "UNIDAD";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal MontoDescuento { get; set; }
    public decimal VentaNoSujeta { get; set; }
    public decimal VentaExenta { get; set; }
    public decimal VentaGravada { get; set; }
    public decimal IvaItem { get; set; }
    public bool NoGravado { get; set; }
    public string? Observaciones { get; set; }
}

public class CreateDteDocumentoRequest
{
    /// <summary>01, 03, 05, 06, 14.</summary>
    public string TipoDteCodigo { get; set; } = "01";
    public int? SucursalId { get; set; }
    public int? PuntoVentaId { get; set; }

    public int? ClienteId { get; set; }
    public ReceptorDto? ReceptorManual { get; set; }

    public string CondicionOperacionCodigo { get; set; } = "1";
    public string? FormaPagoCodigo { get; set; }
    public int? PlazoDias { get; set; }
    public string? TipoMonedaCodigo { get; set; }

    // Documento relacionado (NC/ND)
    public int? DocumentoRelacionadoId { get; set; }
    public string? NumeroDocumentoRelacionado { get; set; }
    public string? TipoDteRelacionado { get; set; }
    public string? TipoGeneracionRelacionado { get; set; }

    public string? Observaciones { get; set; }

    public List<CreateDteDocumentoLineaRequest> Lineas { get; set; } = new();
}

public class CreateDteDocumentoLineaRequest
{
    public int? ProductoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public string UnidadMedidaCodigo { get; set; } = "UNIDAD";
    public int TipoItem { get; set; } = 1;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal MontoDescuento { get; set; }
    /// <summary>EXENTA, NO_SUJETA o GRAVADA (default).</summary>
    public string Clasificacion { get; set; } = "GRAVADA";
    public bool NoGravado { get; set; }
    public string? Observaciones { get; set; }
}

public class ReceptorDto
{
    public string? TipoDocumento { get; set; }
    public string? NumeroDocumento { get; set; }
    public string? Nrc { get; set; }
    public string? Nombre { get; set; }
    public string? TipoContribuyente { get; set; }
    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? DepartamentoCodigo { get; set; }
    public string? MunicipioCodigo { get; set; }
    public string? Direccion { get; set; }
    public string? Correo { get; set; }
    public string? Telefono { get; set; }
}

public class DteListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? TipoDteCodigo { get; set; }
    public string? EstadoCodigo { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

public class DteArchivosDto
{
    public string PdfFileName { get; set; } = null!;
    public byte[] PdfContent { get; set; } = Array.Empty<byte>();
    public string JsonFileName { get; set; } = null!;
    public string JsonContent { get; set; } = null!;
    public string NumeroControl { get; set; } = null!;
}

public class DteReenvioResultDto
{
    public bool Enviado { get; set; }
    public string? Destinatario { get; set; }
    public string? Mensaje { get; set; }
    public string? Detalle { get; set; }
    public string? MessageId { get; set; }
    public DateTime EnviadoAt { get; set; } = DateTime.UtcNow;
}
