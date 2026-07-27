using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Datos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Shared;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Exporta los datos de una empresa como ZIP de CSVs (E8). Todo se filtra por EmpresaId:
/// una empresa nunca puede llevarse datos de otra.
/// </summary>
public sealed class PortabilidadService : IPortabilidadService
{
    private const string AuditModule = "DATOS";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public PortabilidadService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<ExportacionDatosDto>> ExportarAsync(
        int empresaId, string? actor, CancellationToken ct = default)
    {
        var empresa = await _db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null)
            return Result<ExportacionDatosDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        var resumen = new Dictionary<string, int>();
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            async Task Agregar(string nombre, CsvExporter csv, int filas)
            {
                var entrada = zip.CreateEntry($"{nombre}.csv", CompressionLevel.Optimal);
                await using var s = entrada.Open();
                var bytes = csv.ToBytes();
                await s.WriteAsync(bytes, ct);
                resumen[nombre] = filas;
            }

            // ── Empresa ───────────────────────────────────────────────────────
            var emp = new CsvExporter("NIT", "NRC", "Razón social", "Nombre comercial",
                "Actividad", "Departamento", "Municipio", "Dirección", "Teléfono", "Correo", "Estado");
            emp.AddRow(empresa.Nit, empresa.Nrc, empresa.RazonSocial, empresa.NombreComercial,
                empresa.ActividadEconomica, empresa.Departamento, empresa.Municipio,
                empresa.Direccion, empresa.Telefono, empresa.Correo, empresa.EstadoCodigo);
            await Agregar("empresa", emp, 1);

            // ── Clientes ──────────────────────────────────────────────────────
            var clientes = await _db.Clientes.AsNoTracking()
                .Where(c => c.EmpresaId == empresaId).OrderBy(c => c.Id).ToListAsync(ct);
            var csvClientes = new CsvExporter("Id", "Tipo documento", "Documento", "NRC", "Nombre",
                "Tipo contribuyente", "Actividad", "Departamento", "Municipio", "Dirección",
                "Correo", "Teléfono", "País", "Etiqueta", "Estado");
            foreach (var c in clientes)
                csvClientes.AddRow(c.Id, c.TipoDocumentoCodigo, c.NumeroDocumento, c.Nrc, c.Nombre,
                    c.TipoContribuyenteCodigo, c.ActividadEconomica, c.DepartamentoCodigo, c.MunicipioCodigo,
                    c.Direccion, c.Correo, c.Telefono, c.PaisCodigo, c.Etiqueta, c.EstadoCodigo);
            await Agregar("clientes", csvClientes, clientes.Count);

            // ── Productos ─────────────────────────────────────────────────────
            var productos = await _db.Productos.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId).OrderBy(p => p.Id).ToListAsync(ct);
            var csvProductos = new CsvExporter("Id", "Código", "Código barra", "Nombre", "Descripción",
                "Tipo", "Unidad", "Categoría", "Precio", "Costo", "Aplica IVA", "Controla lote", "Estado");
            foreach (var p in productos)
                csvProductos.AddRow(p.Id, p.CodigoInterno, p.CodigoBarra, p.Nombre, p.Descripcion,
                    p.TipoItem, p.UnidadMedidaCodigo, p.CategoriaCodigo, F(p.PrecioUnitario), F(p.CostoUnitario),
                    p.AplicaIva ? "SI" : "NO", p.ControlaLote ? "SI" : "NO", p.EstadoCodigo);
            await Agregar("productos", csvProductos, productos.Count);

            // ── DTE (encabezados) ─────────────────────────────────────────────
            var dtes = await _db.DteDocumentos.AsNoTracking()
                .Where(d => d.EmpresaId == empresaId).OrderBy(d => d.Id).ToListAsync(ct);
            var csvDte = new CsvExporter("Id", "Tipo", "Número control", "Código generación", "Sello",
                "Fecha", "Estado", "Condición", "Plazo días", "Receptor", "Documento receptor",
                "Gravada", "Exenta", "IVA", "Total");
            foreach (var d in dtes)
                csvDte.AddRow(d.Id, d.TipoDteCodigo, d.NumeroControl, d.CodigoGeneracion, d.SelloRecibido,
                    d.FechaEmision.ToString("yyyy-MM-dd"), d.EstadoCodigo, d.CondicionOperacionCodigo, d.PlazoDias,
                    d.ReceptorNombre, d.ReceptorNumeroDocumento,
                    F(d.TotalGravada), F(d.TotalExenta), F(d.IvaTotal), F(d.TotalPagar));
            await Agregar("dte_documentos", csvDte, dtes.Count);

            // ── DTE (detalle) ─────────────────────────────────────────────────
            var detalles = await _db.DteDocumentoDetalles.AsNoTracking()
                .Where(x => x.Documento!.EmpresaId == empresaId)
                .OrderBy(x => x.DocumentoId).ThenBy(x => x.NumeroLinea)
                .Select(x => new
                {
                    x.DocumentoId, x.Documento!.NumeroControl, x.NumeroLinea, x.Codigo, x.Descripcion,
                    x.UnidadMedidaCodigo, x.Cantidad, x.PrecioUnitario, x.MontoDescuento,
                    x.VentaGravada, x.VentaExenta, x.VentaNoSujeta,
                })
                .ToListAsync(ct);
            var csvDetalle = new CsvExporter("DTE Id", "Número control", "Línea", "Código", "Descripción",
                "Unidad", "Cantidad", "Precio", "Descuento", "Gravada", "Exenta", "No sujeta");
            foreach (var x in detalles)
                csvDetalle.AddRow(x.DocumentoId, x.NumeroControl, x.NumeroLinea, x.Codigo, x.Descripcion,
                    x.UnidadMedidaCodigo, F(x.Cantidad), F(x.PrecioUnitario), F(x.MontoDescuento),
                    F(x.VentaGravada), F(x.VentaExenta), F(x.VentaNoSujeta));
            await Agregar("dte_detalle", csvDetalle, detalles.Count);

            // ── Inventario ────────────────────────────────────────────────────
            var existencias = await _db.ExistenciasProducto.AsNoTracking()
                .Where(e => e.EmpresaId == empresaId)
                .Select(e => new { e.ProductoId, e.SucursalId, e.Cantidad, e.CostoPromedio, e.StockMinimo })
                .ToListAsync(ct);
            var csvExist = new CsvExporter("Producto Id", "Sucursal Id", "Existencia", "Costo promedio", "Stock mínimo");
            foreach (var e in existencias)
                csvExist.AddRow(e.ProductoId, e.SucursalId, F(e.Cantidad), F(e.CostoPromedio), F(e.StockMinimo));
            await Agregar("inventario_existencias", csvExist, existencias.Count);

            var movimientos = await _db.MovimientosInventario.AsNoTracking()
                .Where(m => m.EmpresaId == empresaId).OrderBy(m => m.Id)
                .Select(m => new
                {
                    m.Id, m.ProductoId, m.SucursalId, m.Fecha, m.Tipo, m.Cantidad,
                    m.CostoUnitario, m.Origen, m.Referencia, m.NumeroLote, m.SaldoCantidad,
                })
                .ToListAsync(ct);
            var csvMov = new CsvExporter("Id", "Producto Id", "Sucursal Id", "Fecha", "Tipo", "Cantidad",
                "Costo unitario", "Origen", "Referencia", "Lote", "Saldo");
            foreach (var m in movimientos)
                csvMov.AddRow(m.Id, m.ProductoId, m.SucursalId, m.Fecha.ToString("yyyy-MM-dd"), m.Tipo,
                    F(m.Cantidad), F(m.CostoUnitario), m.Origen, m.Referencia, m.NumeroLote, F(m.SaldoCantidad));
            await Agregar("inventario_movimientos", csvMov, movimientos.Count);

            // ── Cobros ────────────────────────────────────────────────────────
            var pagos = await _db.Set<Domain.Core.Cobranza.PagoCliente>().AsNoTracking()
                .Where(p => p.EmpresaId == empresaId).OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.DteDocumentoId, p.Fecha, p.Monto, p.FormaPagoCodigo, p.Referencia, p.EstadoCodigo })
                .ToListAsync(ct);
            var csvPagos = new CsvExporter("Id", "DTE Id", "Fecha", "Monto", "Forma de pago", "Referencia", "Estado");
            foreach (var p in pagos)
                csvPagos.AddRow(p.Id, p.DteDocumentoId, p.Fecha.ToString("yyyy-MM-dd"), F(p.Monto),
                    p.FormaPagoCodigo, p.Referencia, p.EstadoCodigo);
            await Agregar("cobros_pagos", csvPagos, pagos.Count);

            // ── Compras ───────────────────────────────────────────────────────
            var proveedores = await _db.Proveedores.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId).OrderBy(p => p.Id).ToListAsync(ct);
            var csvProv = new CsvExporter("Id", "Código", "Nombre", "NIT", "NRC", "Contacto",
                "Teléfono", "Correo", "Dirección", "Estado");
            foreach (var p in proveedores)
                csvProv.AddRow(p.Id, p.Codigo, p.Nombre, p.Nit, p.Nrc, p.Contacto,
                    p.Telefono, p.Email, p.Direccion, p.EstadoCodigo);
            await Agregar("proveedores", csvProv, proveedores.Count);

            var compras = await _db.FacturasCompra.AsNoTracking()
                .Where(f => f.EmpresaId == empresaId).OrderBy(f => f.Id)
                .Select(f => new
                {
                    f.Id, f.ProveedorId, f.NumeroDocumento, f.TipoDocumento, f.FechaEmision,
                    f.Subtotal, f.Iva, f.Total, f.EstadoCodigo,
                })
                .ToListAsync(ct);
            var csvCompras = new CsvExporter("Id", "Proveedor Id", "Documento", "Tipo", "Fecha",
                "Subtotal", "IVA", "Total", "Estado");
            foreach (var f in compras)
                csvCompras.AddRow(f.Id, f.ProveedorId, f.NumeroDocumento, f.TipoDocumento,
                    f.FechaEmision.ToString("yyyy-MM-dd"), F(f.Subtotal), F(f.Iva), F(f.Total), f.EstadoCodigo);
            await Agregar("compras_facturas", csvCompras, compras.Count);

            // ── Guía de lectura ───────────────────────────────────────────────
            var leeme = zip.CreateEntry("LEEME.txt", CompressionLevel.Optimal);
            await using (var s = leeme.Open())
            await using (var w = new StreamWriter(s))
            {
                await w.WriteLineAsync($"Exportación de datos — {empresa.RazonSocial} (NIT {empresa.Nit})");
                await w.WriteLineAsync($"Generada el {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC por NeoSTP Cloud.");
                await w.WriteLineAsync();
                await w.WriteLineAsync("Archivos incluidos (CSV, separador coma, codificación UTF-8 con BOM):");
                foreach (var kv in resumen.OrderBy(k => k.Key))
                    await w.WriteLineAsync($"  {kv.Key}.csv — {kv.Value} fila(s)");
                await w.WriteLineAsync();
                await w.WriteLineAsync("Las columnas 'Id' permiten cruzar archivos entre sí:");
                await w.WriteLineAsync("  dte_detalle.'DTE Id'            -> dte_documentos.Id");
                await w.WriteLineAsync("  inventario_*.'Producto Id'      -> productos.Id");
                await w.WriteLineAsync("  cobros_pagos.'DTE Id'           -> dte_documentos.Id");
                await w.WriteLineAsync("  compras_facturas.'Proveedor Id' -> proveedores.Id");
            }
        }

        await _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = "EXPORTAR_DATOS",
            Entidad = "Empresa",
            EntidadId = empresaId.ToString(),
            Resultado = "OK",
            Detalle = string.Join(", ", resumen.Select(kv => $"{kv.Key}={kv.Value}")),
        });

        var nombre = $"neostp_{Sanitizar(empresa.Nit)}_{DateTime.UtcNow:yyyyMMdd}.zip";
        return Result<ExportacionDatosDto>.Ok(new ExportacionDatosDto
        {
            NombreArchivo = nombre,
            Contenido = buffer.ToArray(),
            Resumen = resumen,
        });
    }

    private static string F(decimal? v) => (v ?? 0m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static string Sanitizar(string valor)
        => new(valor.Where(char.IsLetterOrDigit).ToArray());
}
