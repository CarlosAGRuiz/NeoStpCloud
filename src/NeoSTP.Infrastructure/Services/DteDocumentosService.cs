using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class DteDocumentosService : IDteDocumentosService
{
    private const string AuditModule = "DTE_DOCUMENTOS";
    private static readonly string[] TiposSoportados =
    {
        TipoDteCodigos.FacturaConsumidorFinal,
        TipoDteCodigos.ComprobanteCreditoFiscal,
        TipoDteCodigos.NotaCredito,
        TipoDteCodigos.NotaDebito,
        TipoDteCodigos.FacturaSujetoExcluido,
    };

    private readonly NeoStpDbContext _db;
    private readonly IDteCalculator _calculator;
    private readonly IDteGeneratorService _generator;
    private readonly IAuditoriaService _auditoria;

    public DteDocumentosService(
        NeoStpDbContext db,
        IDteCalculator calculator,
        IDteGeneratorService generator,
        IAuditoriaService auditoria)
    {
        _db = db;
        _calculator = calculator;
        _generator = generator;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<DteDocumentoListItemDto>>> GetListAsync(int empresaId, DteListQuery query, CancellationToken ct = default)
    {
        var q = _db.DteDocumentos.AsNoTracking().Where(d => d.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(d => EF.Functions.Like(d.NumeroControl, $"%{s}%")
                          || EF.Functions.Like(d.CodigoGeneracion, $"%{s}%")
                          || EF.Functions.Like(d.ReceptorNombre ?? string.Empty, $"%{s}%")
                          || EF.Functions.Like(d.ReceptorNumeroDocumento ?? string.Empty, $"%{s}%"));
        }
        if (!string.IsNullOrEmpty(query.TipoDteCodigo))
            q = q.Where(d => d.TipoDteCodigo == query.TipoDteCodigo);
        if (!string.IsNullOrEmpty(query.EstadoCodigo))
            q = q.Where(d => d.EstadoCodigo == query.EstadoCodigo);
        if (query.Desde.HasValue)
            q = q.Where(d => d.FechaEmision >= query.Desde.Value.Date);
        if (query.Hasta.HasValue)
            q = q.Where(d => d.FechaEmision <= query.Hasta.Value.Date);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderByDescending(d => d.FechaEmision)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new DteDocumentoListItemDto
            {
                Id = d.Id,
                TipoDteCodigo = d.TipoDteCodigo,
                NumeroControl = d.NumeroControl,
                CodigoGeneracion = d.CodigoGeneracion,
                FechaEmision = d.FechaEmision,
                ReceptorNombre = d.ReceptorNombre,
                ReceptorNumeroDocumento = d.ReceptorNumeroDocumento,
                MontoTotalOperacion = d.MontoTotalOperacion,
                TotalPagar = d.TotalPagar,
                EstadoCodigo = d.EstadoCodigo,
                AmbienteCodigo = d.AmbienteCodigo,
                CreatedAt = d.CreatedAt,
            })
            .ToListAsync(ct);

        return Result<PagedResult<DteDocumentoListItemDto>>.Ok(
            PagedResult<DteDocumentoListItemDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<DteDocumentoDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null)
            return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        return Result<DteDocumentoDto>.Ok(MapToDto(doc));
    }

    public async Task<Result<DteDocumentoDto>> CreateBorradorAsync(int empresaId, CreateDteDocumentoRequest request, string? actor, CancellationToken ct = default)
    {
        var validation = ValidateRequest(request);
        if (validation.Count > 0)
            return Result<DteDocumentoDto>.Fail("Datos del documento inválidos.", "VALIDATION", validation);

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null)
            return Result<DteDocumentoDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        var config = await _db.DteConfiguracion.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        var ambiente = config?.AmbienteCodigo ?? "PRUEBAS";
        var establecimiento = (config?.CodigoEstablecimientoMh ?? "0001").PadLeft(4, '0');
        var puntoVenta = (config?.CodigoPuntoVentaMh ?? "0001").PadLeft(4, '0');

        var doc = new DteDocumento
        {
            EmpresaId = empresaId,
            SucursalId = request.SucursalId,
            PuntoVentaId = request.PuntoVentaId,
            TipoDteCodigo = request.TipoDteCodigo,
            VersionDte = request.TipoDteCodigo is TipoDteCodigos.ComprobanteCreditoFiscal
                                              or TipoDteCodigos.NotaCredito
                                              or TipoDteCodigos.NotaDebito ? 3 : 1,
            AmbienteCodigo = ambiente,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            FechaEmision = DateTime.UtcNow.Date,
            HoraEmision = DateTime.UtcNow.TimeOfDay,
            TipoMonedaCodigo = string.IsNullOrEmpty(request.TipoMonedaCodigo) ? "USD" : request.TipoMonedaCodigo,
            CondicionOperacionCodigo = request.CondicionOperacionCodigo,
            FormaPagoCodigo = request.FormaPagoCodigo,
            PlazoDias = request.PlazoDias,
            DocumentoRelacionadoId = request.DocumentoRelacionadoId,
            NumeroDocumentoRelacionado = request.NumeroDocumentoRelacionado,
            TipoDteRelacionado = request.TipoDteRelacionado,
            TipoGeneracionRelacionado = request.TipoGeneracionRelacionado,
            Observaciones = request.Observaciones,
            EstadoCodigo = DteEstadoCodigos.Borrador,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };

        // Receptor snapshot
        if (request.ClienteId.HasValue)
        {
            var cliente = await _db.Clientes.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClienteId.Value && c.EmpresaId == empresaId, ct);
            if (cliente is null)
                return Result<DteDocumentoDto>.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");
            doc.ClienteId = cliente.Id;
            doc.ReceptorTipoDocumento = cliente.TipoDocumentoCodigo;
            doc.ReceptorNumeroDocumento = cliente.NumeroDocumento;
            doc.ReceptorNrc = cliente.Nrc;
            doc.ReceptorNombre = cliente.Nombre;
            doc.ReceptorTipoContribuyente = cliente.TipoContribuyenteCodigo;
            doc.ReceptorCodigoActividad = cliente.CodigoActividad;
            doc.ReceptorActividadEconomica = cliente.ActividadEconomica;
            doc.ReceptorDepartamentoCodigo = cliente.DepartamentoCodigo;
            doc.ReceptorMunicipioCodigo = cliente.MunicipioCodigo;
            doc.ReceptorDireccion = cliente.Direccion;
            doc.ReceptorCorreo = cliente.Correo;
            doc.ReceptorTelefono = cliente.Telefono;
        }
        else if (request.ReceptorManual is { } r)
        {
            doc.ReceptorTipoDocumento = r.TipoDocumento;
            doc.ReceptorNumeroDocumento = r.NumeroDocumento;
            doc.ReceptorNrc = r.Nrc;
            doc.ReceptorNombre = r.Nombre;
            doc.ReceptorTipoContribuyente = r.TipoContribuyente;
            doc.ReceptorCodigoActividad = r.CodigoActividad;
            doc.ReceptorActividadEconomica = r.ActividadEconomica;
            doc.ReceptorDepartamentoCodigo = r.DepartamentoCodigo;
            doc.ReceptorMunicipioCodigo = r.MunicipioCodigo;
            doc.ReceptorDireccion = r.Direccion;
            doc.ReceptorCorreo = r.Correo;
            doc.ReceptorTelefono = r.Telefono;
        }

        // Numero de control: correlativo por (empresa, tipoDte)
        var ultimoCorrelativo = await _db.DteDocumentos
            .Where(d => d.EmpresaId == empresaId && d.TipoDteCodigo == request.TipoDteCodigo)
            .CountAsync(ct);
        var correlativo = (ultimoCorrelativo + 1).ToString().PadLeft(15, '0');
        doc.NumeroControl = $"DTE-{request.TipoDteCodigo}-{establecimiento}{puntoVenta}-{correlativo}";

        // Detalles
        var numLinea = 1;
        foreach (var linea in request.Lineas)
        {
            string codigo = linea.Codigo;
            string descripcion = linea.Descripcion;
            string unidad = linea.UnidadMedidaCodigo;
            int tipoItem = linea.TipoItem;

            if (linea.ProductoId.HasValue)
            {
                var prod = await _db.Productos.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == linea.ProductoId.Value && p.EmpresaId == empresaId, ct);
                if (prod is null)
                    return Result<DteDocumentoDto>.Fail($"Producto {linea.ProductoId} no encontrado.", "PRODUCTO_NOT_FOUND");
                if (string.IsNullOrEmpty(codigo)) codigo = prod.CodigoInterno;
                if (string.IsNullOrEmpty(descripcion)) descripcion = prod.Nombre;
                if (string.IsNullOrEmpty(unidad)) unidad = prod.UnidadMedidaCodigo;
                if (prod.TipoItem == "SERVICIO") tipoItem = 2;
            }

            doc.Detalles.Add(new DteDocumentoDetalle
            {
                NumeroLinea = numLinea++,
                ProductoId = linea.ProductoId,
                Codigo = string.IsNullOrEmpty(codigo) ? $"ITEM-{numLinea:000}" : codigo,
                Descripcion = string.IsNullOrEmpty(descripcion) ? "Producto" : descripcion,
                UnidadMedidaCodigo = string.IsNullOrEmpty(unidad) ? "UNIDAD" : unidad,
                TipoItem = tipoItem,
                Cantidad = linea.Cantidad,
                PrecioUnitario = linea.PrecioUnitario,
                MontoDescuento = linea.MontoDescuento,
                NoGravado = linea.NoGravado || string.Equals(linea.Clasificacion, "NO_SUJETA", StringComparison.OrdinalIgnoreCase),
                Observaciones = linea.Observaciones,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actor,
            });
        }

        // Calcular totales
        _calculator.Recalcular(doc);

        _db.DteDocumentos.Add(doc);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREATE_BORRADOR", "OK",
            $"DTE {doc.TipoDteCodigo} #{doc.NumeroControl} en borrador (total={doc.TotalPagar:0.00})", doc.Id);

        return await GetByIdAsync(empresaId, doc.Id, ct);
    }

    public async Task<Result<DteDocumentoDto>> GenerarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .Include(d => d.Empresa)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado or DteEstadoCodigos.Enviado or DteEstadoCodigos.Firmado)
            return Result<DteDocumentoDto>.Fail("El documento ya fue procesado.", "INVALID_STATE");

        // Re-snapshot del cálculo por si cambiaron líneas
        _calculator.Recalcular(doc);

        var json = _generator.Generar(doc);
        if (json.IsFailure)
            return Result<DteDocumentoDto>.Fail(json.Error ?? "Error al generar JSON.", json.ErrorCode);

        if (doc.Json is null)
        {
            doc.Json = new DteDocumentoJson
            {
                JsonDte = json.Value!,
                GeneradoAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actor,
            };
        }
        else
        {
            doc.Json.JsonDte = json.Value!;
            doc.Json.GeneradoAt = DateTime.UtcNow;
            doc.Json.UpdatedAt = DateTime.UtcNow;
            doc.Json.UpdatedBy = actor;
        }

        doc.EstadoCodigo = DteEstadoCodigos.Generado;
        doc.GeneradoAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "GENERAR", "OK",
            $"JSON generado para DTE {doc.NumeroControl} (longitud={json.Value!.Length})", doc.Id);

        return await GetByIdAsync(empresaId, doc.Id, ct);
    }

    public async Task<Result<DteDocumentoDto>> ValidarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .Include(d => d.Empresa)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        var errors = ValidateDomain(doc);
        if (errors.Count > 0)
        {
            doc.EstadoCodigo = DteEstadoCodigos.Error;
            doc.UpdatedAt = DateTime.UtcNow; doc.UpdatedBy = actor;
            await _db.SaveChangesAsync(ct);
            await Audit(empresaId, actor, "VALIDAR", "FAIL", string.Join("; ", errors), doc.Id);
            return Result<DteDocumentoDto>.Fail("Documento no válido.", "VALIDATION", errors);
        }

        if (doc.EstadoCodigo == DteEstadoCodigos.Borrador)
        {
            var gen = await GenerarAsync(empresaId, id, actor, ct);
            if (gen.IsFailure) return gen;
        }

        doc.EstadoCodigo = DteEstadoCodigos.Validado;
        doc.ValidadoAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow; doc.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "VALIDAR", "OK", $"DTE {doc.NumeroControl} validado.", doc.Id);

        return await GetByIdAsync(empresaId, doc.Id, ct);
    }

    public async Task<Result> InvalidarAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos.FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado)
            return Result.Fail("No se puede invalidar un DTE ya procesado en Hacienda. Use anulación.", "INVALID_STATE");

        doc.EstadoCodigo = DteEstadoCodigos.Invalidado;
        doc.Observaciones = string.IsNullOrEmpty(motivo) ? doc.Observaciones : $"[INVALIDADO] {motivo}";
        doc.UpdatedAt = DateTime.UtcNow; doc.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INVALIDAR", "OK", motivo ?? "Sin motivo", doc.Id);
        return Result.Ok();
    }

    // ---- validación de request ----

    private static List<string> ValidateRequest(CreateDteDocumentoRequest r)
    {
        var errors = new List<string>();
        if (!TiposSoportados.Contains(r.TipoDteCodigo))
            errors.Add($"Tipo de DTE no soportado: {r.TipoDteCodigo}.");
        if (r.Lineas is null || r.Lineas.Count == 0)
            errors.Add("Debe incluir al menos una línea de detalle.");
        else
        {
            for (var i = 0; i < r.Lineas.Count; i++)
            {
                var l = r.Lineas[i];
                if (string.IsNullOrWhiteSpace(l.Descripcion) && !l.ProductoId.HasValue)
                    errors.Add($"Línea {i + 1}: descripción requerida.");
                if (l.Cantidad <= 0)
                    errors.Add($"Línea {i + 1}: la cantidad debe ser > 0.");
                if (l.PrecioUnitario < 0)
                    errors.Add($"Línea {i + 1}: el precio no puede ser negativo.");
                if (l.MontoDescuento < 0)
                    errors.Add($"Línea {i + 1}: el descuento no puede ser negativo.");
            }
        }

        // Para CCF, NC, ND y Sujeto Excluido: receptor con identificación obligatorio
        var requiereReceptor = r.TipoDteCodigo != TipoDteCodigos.FacturaConsumidorFinal;
        if (requiereReceptor && r.ClienteId is null && r.ReceptorManual is null)
            errors.Add("Para este tipo de DTE el receptor es obligatorio.");

        // NC y ND: documento relacionado
        if (r.TipoDteCodigo is TipoDteCodigos.NotaCredito or TipoDteCodigos.NotaDebito)
        {
            if (string.IsNullOrWhiteSpace(r.NumeroDocumentoRelacionado) && r.DocumentoRelacionadoId is null)
                errors.Add("Nota de crédito/débito requiere documento relacionado.");
        }

        return errors;
    }

    private static List<string> ValidateDomain(DteDocumento d)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(d.NumeroControl)) errors.Add("Falta número de control.");
        if (string.IsNullOrWhiteSpace(d.CodigoGeneracion)) errors.Add("Falta código de generación.");
        if (d.Detalles.Count == 0) errors.Add("Documento sin detalles.");
        if (d.MontoTotalOperacion <= 0 && d.TotalPagar <= 0) errors.Add("Monto total debe ser > 0.");
        if (d.TipoDteCodigo != TipoDteCodigos.FacturaConsumidorFinal && string.IsNullOrEmpty(d.ReceptorNombre))
            errors.Add("Receptor obligatorio para este tipo de DTE.");
        if (d.TipoDteCodigo == TipoDteCodigos.ComprobanteCreditoFiscal && string.IsNullOrEmpty(d.ReceptorNrc))
            errors.Add("CCF requiere NRC del receptor.");
        return errors;
    }

    // ---- mapping ----

    private static DteDocumentoDto MapToDto(DteDocumento d) => new()
    {
        Id = d.Id,
        EmpresaId = d.EmpresaId,
        TipoDteCodigo = d.TipoDteCodigo,
        VersionDte = d.VersionDte,
        AmbienteCodigo = d.AmbienteCodigo,
        NumeroControl = d.NumeroControl,
        CodigoGeneracion = d.CodigoGeneracion,
        SelloRecibido = d.SelloRecibido,
        ModeloFacturacion = d.ModeloFacturacion,
        TipoTransmision = d.TipoTransmision,
        FechaEmision = d.FechaEmision,
        HoraEmision = d.HoraEmision,
        TipoMonedaCodigo = d.TipoMonedaCodigo,
        ClienteId = d.ClienteId,
        ReceptorTipoDocumento = d.ReceptorTipoDocumento,
        ReceptorNumeroDocumento = d.ReceptorNumeroDocumento,
        ReceptorNrc = d.ReceptorNrc,
        ReceptorNombre = d.ReceptorNombre,
        ReceptorTipoContribuyente = d.ReceptorTipoContribuyente,
        ReceptorCodigoActividad = d.ReceptorCodigoActividad,
        ReceptorActividadEconomica = d.ReceptorActividadEconomica,
        ReceptorDepartamentoCodigo = d.ReceptorDepartamentoCodigo,
        ReceptorMunicipioCodigo = d.ReceptorMunicipioCodigo,
        ReceptorDireccion = d.ReceptorDireccion,
        ReceptorCorreo = d.ReceptorCorreo,
        ReceptorTelefono = d.ReceptorTelefono,
        CondicionOperacionCodigo = d.CondicionOperacionCodigo,
        FormaPagoCodigo = d.FormaPagoCodigo,
        PlazoDias = d.PlazoDias,
        DocumentoRelacionadoId = d.DocumentoRelacionadoId,
        NumeroDocumentoRelacionado = d.NumeroDocumentoRelacionado,
        TipoDteRelacionado = d.TipoDteRelacionado,
        Observaciones = d.Observaciones,
        TotalNoSujeto = d.TotalNoSujeto,
        TotalExenta = d.TotalExenta,
        TotalGravada = d.TotalGravada,
        SubTotalVentas = d.SubTotalVentas,
        TotalDescuento = d.TotalDescuento,
        IvaTotal = d.IvaTotal,
        IvaRetenido = d.IvaRetenido,
        ReteRenta = d.ReteRenta,
        SubTotal = d.SubTotal,
        MontoTotalOperacion = d.MontoTotalOperacion,
        TotalNoGravado = d.TotalNoGravado,
        TotalPagar = d.TotalPagar,
        TotalLetras = d.TotalLetras,
        EstadoCodigo = d.EstadoCodigo,
        CreatedAt = d.CreatedAt,
        GeneradoAt = d.GeneradoAt,
        ValidadoAt = d.ValidadoAt,
        Detalles = d.Detalles
            .OrderBy(x => x.NumeroLinea)
            .Select(l => new DteDocumentoDetalleDto
            {
                Id = l.Id,
                NumeroLinea = l.NumeroLinea,
                ProductoId = l.ProductoId,
                TipoItem = l.TipoItem,
                Codigo = l.Codigo,
                Descripcion = l.Descripcion,
                UnidadMedidaCodigo = l.UnidadMedidaCodigo,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario,
                MontoDescuento = l.MontoDescuento,
                VentaNoSujeta = l.VentaNoSujeta,
                VentaExenta = l.VentaExenta,
                VentaGravada = l.VentaGravada,
                IvaItem = l.IvaItem,
                NoGravado = l.NoGravado,
                Observaciones = l.Observaciones,
            }).ToList(),
        JsonDte = d.Json?.JsonDte,
    };

    private Task Audit(int empresaId, string? actor, string accion, string resultado, string? detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "DteDocumento", EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });
}
