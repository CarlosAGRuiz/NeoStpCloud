using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
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
    private readonly IDteSignerService _signer;
    private readonly IHaciendaReceptionClient _reception;
    private readonly IHaciendaAuthClient _haciendaAuth;
    private readonly ISecretProtector _protector;
    private readonly IDtePdfService _pdf;
    private readonly IEmailSender _email;
    private readonly IAuditoriaService _auditoria;

    public DteDocumentosService(
        NeoStpDbContext db,
        IDteCalculator calculator,
        IDteGeneratorService generator,
        IDteSignerService signer,
        IHaciendaReceptionClient reception,
        IHaciendaAuthClient haciendaAuth,
        ISecretProtector protector,
        IDtePdfService pdf,
        IEmailSender email,
        IAuditoriaService auditoria)
    {
        _db = db;
        _calculator = calculator;
        _generator = generator;
        _signer = signer;
        _reception = reception;
        _haciendaAuth = haciendaAuth;
        _protector = protector;
        _pdf = pdf;
        _email = email;
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

        // Número de control: correlativo atómico por (empresa, tipoDte)
        // UPSERT + incremento en una sola operación SQL para evitar race conditions.
        var correlativoNum = await NextCorrelativoAsync(empresaId, request.TipoDteCodigo, ct);
        var correlativo = correlativoNum.ToString().PadLeft(15, '0');
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

    public async Task<Result<DteDocumentoDto>> FirmarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .Include(d => d.Empresa)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        if (doc.EstadoCodigo is DteEstadoCodigos.Enviado or DteEstadoCodigos.Procesado)
            return Result<DteDocumentoDto>.Fail("El documento ya fue enviado.", "INVALID_STATE");

        if (doc.Json is null || string.IsNullOrEmpty(doc.Json.JsonDte))
        {
            var gen = await GenerarAsync(empresaId, id, actor, ct);
            if (gen.IsFailure) return gen;
            doc = await _db.DteDocumentos
                .Include(d => d.Json)
                .FirstAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        }

        var config = await _db.DteConfiguracion.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config is null)
            return Result<DteDocumentoDto>.Fail("Configuración DTE no encontrada.", "CONFIG_NOT_FOUND");
        if (config.CertificadoBlob is null || config.CertificadoBlob.Length == 0)
            return Result<DteDocumentoDto>.Fail("Certificado no cargado en Configuración DTE.", "VALIDATION");

        string? certPassword = null;
        if (!string.IsNullOrEmpty(config.PasswordCertificadoCifrado))
        {
            try { certPassword = _protector.Unprotect(config.PasswordCertificadoCifrado); }
            catch
            {
                return Result<DteDocumentoDto>.Fail(
                    "No se pudo descifrar el password del certificado (¿llave de DataProtection cambió?). Reingresar.",
                    "DECRYPT_FAILED");
            }
        }

        var firma = await _signer.FirmarAsync(doc.Json!.JsonDte, config.CertificadoBlob, certPassword, ct);
        if (!firma.Success)
        {
            await Audit(empresaId, actor, "FIRMAR", "FAIL", $"{firma.Mensaje}: {firma.Detalle}", doc.Id);
            return Result<DteDocumentoDto>.Fail(firma.Detalle ?? firma.Mensaje ?? "Error firmando.", "FIRMA_FAILED");
        }

        doc.Json.JsonFirmado = firma.JsonFirmado;
        doc.Json.FirmadoAt = DateTime.UtcNow;
        doc.Json.UpdatedAt = DateTime.UtcNow;
        doc.Json.UpdatedBy = actor;

        doc.EstadoCodigo = DteEstadoCodigos.Firmado;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "FIRMAR", "OK",
            $"DTE {doc.NumeroControl} firmado ({firma.Detalle}).", doc.Id);

        return await GetByIdAsync(empresaId, doc.Id, ct);
    }

    public async Task<Result<DteDocumentoDto>> EnviarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .Include(d => d.Empresa)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado)
            return Result<DteDocumentoDto>.Fail("El documento ya fue procesado por Hacienda.", "INVALID_STATE");
        if (doc.Json is null || string.IsNullOrEmpty(doc.Json.JsonFirmado))
        {
            // Auto-firma si no está firmado todavía
            var f = await FirmarAsync(empresaId, id, actor, ct);
            if (f.IsFailure) return f;
            doc = await _db.DteDocumentos
                .Include(d => d.Json)
                .FirstAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        }

        var config = await _db.DteConfiguracion.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config is null)
            return Result<DteDocumentoDto>.Fail("Configuración DTE no encontrada.", "CONFIG_NOT_FOUND");

        var tokenResult = await ObtenerTokenAsync(config, ct);
        if (!tokenResult.Success)
            return Result<DteDocumentoDto>.Fail(tokenResult.Mensaje ?? "No se pudo obtener token Hacienda.", "HACIENDA_AUTH_FAILED");

        var resp = await _reception.EnviarAsync(new HaciendaReceptionRequest
        {
            Ambiente = config.AmbienteCodigo == "PRODUCCION" ? "01" : "00",
            AmbienteCodigo = config.AmbienteCodigo,
            IdEnvio = doc.Id,
            Version = doc.VersionDte,
            TipoDte = doc.TipoDteCodigo,
            Documento = doc.Json!.JsonFirmado!,
            CodigoGeneracion = doc.CodigoGeneracion,
            Token = tokenResult.Token!,
        }, ct);

        doc.Json.RespuestaHacienda = resp.Raw;
        doc.Json.RespuestaAt = DateTime.UtcNow;
        doc.Json.UpdatedAt = DateTime.UtcNow;
        doc.Json.UpdatedBy = actor;
        doc.EnviadoAt = DateTime.UtcNow;

        // Map response -> estado interno
        var nuevoEstado = (resp.Estado ?? string.Empty).ToUpperInvariant() switch
        {
            "PROCESADO" => DteEstadoCodigos.Procesado,
            "RECHAZADO" => DteEstadoCodigos.Rechazado,
            "CONTINGENCIA" => DteEstadoCodigos.Contingencia,
            "NO_AUTORIZADO" => DteEstadoCodigos.Error,
            _ => resp.Success ? DteEstadoCodigos.Enviado : DteEstadoCodigos.Error,
        };
        doc.EstadoCodigo = nuevoEstado;
        if (nuevoEstado == DteEstadoCodigos.Procesado)
        {
            doc.SelloRecibido = resp.SelloRecibido;
            doc.ProcesadoAt = resp.FhProcesamiento ?? DateTime.UtcNow;
        }
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ENVIAR",
            resp.Success && nuevoEstado == DteEstadoCodigos.Procesado ? "OK" : "FAIL",
            $"[{resp.CodigoHttp}] estado={resp.Estado} cod={resp.CodigoMsg} desc={resp.DescripcionMsg}",
            doc.Id);

        return await GetByIdAsync(empresaId, doc.Id, ct);
    }

    private async Task<(bool Success, string? Token, string? Mensaje)> ObtenerTokenAsync(
        Domain.Core.Dte.DteConfiguracion config, CancellationToken ct)
    {
        // Token cacheado vigente: 5 minutos de margen antes de expirar
        if (!string.IsNullOrEmpty(config.TokenMhCifrado)
            && config.TokenMhExpiraAt.HasValue
            && config.TokenMhExpiraAt.Value > DateTime.UtcNow.AddMinutes(5))
        {
            try { return (true, _protector.Unprotect(config.TokenMhCifrado), null); }
            catch { /* fall-through al refresh */ }
        }

        // Refresh: autenticar contra Hacienda con las credenciales guardadas
        if (string.IsNullOrEmpty(config.UsuarioMh) || string.IsNullOrEmpty(config.PasswordMhCifrado))
            return (false, null, "Faltan credenciales MH en Configuración DTE.");

        string password;
        try { password = _protector.Unprotect(config.PasswordMhCifrado); }
        catch { return (false, null, "No se pudo descifrar el password MH (¿llave DataProtection cambió?)."); }

        var auth = await _haciendaAuth.AutenticarAsync(config.UsuarioMh, password, config.AmbienteCodigo, ct);
        if (!auth.Success || string.IsNullOrEmpty(auth.Token))
            return (false, null, $"Auth MH falló: [{auth.CodigoHttp}] {auth.Mensaje}");

        config.TokenMhCifrado = _protector.Protect(auth.Token);
        config.TokenMhExpiraAt = auth.ExpiresAt ?? DateTime.UtcNow.AddHours(8);
        await _db.SaveChangesAsync(ct);
        return (true, auth.Token, null);
    }

    public async Task<Result<DteArchivosDto>> ObtenerArchivosAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .Include(d => d.Empresa)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteArchivosDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        var safeNumero = (doc.NumeroControl ?? "documento").Replace(" ", "_");
        var pdfBytes = _pdf.Generar(doc);
        var jsonContent = doc.Json?.JsonDte ?? string.Empty;

        return Result<DteArchivosDto>.Ok(new DteArchivosDto
        {
            NumeroControl = doc.NumeroControl ?? string.Empty,
            PdfFileName = $"{safeNumero}.pdf",
            PdfContent = pdfBytes,
            JsonFileName = $"{safeNumero}.json",
            JsonContent = jsonContent,
        });
    }

    public async Task<Result<DteReenvioResultDto>> ReenviarPorCorreoAsync(int empresaId, int id, string? destinatario, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .Include(d => d.Empresa)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteReenvioResultDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        var to = !string.IsNullOrWhiteSpace(destinatario) ? destinatario!.Trim() : doc.ReceptorCorreo;
        if (string.IsNullOrWhiteSpace(to))
            return Result<DteReenvioResultDto>.Fail(
                "No hay correo destinatario. Indica uno o registra el del receptor.", "VALIDATION");

        var pdf = _pdf.Generar(doc);
        var json = doc.Json?.JsonDte;
        var safeNumero = (doc.NumeroControl ?? "documento").Replace(" ", "_");

        var attachments = new List<EmailAttachment>
        {
            new()
            {
                FileName = $"{safeNumero}.pdf",
                MediaType = "application/pdf",
                Content = pdf,
            },
        };
        if (!string.IsNullOrEmpty(json))
        {
            attachments.Add(new EmailAttachment
            {
                FileName = $"{safeNumero}.json",
                MediaType = "application/json",
                Content = System.Text.Encoding.UTF8.GetBytes(json),
            });
        }

        var emisor = doc.Empresa?.RazonSocial ?? "su proveedor";
        var subject = $"DTE {doc.TipoDteCodigo} {doc.NumeroControl} - {emisor}";
        var body = BuildBody(doc, emisor);

        var message = new EmailMessage
        {
            To = to,
            Subject = subject,
            HtmlBody = body,
        };
        message.Attachments.AddRange(attachments);
        var result = await _email.EnviarAsync(message, ct);

        var dto = new DteReenvioResultDto
        {
            Enviado = result.Success,
            Destinatario = to,
            Mensaje = result.Mensaje,
            Detalle = result.Detalle,
            MessageId = result.MessageId,
        };

        await Audit(empresaId, actor, "REENVIAR_CORREO", result.Success ? "OK" : "FAIL",
            $"a {to}: {result.Mensaje} - {result.Detalle}", doc.Id);

        return result.Success
            ? Result<DteReenvioResultDto>.Ok(dto)
            : Result<DteReenvioResultDto>.Fail(result.Detalle ?? result.Mensaje ?? "Error enviando correo.", "EMAIL_FAILED");
    }

    private static string BuildBody(DteDocumento d, string emisor)
    {
        var receptor = d.ReceptorNombre ?? "Estimado(a) cliente";
        return $"""
            <!doctype html>
            <html lang="es">
            <body style="font-family:Segoe UI, Arial, sans-serif; color:#222">
              <p>Estimado(a) <strong>{receptor}</strong>,</p>
              <p>Adjuntamos el Documento Tributario Electrónico emitido por <strong>{emisor}</strong>:</p>
              <ul>
                <li><strong>Tipo:</strong> {d.TipoDteCodigo}</li>
                <li><strong>Número de control:</strong> <code>{d.NumeroControl}</code></li>
                <li><strong>Código de generación:</strong> <code>{d.CodigoGeneracion}</code></li>
                <li><strong>Fecha de emisión:</strong> {d.FechaEmision:yyyy-MM-dd}</li>
                <li><strong>Total a pagar:</strong> $ {d.TotalPagar:N2}</li>
                {(string.IsNullOrEmpty(d.SelloRecibido) ? "" : $"<li><strong>Sello recibido:</strong> <code>{d.SelloRecibido}</code></li>")}
              </ul>
              <p>Encontrará el PDF (representación gráfica) y el JSON oficial del DTE como archivos adjuntos.</p>
              <p style="color:#888; font-size:12px">Enviado por NeoSTP Cloud · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>
            </body>
            </html>
            """;
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
        FirmadoAt = d.Json?.FirmadoAt,
        EnviadoAt = d.EnviadoAt,
        ProcesadoAt = d.ProcesadoAt,
        RespuestaAt = d.Json?.RespuestaAt,
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
        JsonFirmado = d.Json?.JsonFirmado,
        RespuestaHacienda = d.Json?.RespuestaHacienda,
    };

    private Task Audit(int empresaId, string? actor, string accion, string resultado, string? detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "DteDocumento", EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });

    /// <summary>
    /// Obtiene el siguiente correlativo de forma atómica usando UPSERT + UPDATE SQL.
    /// Evita la race condition del COUNT(*)+1 en entornos concurrentes.
    /// </summary>
    private async Task<int> NextCorrelativoAsync(int empresaId, string tipoDte, CancellationToken ct)
    {
        // Intentar actualizar si ya existe el registro
        var updated = await _db.Database.ExecuteSqlAsync(
            $"""
            UPDATE Dte_Correlativos
               SET UltimoCorrelativo = UltimoCorrelativo + 1,
                   ActualizadoAt     = GETUTCDATE()
             WHERE EmpresaId = {empresaId}
               AND TipoDteCodigo = {tipoDte}
            """, ct);

        if (updated == 0)
        {
            // Primera vez: insertar con correlativo = 1, ignorar duplicado por concurrencia
            await _db.Database.ExecuteSqlAsync(
                $"""
                IF NOT EXISTS (SELECT 1 FROM Dte_Correlativos WHERE EmpresaId = {empresaId} AND TipoDteCodigo = {tipoDte})
                    INSERT INTO Dte_Correlativos (EmpresaId, TipoDteCodigo, UltimoCorrelativo, ActualizadoAt)
                    VALUES ({empresaId}, {tipoDte}, 1, GETUTCDATE())
                ELSE
                    UPDATE Dte_Correlativos
                       SET UltimoCorrelativo = UltimoCorrelativo + 1,
                           ActualizadoAt     = GETUTCDATE()
                     WHERE EmpresaId = {empresaId}
                       AND TipoDteCodigo = {tipoDte}
                """, ct);
        }

        var row = await _db.DteCorrelativos
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.TipoDteCodigo == tipoDte, ct);

        return row?.UltimoCorrelativo ?? 1;
    }
}
