using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Productos.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class ProductosService : IProductosService
{
    private const string AuditModule = "PRODUCTOS";
    private static readonly string[] TiposValidos = { "BIEN", "SERVICIO" };

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ProductosService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<ProductoDto>>> GetListAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.Productos.AsNoTracking().Where(p => p.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => EF.Functions.Like(p.Nombre, $"%{s}%")
                          || EF.Functions.Like(p.CodigoInterno, $"%{s}%")
                          || EF.Functions.Like(p.CodigoBarra ?? string.Empty, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(p => p.Nombre)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => MapToDto(p))
            .ToListAsync(ct);

        return Result<PagedResult<ProductoDto>>.Ok(PagedResult<ProductoDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ProductoDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var p = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return p is null
            ? Result<ProductoDto>.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND")
            : Result<ProductoDto>.Ok(MapToDto(p));
    }

    public async Task<Result<ProductoDto>> CreateAsync(int empresaId, CreateProductoRequest request, string? actor, CancellationToken ct = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            return Result<ProductoDto>.Fail("Datos del producto inválidos.", "VALIDATION", errors);

        var codigo = request.CodigoInterno.Trim().ToUpperInvariant();
        var dup = await _db.Productos.AnyAsync(p => p.EmpresaId == empresaId && p.CodigoInterno == codigo, ct);
        if (dup)
            return Result<ProductoDto>.Fail($"Ya existe un producto con código {codigo}.", "PRODUCTO_DUPLICATE");

        var tipo = request.TipoItem.Trim().ToUpperInvariant();
        var producto = new Producto
        {
            EmpresaId = empresaId,
            CodigoInterno = codigo,
            CodigoBarra = string.IsNullOrWhiteSpace(request.CodigoBarra) ? null : request.CodigoBarra.Trim(),
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion,
            TipoItem = tipo,
            UnidadMedidaCodigo = request.UnidadMedidaCodigo.Trim().ToUpperInvariant(),
            PrecioUnitario = request.PrecioUnitario,
            CostoUnitario = request.CostoUnitario,
            AplicaIva = request.AplicaIva,
            TributoCodigo = request.TributoCodigo,
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = DateTime.UtcNow, CreatedBy = actor,
        };
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREATE", "OK", $"Producto {producto.CodigoInterno} creado", producto.Id);

        return Result<ProductoDto>.Ok(MapToDto(producto));
    }

    public async Task<Result<ProductoDto>> UpdateAsync(int empresaId, int id, UpdateProductoRequest request, string? actor, CancellationToken ct = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            return Result<ProductoDto>.Fail("Datos del producto inválidos.", "VALIDATION", errors);

        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId, ct);
        if (producto is null) return Result<ProductoDto>.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND");

        producto.CodigoBarra = string.IsNullOrWhiteSpace(request.CodigoBarra) ? null : request.CodigoBarra.Trim();
        producto.Nombre = request.Nombre.Trim();
        producto.Descripcion = request.Descripcion;
        producto.TipoItem = request.TipoItem.Trim().ToUpperInvariant();
        producto.UnidadMedidaCodigo = request.UnidadMedidaCodigo.Trim().ToUpperInvariant();
        producto.PrecioUnitario = request.PrecioUnitario;
        producto.CostoUnitario = request.CostoUnitario;
        producto.AplicaIva = request.AplicaIva;
        producto.TributoCodigo = request.TributoCodigo;
        if (!string.IsNullOrWhiteSpace(request.EstadoCodigo)) producto.EstadoCodigo = request.EstadoCodigo;
        producto.UpdatedAt = DateTime.UtcNow;
        producto.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPDATE", "OK", $"Producto {producto.CodigoInterno} actualizado", producto.Id);

        return Result<ProductoDto>.Ok(MapToDto(producto));
    }

    public async Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId, ct);
        if (producto is null) return Result.Fail("Producto no encontrado.", "PRODUCTO_NOT_FOUND");

        producto.EstadoCodigo = EstadoCodes.Inactivo;
        producto.UpdatedAt = DateTime.UtcNow;
        producto.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INACTIVAR", "OK", $"Producto {producto.CodigoInterno} inactivado", producto.Id);
        return Result.Ok();
    }

    public async Task<Result<BulkImportResult>> ImportAsync(int empresaId, BulkImportRequest request, string? actor, CancellationToken ct = default)
    {
        IReadOnlyList<TabularRow> rows;
        try { rows = TabularParser.Parse(request.Content, request.Format); }
        catch (Exception ex) { return Result<BulkImportResult>.Fail($"No se pudo leer el archivo: {ex.Message}", "IMPORT_PARSE_ERROR"); }

        var result = new BulkImportResult { DryRun = request.DryRun, Total = rows.Count };
        var existentes = await _db.Productos
            .Where(p => p.EmpresaId == empresaId)
            .ToDictionaryAsync(p => p.CodigoInterno, p => p, ct);

        foreach (var row in rows)
        {
            decimal precio = ParseDecimal(row.Get("precio") ?? row.Get("preciounitario"));
            var costoRaw = row.Get("costo") ?? row.Get("costounitario");
            decimal? costo = costoRaw is null ? null : ParseDecimal(costoRaw);

            var req = new CreateProductoRequest
            {
                CodigoInterno = row.Get("codigo") ?? row.Get("codigointerno") ?? string.Empty,
                CodigoBarra = row.Get("codigobarra"),
                Nombre = row.Get("nombre") ?? string.Empty,
                Descripcion = row.Get("descripcion"),
                TipoItem = row.Get("tipo") ?? row.Get("tipoitem") ?? "BIEN",
                UnidadMedidaCodigo = row.Get("unidadmedida") ?? row.Get("unidadmedidacodigo") ?? "59",
                PrecioUnitario = precio,
                CostoUnitario = costo,
                AplicaIva = ParseBool(row.Get("aplicaiva")) ?? true,
                TributoCodigo = row.Get("tributo") ?? row.Get("tributocodigo"),
            };

            var errors = Validate(req);
            if (errors.Count > 0)
            {
                result.Errors.Add(new BulkImportError { Row = row.RowNumber, Key = req.CodigoInterno, Message = string.Join("; ", errors) });
                continue;
            }

            var codigo = req.CodigoInterno.Trim().ToUpperInvariant();
            if (existentes.TryGetValue(codigo, out var existing))
            {
                if (!request.DryRun) ApplyUpdate(existing, req, actor);
                result.Updated++;
            }
            else
            {
                var nuevo = BuildProducto(empresaId, req, codigo, actor);
                if (!request.DryRun) _db.Productos.Add(nuevo);
                existentes[codigo] = nuevo;
                result.Inserted++;
            }
        }

        if (!request.DryRun && result.ErrorCount < result.Total)
        {
            await _db.SaveChangesAsync(ct);
            await Audit(empresaId, actor, "IMPORT", "OK", $"Carga masiva productos: {result.Inserted} nuevos, {result.Updated} actualizados, {result.ErrorCount} errores", 0);
        }

        return Result<BulkImportResult>.Ok(result);
    }

    private static Producto BuildProducto(int empresaId, CreateProductoRequest req, string codigo, string? actor) => new()
    {
        EmpresaId = empresaId,
        CodigoInterno = codigo,
        CodigoBarra = string.IsNullOrWhiteSpace(req.CodigoBarra) ? null : req.CodigoBarra.Trim(),
        Nombre = req.Nombre.Trim(),
        Descripcion = req.Descripcion,
        TipoItem = req.TipoItem.Trim().ToUpperInvariant(),
        UnidadMedidaCodigo = req.UnidadMedidaCodigo.Trim().ToUpperInvariant(),
        PrecioUnitario = req.PrecioUnitario,
        CostoUnitario = req.CostoUnitario,
        AplicaIva = req.AplicaIva,
        TributoCodigo = req.TributoCodigo,
        EstadoCodigo = EstadoCodes.Activo,
        CreatedAt = DateTime.UtcNow, CreatedBy = actor,
    };

    private static void ApplyUpdate(Producto p, CreateProductoRequest req, string? actor)
    {
        p.CodigoBarra = string.IsNullOrWhiteSpace(req.CodigoBarra) ? null : req.CodigoBarra.Trim();
        p.Nombre = req.Nombre.Trim();
        p.Descripcion = req.Descripcion;
        p.TipoItem = req.TipoItem.Trim().ToUpperInvariant();
        p.UnidadMedidaCodigo = req.UnidadMedidaCodigo.Trim().ToUpperInvariant();
        p.PrecioUnitario = req.PrecioUnitario;
        p.CostoUnitario = req.CostoUnitario;
        p.AplicaIva = req.AplicaIva;
        p.TributoCodigo = req.TributoCodigo;
        p.UpdatedAt = DateTime.UtcNow;
        p.UpdatedBy = actor;
    }

    private static decimal ParseDecimal(string? s)
        => decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static bool? ParseBool(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "si" or "sí" or "yes" or "y" => true,
            "false" or "0" or "no" or "n" => false,
            _ => null,
        };

    private static List<string> Validate(CreateProductoRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.CodigoInterno)) errors.Add("Código interno es obligatorio.");
        if (string.IsNullOrWhiteSpace(r.Nombre)) errors.Add("Nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(r.UnidadMedidaCodigo)) errors.Add("Unidad de medida es obligatoria.");
        if (r.PrecioUnitario < 0) errors.Add("El precio no puede ser negativo.");
        if (r.CostoUnitario is decimal c && c < 0) errors.Add("El costo no puede ser negativo.");
        var tipo = (r.TipoItem ?? "").Trim().ToUpperInvariant();
        if (!TiposValidos.Contains(tipo))
            errors.Add($"Tipo de item inválido: {r.TipoItem}. Debe ser BIEN o SERVICIO.");
        return errors;
    }

    private static ProductoDto MapToDto(Producto p) => new()
    {
        Id = p.Id, EmpresaId = p.EmpresaId,
        CodigoInterno = p.CodigoInterno, CodigoBarra = p.CodigoBarra,
        Nombre = p.Nombre, Descripcion = p.Descripcion,
        TipoItem = p.TipoItem, EsServicio = p.EsServicio,
        UnidadMedidaCodigo = p.UnidadMedidaCodigo,
        PrecioUnitario = p.PrecioUnitario, CostoUnitario = p.CostoUnitario,
        AplicaIva = p.AplicaIva, TributoCodigo = p.TributoCodigo,
        EstadoCodigo = p.EstadoCodigo, CreatedAt = p.CreatedAt,
    };

    private Task Audit(int empresaId, string? actor, string accion, string resultado, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "Producto", EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });
}
