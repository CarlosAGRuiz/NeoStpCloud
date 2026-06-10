using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Reportes;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Shared;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NEOBI fiscal (V2-D1). Proyecta DTE PROCESADOS y facturas de compra del mes a los
/// libros IVA con <see cref="LibroIvaCalculator"/> (puro). Solo lectura; sin tablas nuevas.
/// </summary>
public class ReporteFiscalService : IReporteFiscalService
{
    private readonly NeoStpDbContext _db;

    public ReporteFiscalService(NeoStpDbContext db)
    {
        _db = db;
    }

    public async Task<Result<LibroFiscalDto<VentasConsumidorDiaDto>>> LibroVentasConsumidorAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        if (ValidarPeriodo(anio, mes) is string err) return Result<LibroFiscalDto<VentasConsumidorDiaDto>>.Fail(err, "VALIDATION");
        var filas = LibroIvaCalculator.VentasConsumidor(await VentasAsync(empresaId, anio, mes, ct));
        return Result<LibroFiscalDto<VentasConsumidorDiaDto>>.Ok(new() { Anio = anio, Mes = mes, Filas = filas });
    }

    public async Task<Result<LibroFiscalDto<VentasContribuyenteRowDto>>> LibroVentasContribuyentesAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        if (ValidarPeriodo(anio, mes) is string err) return Result<LibroFiscalDto<VentasContribuyenteRowDto>>.Fail(err, "VALIDATION");
        var filas = LibroIvaCalculator.VentasContribuyentes(await VentasAsync(empresaId, anio, mes, ct));
        return Result<LibroFiscalDto<VentasContribuyenteRowDto>>.Ok(new() { Anio = anio, Mes = mes, Filas = filas });
    }

    public async Task<Result<LibroFiscalDto<ComprasRowDto>>> LibroComprasAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        if (ValidarPeriodo(anio, mes) is string err) return Result<LibroFiscalDto<ComprasRowDto>>.Fail(err, "VALIDATION");
        var filas = LibroIvaCalculator.Compras(await ComprasAsync(empresaId, anio, mes, ct));
        return Result<LibroFiscalDto<ComprasRowDto>>.Ok(new() { Anio = anio, Mes = mes, Filas = filas });
    }

    public async Task<Result<ResumenF07Dto>> ResumenF07Async(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        if (ValidarPeriodo(anio, mes) is string err) return Result<ResumenF07Dto>.Fail(err, "VALIDATION");
        var ventas = await VentasAsync(empresaId, anio, mes, ct);
        var compras = await ComprasAsync(empresaId, anio, mes, ct);
        var f07 = LibroIvaCalculator.F07(
            LibroIvaCalculator.VentasConsumidor(ventas),
            LibroIvaCalculator.VentasContribuyentes(ventas),
            LibroIvaCalculator.Compras(compras));
        return Result<ResumenF07Dto>.Ok(f07);
    }

    // ── CSV ──────────────────────────────────────────────────────────────────

    public async Task<Result<byte[]>> LibroVentasConsumidorCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        var r = await LibroVentasConsumidorAsync(empresaId, anio, mes, ct);
        if (r.IsFailure) return Result<byte[]>.Fail(r.Error!, r.ErrorCode);
        var csv = new CsvExporter("Fecha", "Documentos", "Exentas", "No sujetas", "Gravadas (con IVA)", "Ventas netas", "Débito fiscal");
        foreach (var f in r.Value!.Filas)
            csv.AddRow(f.Fecha.ToString("yyyy-MM-dd"), f.Documentos, F(f.Exentas), F(f.NoSujetas), F(f.GravadasConIva), F(f.VentasNetas), F(f.DebitoFiscal));
        return Result<byte[]>.Ok(csv.ToBytes());
    }

    public async Task<Result<byte[]>> LibroVentasContribuyentesCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        var r = await LibroVentasContribuyentesAsync(empresaId, anio, mes, ct);
        if (r.IsFailure) return Result<byte[]>.Fail(r.Error!, r.ErrorCode);
        var csv = new CsvExporter("Fecha", "Tipo", "Número de control", "Receptor", "NRC", "Exenta", "Venta neta", "Débito fiscal", "Total");
        foreach (var f in r.Value!.Filas)
            csv.AddRow(f.Fecha.ToString("yyyy-MM-dd"), f.TipoDte, f.NumeroControl, f.Receptor, f.ReceptorNrc, F(f.Exenta), F(f.VentaNeta), F(f.DebitoFiscal), F(f.Total));
        return Result<byte[]>.Ok(csv.ToBytes());
    }

    public async Task<Result<byte[]>> LibroComprasCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        var r = await LibroComprasAsync(empresaId, anio, mes, ct);
        if (r.IsFailure) return Result<byte[]>.Fail(r.Error!, r.ErrorCode);
        var csv = new CsvExporter("Fecha", "Documento", "Proveedor", "NRC", "Compras netas", "Crédito fiscal", "IVA no deducible", "Total");
        foreach (var f in r.Value!.Filas)
            csv.AddRow(f.Fecha.ToString("yyyy-MM-dd"), f.NumeroDocumento, f.Proveedor, f.ProveedorNrc, F(f.ComprasNetas), F(f.CreditoFiscal), F(f.IvaNoDeducible), F(f.Total));
        return Result<byte[]>.Ok(csv.ToBytes());
    }

    // ── Fuentes ──────────────────────────────────────────────────────────────

    private async Task<List<VentaFiscalRow>> VentasAsync(int empresaId, int anio, int mes, CancellationToken ct)
    {
        var desde = new DateTime(anio, mes, 1);
        var hasta = desde.AddMonths(1);
        string[] tipos = ["01", "03", "05", "06"];
        var rows = await _db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId
                && d.EstadoCodigo == DteEstadoCodigos.Procesado // INVALIDADO/RECHAZADO quedan fuera
                && d.FechaEmision >= desde && d.FechaEmision < hasta
                && tipos.Contains(d.TipoDteCodigo))
            .Select(d => new
            {
                d.FechaEmision, d.TipoDteCodigo, d.NumeroControl, d.ReceptorNombre, d.ReceptorNrc,
                d.TotalGravada, d.TotalExenta, d.TotalNoSujeto, d.IvaTotal,
            }).ToListAsync(ct);
        return rows.Select(d => new VentaFiscalRow(
            DateOnly.FromDateTime(d.FechaEmision), d.TipoDteCodigo, d.NumeroControl, d.ReceptorNombre, d.ReceptorNrc,
            d.TotalGravada, d.TotalExenta, d.TotalNoSujeto, d.IvaTotal)).ToList();
    }

    private async Task<List<CompraFiscalRow>> ComprasAsync(int empresaId, int anio, int mes, CancellationToken ct)
    {
        var desde = new DateOnly(anio, mes, 1);
        var hasta = desde.AddMonths(1);
        var rows = await _db.FacturasCompra.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId
                && f.EstadoCodigo != FacturaCompraEstados.Anulada
                && f.FechaEmision >= desde && f.FechaEmision < hasta)
            .Select(f => new
            {
                f.FechaEmision, f.NumeroDocumento, Proveedor = f.Proveedor.Nombre, ProveedorNrc = f.Proveedor.Nrc,
                f.Subtotal, f.Iva, f.IvaDeducible,
            }).ToListAsync(ct);
        return rows.Select(f => new CompraFiscalRow(
            f.FechaEmision, f.NumeroDocumento, f.Proveedor, f.ProveedorNrc, f.Subtotal, f.Iva, f.IvaDeducible)).ToList();
    }

    private static string? ValidarPeriodo(int anio, int mes)
        => anio is < 2020 or > 2100 || mes is < 1 or > 12 ? "Período inválido (año 2020-2100, mes 1-12)." : null;

    private static string F(decimal v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
