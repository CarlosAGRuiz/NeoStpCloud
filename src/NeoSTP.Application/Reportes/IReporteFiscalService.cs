using NeoSTP.Application.Common;

namespace NeoSTP.Application.Reportes;

/// <summary>
/// NEOBI fiscal — libros IVA mensuales y resumen F-07 derivados de datos reales
/// (DTE PROCESADOS y facturas de compra). Aislado por EmpresaId. Export CSV por libro.
/// </summary>
public interface IReporteFiscalService
{
    Task<Result<LibroFiscalDto<VentasConsumidorDiaDto>>> LibroVentasConsumidorAsync(int empresaId, int anio, int mes, CancellationToken ct = default);
    Task<Result<LibroFiscalDto<VentasContribuyenteRowDto>>> LibroVentasContribuyentesAsync(int empresaId, int anio, int mes, CancellationToken ct = default);
    Task<Result<LibroFiscalDto<ComprasRowDto>>> LibroComprasAsync(int empresaId, int anio, int mes, CancellationToken ct = default);
    Task<Result<ResumenF07Dto>> ResumenF07Async(int empresaId, int anio, int mes, CancellationToken ct = default);

    Task<Result<byte[]>> LibroVentasConsumidorCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default);
    Task<Result<byte[]>> LibroVentasContribuyentesCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default);
    Task<Result<byte[]>> LibroComprasCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default);
}
