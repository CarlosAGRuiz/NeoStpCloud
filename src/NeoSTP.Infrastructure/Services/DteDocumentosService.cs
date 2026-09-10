using System.Data;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Branding;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Application.Clientes;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Common;
using NeoSTP.Application.Lookups;
using NeoSTP.Infrastructure.Persistence;
using System.Text.Json;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService : IDteDocumentosService
{
    private const string AuditModule = "DTE_DOCUMENTOS";
    private static readonly string[] TiposSoportados =
    {
        TipoDteCodigos.FacturaConsumidorFinal,
        TipoDteCodigos.ComprobanteCreditoFiscal,
        TipoDteCodigos.NotaCredito,
        TipoDteCodigos.NotaDebito,
        TipoDteCodigos.FacturaSujetoExcluido,
        TipoDteCodigos.NotaRemision,
        TipoDteCodigos.FacturaExportacion,
        TipoDteCodigos.ComprobanteDonacion,
        TipoDteCodigos.ComprobanteRetencion,
        TipoDteCodigos.ComprobanteLiquidacion,
        TipoDteCodigos.DocumentoContableLiquidacion,
    };

    private readonly NeoStpDbContext _db;
    private readonly IDteCalculator _calculator;
    private readonly IDteGeneratorService _generator;
    private readonly IDteSignerService _signer;
    private readonly IHaciendaReceptionClient _reception;
    private readonly IHaciendaContingenciaClient _contingencia;
    private readonly IHaciendaEventoClient _evento;
    private readonly IHaciendaAuthClient _haciendaAuth;
    private readonly ISecretProtector _protector;
    private readonly IDtePdfService _pdf;
    private readonly ITenantEmailSender _email;
    private readonly IAuditoriaService _auditoria;
    private readonly IConnectWebhookDispatcher _webhookDispatcher;
    private readonly NeoSTP.Infrastructure.Diagnostics.NeoStpMetrics? _metrics;
    private readonly NeoSTP.Application.Licenciamiento.ILicenciaGuardService? _licenciaGuard;
    private readonly NeoSTP.Application.Lookups.ILookupService? _lookup;
    private readonly IHaciendaConsultaDteClient? _consultaDte;
    private readonly Microsoft.Extensions.Logging.ILogger<DteDocumentosService>? _logger;

    // Corte de esquemas MH 2026-08-25: cuando Dte:EsquemaNuevo=true los eventos usan las
    // versiones nuevas (invalidación v3). Contingencia ya migró a v4 sin toggle porque apitest
    // la exige desde ya. Default false = versiones que apitest aún acepta hoy.
    private readonly bool _esquemaNuevo;
    private readonly DteSchemaPolicy _schemaPolicy;

    public DteDocumentosService(
        NeoStpDbContext db,
        IDteCalculator calculator,
        IDteGeneratorService generator,
        IDteSignerService signer,
        IHaciendaReceptionClient reception,
        IHaciendaContingenciaClient contingencia,
        IHaciendaEventoClient evento,
        IHaciendaAuthClient haciendaAuth,
        ISecretProtector protector,
        IDtePdfService pdf,
        ITenantEmailSender email,
        IAuditoriaService auditoria,
        IConnectWebhookDispatcher webhookDispatcher,
        NeoSTP.Infrastructure.Diagnostics.NeoStpMetrics? metrics = null,
        NeoSTP.Application.Licenciamiento.ILicenciaGuardService? licenciaGuard = null,
        NeoSTP.Application.Lookups.ILookupService? lookup = null,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null,
        IHaciendaConsultaDteClient? consultaDte = null,
        Microsoft.Extensions.Logging.ILogger<DteDocumentosService>? logger = null)
    {
        _schemaPolicy = new DteSchemaPolicy(configuration);
        _metrics = metrics;
        _licenciaGuard = licenciaGuard;
        _lookup = lookup;
        _esquemaNuevo = configuration is not null
            && Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<bool>(configuration, "Dte:EsquemaNuevo");
        _db = db;
        _calculator = calculator;
        _generator = generator;
        _signer = signer;
        _reception = reception;
        _contingencia = contingencia;
        _evento = evento;
        _haciendaAuth = haciendaAuth;
        _protector = protector;
        _pdf = pdf;
        _email = email;
        _auditoria = auditoria;
        _webhookDispatcher = webhookDispatcher;
        _consultaDte = consultaDte;
        _logger = logger;
    }

    private static readonly TimeZoneInfo SvTimeZone = ResolveSvTimeZone();

    private static TimeZoneInfo ResolveSvTimeZone()
    {
        foreach (var id in new[] { "America/El_Salvador", "Central America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("SV-UTC-6", TimeSpan.FromHours(-6), "El Salvador", "El Salvador");
    }

    /// <summary>
    /// Fecha/hora actual de El Salvador (UTC-6, sin horario de verano). MH valida contra su reloj
    /// local: usar <c>DateTime.UtcNow</c> hace que los DTE emitidos de noche (UTC-6) lleven la fecha
    /// del día siguiente, y rompe la coherencia fInicio&lt;=fFin del evento de contingencia.
    /// </summary>
    private static DateTime NowSv() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SvTimeZone);

    /// <summary>
    /// Traduce los códigos territoriales internos del receptor (p. ej. "SAN_SALVADOR",
    /// "SAN_SALVADOR_CENTRO") al código MH numérico que exige el esquema de Hacienda ("06",
    /// "23"). El emisor ya guarda códigos MH; el cliente guarda los códigos internos del
    /// catálogo, y el generador los transmite tal cual. Sin esta traducción Hacienda rechaza
    /// el DTE ("departamento no cumple el formato requerido"). Es tolerante: si el código no
    /// está en catálogo (o ya es un código MH) lo deja como está, así no rompe datos válidos.
    /// </summary>
    private async Task<Result> ResolverReceptorTerritorialMhAsync(DteDocumento doc, int empresaId, bool esquemaNuevo, CancellationToken ct)
    {
        if (_lookup is null) return Result.Ok();

        if (DteGeneratorService.EsTipoConTerritorioVerificado(doc.TipoDteCodigo))
        {
            if (doc.TipoDteCodigo == "11" || (string.IsNullOrWhiteSpace(doc.ReceptorDepartamentoCodigo)
                && string.IsNullOrWhiteSpace(doc.ReceptorMunicipioCodigo) && string.IsNullOrWhiteSpace(doc.ReceptorDistritoCodigo)))
                return Result.Ok();
            var departments = await _lookup.GetCatalogoAsync(CatalogCodes.DepartamentoEs, empresaId, null, ct);
            var municipalities = await _lookup.GetCatalogoAsync(CatalogCodes.MunicipioEs, empresaId, null, ct);
            var districts = await _lookup.GetCatalogoAsync(CatalogCodes.DistritoEs, empresaId, null, ct);
            var resolved = DteTerritoryResolver.Resolve(doc.ReceptorDepartamentoCodigo, doc.ReceptorMunicipioCodigo,
                doc.ReceptorDistritoCodigo, departments, municipalities, districts,
                DteGeneratorService.RequiereTerritorio2024(doc.TipoDteCodigo, esquemaNuevo));
            if (resolved.IsFailure) return Result.Fail("Receptor: " + resolved.Error, resolved.ErrorCode);
            doc.ReceptorDepartamentoCodigo = resolved.Value!.Department;
            doc.ReceptorMunicipioCodigo = resolved.Value.Municipality;
            doc.ReceptorDistritoCodigo = resolved.Value.District;
            return Result.Ok();
        }

        doc.ReceptorDepartamentoCodigo = await MapCodigoMhAsync(CatalogCodes.DepartamentoEs, doc.ReceptorDepartamentoCodigo, empresaId, ct);
        doc.ReceptorMunicipioCodigo    = await MapCodigoMhAsync(CatalogCodes.MunicipioEs,    doc.ReceptorMunicipioCodigo,    empresaId, ct);
        doc.ReceptorDistritoCodigo     = await MapCodigoMhAsync(CatalogCodes.DistritoEs,     doc.ReceptorDistritoCodigo,     empresaId, ct);
        return Result.Ok();
    }

    private async Task<string?> MapCodigoMhAsync(string catalogo, string? codigoInterno, int empresaId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigoInterno) || _lookup is null) return codigoInterno;
        var items = await _lookup.GetCatalogoAsync(catalogo, empresaId, null, ct);
        return ResolverCodigoMhEnItems(items, codigoInterno);
    }

    /// <summary>
    /// Busca el ítem del catálogo cuyo código interno coincide y devuelve su <c>codigoMH</c>.
    /// Si no está en catálogo o no trae metadata, devuelve el código original (tolerante).
    /// </summary>
    internal static string? ResolverCodigoMhEnItems(IReadOnlyList<LookupItem> items, string? codigoInterno)
    {
        if (string.IsNullOrWhiteSpace(codigoInterno)) return codigoInterno;
        var buscado = NormalizarLookupTexto(codigoInterno);
        var item = items.FirstOrDefault(i =>
            string.Equals(i.Value, codigoInterno, StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.Label, codigoInterno, StringComparison.OrdinalIgnoreCase)
            || NormalizarLookupTexto(i.Value) == buscado
            || NormalizarLookupTexto(i.Label) == buscado);

        if (item is null) return codigoInterno;
        return ExtraerCodigoMh(item.Meta) ?? (PareceCodigoMh(item.Value) ? item.Value : codigoInterno);
    }

    private static bool PareceCodigoMh(string? valor)
        => !string.IsNullOrWhiteSpace(valor)
           && valor.Length <= 4
           && valor.All(char.IsDigit);

    private static string NormalizarLookupTexto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

        var descompuesto = valor.Trim()
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Normalize(NormalizationForm.FormD);

        var sinAcentos = new string(descompuesto
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return string.Join(
                ' ',
                sinAcentos.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToUpperInvariant();
    }

    /// <summary>
    /// Saneador defensivo del emisor antes de generar el JSON DTE. Traduce a códigos MH todo lo
    /// que la empresa pudo haber guardado en formato interno o "humano" (nombres de departamento,
    /// código interno CASA_MATRIZ del tipoEstablecimiento) y quita guiones al NIT/NRC. Sin esto
    /// MH rechaza con "no cumple el formato requerido" / "excede el tamaño permitido" en
    /// #/emisor/nit, /direccion/departamento, /direccion/municipio y /tipoEstablecimiento.
    /// <para>OJO: la Empresa que llega aquí YA debe estar Detach del contexto para evitar que
    /// SaveChanges persista los cambios y sobrescriba la data del usuario en BD.</para>
    /// </summary>
    private async Task SanearEmisorParaMhAsync(Empresa e, DteConfiguracion? config, int empresaId, CancellationToken ct, bool skipTerritory = false)
    {
        e.Nit = ClienteValidator.StripToDigits(e.Nit) ?? e.Nit;
        e.Nrc = ClienteValidator.StripToDigits(e.Nrc);
        if (!skipTerritory)
        {
            e.Departamento = await MapCodigoMhAsync(CatalogCodes.DepartamentoEs, e.Departamento, empresaId, ct);
            e.Municipio = await MapCodigoMhAsync(CatalogCodes.MunicipioEs, e.Municipio, empresaId, ct);
            e.Distrito = await MapCodigoMhAsync(CatalogCodes.DistritoEs, e.Distrito, empresaId, ct);
        }
        if (config is not null && !string.IsNullOrWhiteSpace(config.TipoEstablecimientoCodigo))
        {
            config.TipoEstablecimientoCodigo = await MapCodigoMhAsync(
                CatalogCodes.TipoEstablecimiento, config.TipoEstablecimientoCodigo, empresaId, ct);
        }
    }

    /// <summary>
    /// Saneador defensivo del receptor: si el tipoDocumento interno es NIT y el número trae
    /// guiones (el <see cref="ClienteValidator.NormalizeNit"/> los añade para presentación),
    /// se limpia para MH. Aplica también al NRC del receptor.
    /// </summary>
    private static void SanearReceptorParaMh(DteDocumento doc)
    {
        if (string.Equals(doc.ReceptorTipoDocumento, "NIT", StringComparison.OrdinalIgnoreCase))
            doc.ReceptorNumeroDocumento = ClienteValidator.StripToDigits(doc.ReceptorNumeroDocumento);
        doc.ReceptorNrc = ClienteValidator.StripToDigits(doc.ReceptorNrc);
    }

    /// <summary>
    /// Valida que la empresa emisora tenga los datos que MH exige NO-null (correo, teléfono,
    /// NIT). No podemos inventarlos si están vacíos; devolvemos error claro para que el usuario
    /// los complete en Empresa → Editar antes de emitir.
    /// </summary>
    private static List<string> ValidarEmisorParaMh(Empresa? e)
    {
        var errors = new List<string>();
        if (e is null) { errors.Add("Empresa emisora no cargada."); return errors; }
        if (string.IsNullOrWhiteSpace(e.Nit))
            errors.Add("[emisor.nit] La empresa no tiene NIT registrado.");
        if (string.IsNullOrWhiteSpace(e.Correo))
            errors.Add("[emisor.correo] La empresa no tiene correo registrado. Configúralo en Empresa → Editar antes de emitir.");
        if (string.IsNullOrWhiteSpace(e.Telefono))
            errors.Add("[emisor.telefono] La empresa no tiene teléfono registrado. Configúralo en Empresa → Editar antes de emitir.");
        return errors;
    }

    /// <summary>Extrae <c>codigoMH</c> del metadata JSON del ítem de catálogo, o null si no lo trae.</summary>
    internal static string? ExtraerCodigoMh(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var json = JsonDocument.Parse(metadataJson);
            if (json.RootElement.TryGetProperty("codigoMH", out var mh) && mh.ValueKind == JsonValueKind.String)
            {
                var valor = mh.GetString();
                return string.IsNullOrWhiteSpace(valor) ? null : valor;
            }
        }
        catch (JsonException) { }
        return null;
    }

    public async Task<Result<PagedResult<DteDocumentoListItemDto>>> GetListAsync(int empresaId, DteListQuery query, CancellationToken ct = default)
    {
        var q = _db.DteDocumentos.AsNoTracking().Where(d => d.EmpresaId == empresaId);
        if (query.AmbienteCodigo is not null)
        {
            if (!DteAmbientes.EsValido(query.AmbienteCodigo))
                return Result<PagedResult<DteDocumentoListItemDto>>.Fail("Filtro de ambiente inválido. Use PRUEBAS o PRODUCCION.", "DTE_AMBIENTE_INVALIDO");
            q = q.Where(d => d.AmbienteCodigo == query.AmbienteCodigo);
        }

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
        if (query.TiposDteCodigo is { Count: > 0 })
        {
            var tipos = query.TiposDteCodigo
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (tipos.Count > 0)
                q = q.Where(d => tipos.Contains(d.TipoDteCodigo));
        }
        if (!string.IsNullOrEmpty(query.EstadoCodigo))
            q = q.Where(d => d.EstadoCodigo == query.EstadoCodigo);
        if (query.Desde.HasValue)
            q = q.Where(d => d.FechaEmision >= query.Desde.Value.Date);
        if (query.Hasta.HasValue)
            q = q.Where(d => d.FechaEmision <= query.Hasta.Value.Date);
        if (query.MontoMinimo.HasValue)
            q = q.Where(d => d.TotalPagar >= query.MontoMinimo.Value);
        if (query.MontoMaximo.HasValue)
            q = q.Where(d => d.TotalPagar <= query.MontoMaximo.Value);

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
        var keyValidation = DteIdempotency.ValidateKey(request.IdempotencyKey);
        if (keyValidation.IsFailure) return Result<DteDocumentoDto>.Fail(keyValidation.Error!, keyValidation.ErrorCode);
        if (request.VentaPosOrigenId.HasValue && request.TipoDteCodigo is not ("01" or "03"))
            return Result<DteDocumentoDto>.Fail("POS solo admite factura o crédito fiscal.", "VALIDATION");
        // Clave aleatoria por invocación legacy: protege también un commit incierto dentro del execution strategy.
        // Solo una clave provista por el consumidor (o la venta POS) deduplica POST independientes.
        var scope = request.VentaPosOrigenId.HasValue ? "POS" : request.IdempotencyKey is null ? "ATTEMPT" : "DTE";
        var key = request.VentaPosOrigenId?.ToString(CultureInfo.InvariantCulture) ?? request.IdempotencyKey ?? Guid.NewGuid().ToString("N");
        var keyHash = DteIdempotency.HashKey(key);
        var requestHash = DteIdempotency.Fingerprint(request);
        var validation = ValidateRequest(request);
        if (validation.Count > 0)
            return Result<DteDocumentoDto>.Fail("Datos del documento inválidos.", "VALIDATION", validation);

        // Reserva el cupo del plan y crea el DTE dentro de una misma transacción. En SQL Server
        // se toma un application lock por empresa, de modo que dos nodos/API concurrentes no
        // puedan aprobar simultáneamente el último cupo mensual.
        //
        // La transacción de usuario DEBE ejecutarse a través del execution strategy: el DbContext
        // tiene EnableRetryOnFailure y, sin este envoltorio, EF lanza "the configured execution
        // strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated
        // transactions" y NO se puede emitir ningún DTE en SQL Server (los tests usan EF InMemory,
        // no relacional, por eso no lo detectan). Ver EF Core: "Connection Resiliency".
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Cada intento parte de un ChangeTracker limpio: un reintento transitorio revierte la
            // transacción pero deja las entidades Added rastreadas; sin esto se insertarían dobles.
            _db.ChangeTracker.Clear();

            await using var limiteTransaction = await BeginDteLimitTransactionAsync(empresaId, ct);

        // Se consulta bajo el mismo lock/transacción de creación, antes de consumir cupo o correlativo.
        var previous = await _db.DteDocumentos.AsNoTracking().FirstOrDefaultAsync(d =>
            d.EmpresaId == empresaId && d.IdempotencyScope == scope && d.IdempotencyKeyHash == keyHash, ct);
        if (previous is not null)
        {
            if (previous.IdempotencyRequestHash != requestHash)
                return Result<DteDocumentoDto>.Fail("La clave ya corresponde a otra solicitud. No cambie los datos ni genere otra clave para reintentar una venta existente.", "IDEMPOTENCY_CONFLICT");
            var currentConfig = await _db.DteConfiguracion.AsNoTracking().FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
            var replayContext = DteFiscalContext.Validar(previous.AmbienteCodigo, currentConfig);
            var replay = await GetByIdAsync(empresaId, previous.Id, ct);
            if (replay.IsFailure) return replay;
            replay.Value!.IdempotencyReplayed = true;
            if (replayContext.IsFailure)
                return Result<DteDocumentoDto>.FailWithValue(replay.Value, replayContext.Error!, replayContext.ErrorCode);
            return replay;
        }

        NeoSTP.Domain.Core.Pos.VentaPos? ventaOrigen = null;
        if (request.VentaPosOrigenId is int posId)
        {
            ventaOrigen = await _db.VentasPos.FirstOrDefaultAsync(v => v.Id == posId && v.EmpresaId == empresaId, ct);
            if (ventaOrigen is null) return Result<DteDocumentoDto>.Fail("Venta POS no encontrada.", "VENTA_POS_NOT_FOUND");
            if (ventaOrigen.EstadoCodigo != NeoSTP.Domain.Core.Pos.VentaPosEstados.Completada
                || ventaOrigen.DteDocumentoId.HasValue
                || ventaOrigen.EstadoFacturacion == NeoSTP.Domain.Core.Pos.VentaPosFacturacion.Facturada)
                return Result<DteDocumentoDto>.Fail("La venta POS está anulada o ya tiene un DTE asociado. Consulte el documento existente.", "IDEMPOTENCY_CONFLICT");
        }

        // Enforcement comercial: límite mensual de documentos del plan.
        if (_licenciaGuard is not null)
        {
            var limite = await _licenciaGuard.ValidarLimiteAsync(empresaId,
                NeoSTP.Application.Licenciamiento.RecursoLimitado.DteMensual, ct);
            if (limite.IsFailure) return Result<DteDocumentoDto>.Fail(limite.Error!, limite.ErrorCode);
        }

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null)
            return Result<DteDocumentoDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        var config = await _db.DteConfiguracion.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        var contexto = DteFiscalContext.Validar(config?.AmbienteCodigo, config);
        if (contexto.IsFailure) return Result<DteDocumentoDto>.Fail(contexto.Error!, contexto.ErrorCode);
        var ambiente = config!.AmbienteCodigo;
        var schema = _schemaPolicy.Resolve(empresaId, empresa.Nit, ambiente);
        if (schema.IsFailure) return Result<DteDocumentoDto>.Fail(schema.Error!, schema.ErrorCode);
        var tipoAutorizado = await DteTypeAuthorization.ValidateAsync(_db, empresaId, request.TipoDteCodigo, ct);
        if (tipoAutorizado.IsFailure) return Result<DteDocumentoDto>.Fail(tipoAutorizado.Error!, tipoAutorizado.ErrorCode);
        if (request.DocumentoRelacionadoId is int relacionadoId)
        {
            var relacionado = await _db.DteDocumentos.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == relacionadoId && d.EmpresaId == empresaId, ct);
            if (relacionado is null) return Result<DteDocumentoDto>.Fail("Documento relacionado no encontrado.", "DTE_NOT_FOUND");
            var contextoRelacionado = DteFiscalContext.Validar(relacionado.AmbienteCodigo, config);
            if (contextoRelacionado.IsFailure) return Result<DteDocumentoDto>.Fail(contextoRelacionado.Error!, contextoRelacionado.ErrorCode);
        }
        // numeroControl: DTE-XX-{bloqueEstab}-{15 digitos}.
        // Formato oficial MH (esquemas svfe): el bloque de 8 chars es
        //   (M|B|S|P)([0-9]{3})(P)([0-9]{3})  →  letraTipoEstablecimiento + codEstable(3) + 'P' + codPuntoVenta(3)
        // Ej: DTE-01-M001P001-000000000000001  (NO es codEstable(4)+codPuntoVenta(4) como se creía).
        var bloqueEstab = BuildBloqueEstablecimiento(config);

        var doc = new DteDocumento
        {
            EmpresaId = empresaId,
            IdempotencyScope = scope,
            IdempotencyKeyHash = keyHash,
            IdempotencyRequestHash = requestHash,
            SucursalId = request.SucursalId,
            PuntoVentaId = request.PuntoVentaId,
            TipoDteCodigo = request.TipoDteCodigo,
            VersionDte = request.TipoDteCodigo switch
            {
                TipoDteCodigos.ComprobanteCreditoFiscal => 3,
                TipoDteCodigos.NotaCredito => 3,
                TipoDteCodigos.NotaDebito => 3,
                TipoDteCodigos.NotaRemision => 3,
                TipoDteCodigos.FacturaExportacion => 3,
                TipoDteCodigos.ComprobanteDonacion => 2,
                TipoDteCodigos.ComprobanteLiquidacion => 2,
                TipoDteCodigos.DocumentoContableLiquidacion => 2,
                _ => 1,
            },
            AmbienteCodigo = ambiente,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            FechaEmision = NowSv().Date,
            HoraEmision = NowSv().TimeOfDay,
            TipoMonedaCodigo = string.IsNullOrEmpty(request.TipoMonedaCodigo) ? "USD" : request.TipoMonedaCodigo,
            CondicionOperacionCodigo = request.CondicionOperacionCodigo,
            FormaPagoCodigo = request.FormaPagoCodigo,
            PlazoDias = request.PlazoDias,
            DocumentoRelacionadoId = request.DocumentoRelacionadoId,
            NumeroDocumentoRelacionado = request.NumeroDocumentoRelacionado,
            TipoDteRelacionado = request.TipoDteRelacionado,
            TipoGeneracionRelacionado = request.TipoGeneracionRelacionado,
            DocumentoRelacionadoFecha = request.FechaDocumentoRelacionado,
            Observaciones = request.Observaciones,
            VentaTerceroNit = request.VentaTerceroNit,
            VentaTerceroNombre = request.VentaTerceroNombre,
            // Contingencia (MOMENTO 1): modelo diferido (2) + transmisión contingencia (2) + tipo/motivo CAT-005.
            ModeloFacturacion = request.TipoTransmision == 2 ? 2 : (request.ModeloFacturacion == 0 ? 1 : request.ModeloFacturacion),
            TipoTransmision = request.TipoTransmision == 0 ? 1 : request.TipoTransmision,
            TipoContingenciaCodigo = request.TipoContingenciaCodigo,
            MotivoContingencia = request.MotivoContingencia,
            EstadoCodigo = DteEstadoCodigos.Borrador,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };

        // Receptor snapshot
        Cliente? cliente = null;
        if (request.ClienteId.HasValue)
        {
            cliente = await _db.Clientes.AsNoTracking()
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
            doc.ReceptorDistritoCodigo = cliente.DistritoCodigo;
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
            doc.ReceptorDistritoCodigo = r.DistritoCodigo;
            doc.ReceptorDireccion = r.Direccion;
            doc.ReceptorCorreo = r.Correo;
            doc.ReceptorTelefono = r.Telefono;
        }

        // El receptor guarda códigos territoriales internos (p. ej. "SAN_SALVADOR"); Hacienda
        // exige el código MH numérico ("06"). Traducirlos antes de persistir el DTE.
        var receptorTerritorial = await ResolverReceptorTerritorialMhAsync(doc, empresaId, schema.Value, ct);
        if (receptorTerritorial.IsFailure)
            return Result<DteDocumentoDto>.Fail(receptorTerritorial.Error!, receptorTerritorial.ErrorCode);

        // NC/ND: MH exige la fecha REAL del documento relacionado. Si se referencia un DTE
        // electrónico por Id, tomamos su fecha de emisión (evita "017 FECHA NO ES CORRECTA"
        // cuando la nota ajusta un documento de un día anterior).
        if (doc.DocumentoRelacionadoFecha is null && request.DocumentoRelacionadoId is int relId)
        {
            doc.DocumentoRelacionadoFecha = await _db.DteDocumentos.AsNoTracking()
                .Where(x => x.Id == relId && x.EmpresaId == empresaId)
                .Select(x => (DateTime?)x.FechaEmision)
                .FirstOrDefaultAsync(ct);
        }

        // Datos del corte de liquidación (09). Los importes no se copian del request:
        // los deriva el calculador desde las líneas (ver DteLiquidacion).
        if (request.TipoDteCodigo == TipoDteCodigos.DocumentoContableLiquidacion && request.Liquidacion is { } liq)
        {
            doc.LiquidacionPeriodoInicio = liq.PeriodoInicio;
            doc.LiquidacionPeriodoFin = liq.PeriodoFin;
            doc.LiquidacionCodigo = liq.Codigo;
            doc.LiquidacionCantidadDocumentos = liq.CantidadDocumentos;
            doc.LiquidacionMontoSinPercepcion = liq.MontoSinPercepcion;
            doc.LiquidacionDescripcionSinPercepcion = liq.DescripcionSinPercepcion;
            doc.LiquidacionPorcentajeComision = liq.PorcentajeComision;
            doc.LiquidacionNombreEntrega = liq.NombreEntrega;
            doc.LiquidacionDocumentoEntrega = liq.DocumentoEntrega;
            doc.LiquidacionCodigoEmpleado = liq.CodigoEmpleado;
        }

        // Datos específicos de Factura de Exportación.
        if (request.TipoDteCodigo == TipoDteCodigos.FacturaExportacion)
        {
            var fex = await AplicarDatosExportacionAsync(doc, request, cliente, ct);
            if (fex.IsFailure)
                return Result<DteDocumentoDto>.Fail(fex.Error!, fex.ErrorCode);
        }

        // Número de control: correlativo atómico por (empresa, tipoDte)
        // UPSERT + incremento en una sola operación SQL para evitar race conditions.
        int correlativoNum;
        try { correlativoNum = await NextCorrelativoAsync(empresaId, request.TipoDteCodigo, ct); }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 50002)
        { return Result<DteDocumentoDto>.Fail("Correlativo agotado o fuera del rango soportado. Contacte soporte; no reinicie la secuencia.", "DTE_CORRELATIVO_AGOTADO"); }
        var correlativo = correlativoNum.ToString().PadLeft(15, '0');
        doc.NumeroControl = $"DTE-{request.TipoDteCodigo}-{bloqueEstab}-{correlativo}";

        // Detalles
        var numLinea = 1;

        if (request.TipoDteCodigo == TipoDteCodigos.ComprobanteRetencion)
        {
            // CR (07): cada línea es un documento sujeto a retención (no productos).
            foreach (var linea in request.Lineas)
            {
                var numero = (linea.DocRelacionadoNumero ?? "").Trim();
                var esElectronico = DteRetencion.EsCodigoGeneracion(numero);
                if (esElectronico) numero = numero.ToUpperInvariant(); // MH exige el UUID en mayúsculas
                var tipoRel = string.IsNullOrWhiteSpace(linea.DocRelacionadoTipoDte) ? "03" : linea.DocRelacionadoTipoDte.Trim();
                var codigoRet = string.IsNullOrWhiteSpace(linea.RetencionCodigoMH)
                    ? DteRetencion.CodigoIva1
                    : linea.RetencionCodigoMH.Trim().ToUpperInvariant();
                var monto = linea.MontoSujetoRetencion ?? (linea.Cantidad * linea.PrecioUnitario);

                doc.Detalles.Add(new DteDocumentoDetalle
                {
                    NumeroLinea = numLinea++,
                    Codigo = numero,
                    Descripcion = string.IsNullOrWhiteSpace(linea.Descripcion)
                        ? $"Retención de IVA sobre DTE {tipoRel} {numero}"
                        : linea.Descripcion,
                    UnidadMedidaCodigo = "59",
                    TipoItem = esElectronico ? 2 : 1, // tipoGeneracion: 2 electrónico, 1 físico
                    Cantidad = 1,
                    PrecioUnitario = monto,           // monto sujeto a retención
                    MontoDescuento = 0,
                    DocRelacionadoTipoDte = tipoRel,
                    DocRelacionadoFecha = linea.DocRelacionadoFecha,
                    RetencionCodigoMH = codigoRet,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actor,
                });
            }

            _calculator.Recalcular(doc);
            _db.DteDocumentos.Add(doc);
            await _db.SaveChangesAsync(ct);
            if (limiteTransaction is not null) await limiteTransaction.CommitAsync(ct);
            await Audit(empresaId, actor, "CREATE_BORRADOR", "OK",
                $"DTE {doc.TipoDteCodigo} #{doc.NumeroControl} en borrador (IVA retenido={doc.TotalPagar:0.00})", doc.Id);
            return await GetByIdAsync(empresaId, doc.Id, ct);
        }

        if (request.TipoDteCodigo == TipoDteCodigos.ComprobanteLiquidacion)
        {
            // CL (08): cada línea es un documento vendido por cuenta del mandante. Igual que
            // en el 07 el número del documento va en Codigo, pero aquí la línea sí lleva
            // importes (las ventas de ese documento, sin IVA: el 08 lo desglosa aparte).
            foreach (var linea in request.Lineas)
            {
                var numero = (linea.DocRelacionadoNumero ?? linea.Codigo ?? "").Trim();
                if (DteRetencion.EsCodigoGeneracion(numero)) numero = numero.ToUpperInvariant();
                var tipoRel = string.IsNullOrWhiteSpace(linea.DocRelacionadoTipoDte) ? "01" : linea.DocRelacionadoTipoDte.Trim();

                doc.Detalles.Add(new DteDocumentoDetalle
                {
                    NumeroLinea = numLinea++,
                    Codigo = numero,
                    Descripcion = string.IsNullOrWhiteSpace(linea.Descripcion)
                        ? $"Liquidación de DTE {tipoRel} {numero}"
                        : linea.Descripcion,
                    UnidadMedidaCodigo = "99",
                    TipoItem = linea.TipoItem == 0 ? 1 : linea.TipoItem,
                    Cantidad = linea.Cantidad <= 0 ? 1 : linea.Cantidad,
                    PrecioUnitario = linea.PrecioUnitario,
                    MontoDescuento = linea.MontoDescuento,
                    NoGravado = linea.NoGravado || string.Equals(linea.Clasificacion, "NO_SUJETA", StringComparison.OrdinalIgnoreCase),
                    Observaciones = linea.Observaciones,
                    DocRelacionadoTipoDte = tipoRel,
                    DocRelacionadoFecha = linea.DocRelacionadoFecha,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actor,
                });
            }

            _calculator.Recalcular(doc);
            _db.DteDocumentos.Add(doc);
            await _db.SaveChangesAsync(ct);
            if (limiteTransaction is not null) await limiteTransaction.CommitAsync(ct);
            await Audit(empresaId, actor, "CREATE_BORRADOR", "OK",
                $"DTE {doc.TipoDteCodigo} #{doc.NumeroControl} en borrador ({doc.Detalles.Count} documentos liquidados, total={doc.TotalPagar:0.00})", doc.Id);
            return await GetByIdAsync(empresaId, doc.Id, ct);
        }

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
                UnidadMedidaCodigo = string.IsNullOrEmpty(unidad) ? "59" : unidad,
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
        if (ventaOrigen is not null)
        {
            ventaOrigen.DteDocumentoId = doc.Id;
            ventaOrigen.UpdatedAt = DateTime.UtcNow;
            ventaOrigen.UpdatedBy = actor;
            await _db.SaveChangesAsync(ct);
        }
        if (limiteTransaction is not null) await limiteTransaction.CommitAsync(ct);
        await Audit(empresaId, actor, "CREATE_BORRADOR", "OK",
            $"DTE {doc.TipoDteCodigo} #{doc.NumeroControl} en borrador (total={doc.TotalPagar:0.00})", doc.Id);

            return await GetByIdAsync(empresaId, doc.Id, ct);
        });
    }

    private async Task<IDbContextTransaction?> BeginDteLimitTransactionAsync(int empresaId, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return null;

        IDbContextTransaction? ownTransaction = null;
        if (_db.Database.CurrentTransaction is null)
            ownTransaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            if (_db.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
            {
                var resource = $"NeoSTP:DTE-LIMIT:{empresaId}";
                await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @lockResult int;
                    EXEC @lockResult = sys.sp_getapplock
                        @Resource = {resource},
                        @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction',
                        @LockTimeout = 15000;
                    IF @lockResult < 0
                        THROW 50001, 'No fue posible reservar el cupo mensual de DTE.', 1;
                    """, ct);
            }

            return ownTransaction;
        }
        catch
        {
            if (ownTransaction is not null) await ownTransaction.DisposeAsync();
            throw;
        }
    }

    public Task<Result<DteDocumentoDto>> GenerarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => EjecutarCambioFiscalAsync(empresaId, id, () => GenerarCoreAsync(empresaId, id, actor, ct), ct);

    private async Task<Result<DteDocumentoDto>> GenerarCoreAsync(int empresaId, int id, string? actor, CancellationToken ct)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        var empresaFiscal = await CargarEmpresaFiscalAsync(empresaId, ct);
        if (empresaFiscal is null)
            return Result<DteDocumentoDto>.Fail("Empresa emisora no encontrada.", "EMPRESA_NOT_FOUND");
        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado or DteEstadoCodigos.Enviado or DteEstadoCodigos.Invalidado)
            return Result<DteDocumentoDto>.Fail("No se puede regenerar un documento enviado, procesado o invalidado.", "INVALID_STATE");
        if (doc.EnviadoAt.HasValue && NeoSTP.Application.Dte.Diagnostico.DteDiagnosticoGuia.Crear(
            doc.EstadoCodigo, doc.SelloRecibido, doc.EnviadoAt, doc.Json?.RespuestaHacienda).RequiereConsultaHacienda)
            return Result<DteDocumentoDto>.FailWithValue(MapToDto(doc),
                "Debe conciliar la recepción antes de regenerar este documento. Se conservó su JSON original.", "DTE_RESULTADO_INCIERTO");

        // Cargar config DTE para inyectar codEstable/codPuntoVenta/tipoEstablecimiento en el bloque emisor.
        var configForJson = await _db.DteConfiguracion.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        var contexto = ValidarContextoDocumento(doc, configForJson);
        if (contexto.IsFailure) return Result<DteDocumentoDto>.Fail(contexto.Error!, contexto.ErrorCode);
        var campaignAccess = await NeoSTP.Infrastructure.Dte.Certificacion.CertificationCampaignAccess.ValidateAsync(_db, empresaId, doc, ct);
        if (campaignAccess.IsFailure) return Result<DteDocumentoDto>.Fail(campaignAccess.Error!, campaignAccess.ErrorCode);
        var tipoAutorizado = await DteTypeAuthorization.ValidateAsync(_db, empresaId, doc.TipoDteCodigo, ct);
        if (tipoAutorizado.IsFailure) return Result<DteDocumentoDto>.Fail(tipoAutorizado.Error!, tipoAutorizado.ErrorCode);
        _calculator.Recalcular(doc);

        // Validar datos MH-obligatorios del emisor que no podemos inventar (correo/teléfono/NIT).
        var emisorErrors = ValidarEmisorParaMh(empresaFiscal);
        if (emisorErrors.Count > 0)
            return Result<DteDocumentoDto>.FailWithValue(MapToDto(doc),
                "Corrija los datos de la empresa emisora: " + string.Join(" ", emisorErrors),
                "VALIDATION", emisorErrors);

        // Sanear una proyección fiscal sin tracking para no persistir estos cambios ni
        // materializar el branding. No se asocia a la navegación mientras consultamos
        // catálogos, porque el DbContext puede tener ya otra instancia de Empresa cargada.
        await SanearEmisorParaMhAsync(empresaFiscal, configForJson, empresaId, ct,
            skipTerritory: DteGeneratorService.EsTipoConTerritorioVerificado(doc.TipoDteCodigo));
        var schema = _schemaPolicy.Resolve(empresaId, empresaFiscal.Nit, doc.AmbienteCodigo);
        if (schema.IsFailure) return Result<DteDocumentoDto>.Fail(schema.Error!, schema.ErrorCode);
        var territorioFiscal = await ResolverTerritorioParaGeneracionAsync(empresaFiscal, doc, empresaId, schema.Value, ct);
        if (territorioFiscal.IsFailure)
            return Result<DteDocumentoDto>.Fail(territorioFiscal.Error!, territorioFiscal.ErrorCode);
        // Sanear receptor DEFENSIVAMENTE: quitar guiones del NIT/NRC persistidos con
        // NormalizeNit (formato con guiones para presentación).
        SanearReceptorParaMh(doc);

        Result<string> json;
        var empresaOriginal = doc.Empresa;
        var receptorTerritorialOriginal = (doc.ReceptorDepartamentoCodigo, doc.ReceptorMunicipioCodigo, doc.ReceptorDistritoCodigo);
        doc.Empresa = empresaFiscal;
        doc.ReceptorDepartamentoCodigo = territorioFiscal.Value!.Department;
        doc.ReceptorMunicipioCodigo = territorioFiscal.Value.Municipality;
        doc.ReceptorDistritoCodigo = territorioFiscal.Value.District;
        try
        {
            json = _generator.Generar(doc, configForJson);
        }
        finally
        {
            // La proyección solo existe para construir el JSON. Restaurar la navegación
            // evita que EF intente adjuntarla y choque con una Empresa ya rastreada.
            doc.Empresa = empresaOriginal;
            (doc.ReceptorDepartamentoCodigo, doc.ReceptorMunicipioCodigo, doc.ReceptorDistritoCodigo) = receptorTerritorialOriginal;
        }
        if (json.IsFailure)
            return Result<DteDocumentoDto>.Fail(json.Error ?? "Error al generar JSON.", json.ErrorCode);
        if (!DteFiscalContext.TryGetGeneratedVersion(json.Value, doc, out var generatedVersion))
            return Result<DteDocumentoDto>.Fail("El JSON generado no tiene una versión o identidad fiscal compatible con el documento.", "DTE_PAYLOAD_INCOMPATIBLE");

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
            if (!string.IsNullOrWhiteSpace(doc.Json.RespuestaHacienda)
                && !await _db.DteErrorOcurrencias.AnyAsync(o => o.EmpresaId == empresaId && o.DteDocumentoId == id
                    && o.RespuestaMhJson == doc.Json.RespuestaHacienda, ct))
                RegistrarRespuestaNoProcesada(doc, "RESPUESTA_HISTORICA", "Respuesta anterior conservada antes de regenerar el DTE.");
            doc.Json.JsonDte = json.Value!;
            doc.Json.JsonFirmado = null;
            doc.Json.FirmadoAt = null;
            doc.Json.RespuestaHacienda = null;
            doc.Json.RespuestaAt = null;
            doc.Json.GeneradoAt = DateTime.UtcNow;
            doc.Json.UpdatedAt = DateTime.UtcNow;
            doc.Json.UpdatedBy = actor;
        }

        doc.EstadoCodigo = DteEstadoCodigos.Generado;
        doc.VersionDte = generatedVersion;
        // Nuevo intento explícito tras un rechazo confirmado. El intento anterior permanece
        // en diagnóstico; EnviadoAt representa la transmisión del JSON vigente.
        doc.EnviadoAt = null;
        doc.ValidadoAt = null;
        doc.GeneradoAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "GENERAR", "OK",
            $"JSON generado para DTE {doc.NumeroControl} (longitud={json.Value!.Length})", doc.Id);

        return await GetByIdAsync(empresaId, doc.Id, ct);
    }

    public Task<Result<DteDocumentoDto>> ValidarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => EjecutarCambioFiscalAsync(empresaId, id, () => ValidarCoreAsync(empresaId, id, actor, ct), ct);

    private async Task<Result<DteDocumentoDto>> ValidarCoreAsync(int empresaId, int id, string? actor, CancellationToken ct)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado or DteEstadoCodigos.Enviado or DteEstadoCodigos.Invalidado)
            return Result<DteDocumentoDto>.Fail("No se puede revalidar un documento enviado, procesado o invalidado.", "INVALID_STATE");
        if (doc.EstadoCodigo == DteEstadoCodigos.Firmado)
            return Result<DteDocumentoDto>.Fail("El documento ya está firmado. Para cambiar sus datos, regenere el JSON antes de volver a validarlo.", "INVALID_STATE");
        if (RequiereConciliacion(doc))
            return Result<DteDocumentoDto>.FailWithValue(MapToDto(doc), "Consulte la recepción en Hacienda antes de modificar este DTE.", "DTE_RESULTADO_INCIERTO");
        var configValidacion = await _db.DteConfiguracion.AsNoTracking().FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        var contexto = ValidarContextoDocumento(doc, configValidacion);
        if (contexto.IsFailure) return Result<DteDocumentoDto>.Fail(contexto.Error!, contexto.ErrorCode);
        var campaignAccess = await NeoSTP.Infrastructure.Dte.Certificacion.CertificationCampaignAccess.ValidateAsync(_db, empresaId, doc, ct);
        if (campaignAccess.IsFailure) return Result<DteDocumentoDto>.Fail(campaignAccess.Error!, campaignAccess.ErrorCode);
        var tipoAutorizado = await DteTypeAuthorization.ValidateAsync(_db, empresaId, doc.TipoDteCodigo, ct);
        if (tipoAutorizado.IsFailure) return Result<DteDocumentoDto>.Fail(tipoAutorizado.Error!, tipoAutorizado.ErrorCode);

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

    public Task<Result<DteDocumentoDto>> FirmarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => EjecutarCambioFiscalAsync(empresaId, id, () => FirmarCoreAsync(empresaId, id, actor, ct), ct);

    private async Task<Result<DteDocumentoDto>> FirmarCoreAsync(int empresaId, int id, string? actor, CancellationToken ct)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        if (doc.EstadoCodigo is DteEstadoCodigos.Enviado or DteEstadoCodigos.Procesado or DteEstadoCodigos.Invalidado)
            return Result<DteDocumentoDto>.Fail("No se puede firmar un documento enviado, procesado o invalidado.", "INVALID_STATE");
        if (RequiereConciliacion(doc))
            return Result<DteDocumentoDto>.FailWithValue(MapToDto(doc), "Consulte la recepción en Hacienda antes de modificar este DTE.", "DTE_RESULTADO_INCIERTO");

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
        var contexto = ValidarContextoDocumento(doc, config);
        if (contexto.IsFailure) return Result<DteDocumentoDto>.Fail(contexto.Error!, contexto.ErrorCode);
        if (!DteFiscalContext.CoincideJson(doc.Json?.JsonDte, doc.AmbienteCodigo, doc))
            return Result<DteDocumentoDto>.Fail("El JSON no corresponde al ambiente o identidad del DTE. Regenere el JSON antes de firmar.", "DTE_PAYLOAD_INCOMPATIBLE");
        var campaignAccess = await NeoSTP.Infrastructure.Dte.Certificacion.CertificationCampaignAccess.ValidateAsync(_db, empresaId, doc, ct);
        if (campaignAccess.IsFailure) return Result<DteDocumentoDto>.Fail(campaignAccess.Error!, campaignAccess.ErrorCode);
        var tipoAutorizado = await DteTypeAuthorization.ValidateAsync(_db, empresaId, doc.TipoDteCodigo, ct);
        if (tipoAutorizado.IsFailure) return Result<DteDocumentoDto>.Fail(tipoAutorizado.Error!, tipoAutorizado.ErrorCode);
        // A signed generation is immutable. Re-signing it could replace the JWS while
        // another host is preparing its send claim. A new signature requires regeneration.
        if (doc.EstadoCodigo == DteEstadoCodigos.Firmado)
            return DteFiscalContext.CoincideJws(doc.Json?.JsonFirmado, doc.AmbienteCodigo, doc)
                ? await GetByIdAsync(empresaId, id, ct)
                : Result<DteDocumentoDto>.Fail("La firma existente no es compatible. Regenere el documento antes de firmarlo nuevamente.", "DTE_PAYLOAD_INCOMPATIBLE");
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

    public Task<Result<DteDocumentoDto>> EnviarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => EjecutarCambioFiscalAsync(empresaId, id, () => EnviarCoreAsync(empresaId, id, actor, ct), ct);

    private async Task<Result<DteDocumentoDto>> EnviarCoreAsync(int empresaId, int id, string? actor, CancellationToken ct)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado or DteEstadoCodigos.Invalidado)
            return Result<DteDocumentoDto>.Fail("No se puede enviar un documento procesado o invalidado.", "INVALID_STATE");

        if (RequiereConciliacion(doc))
            return Result<DteDocumentoDto>.FailWithValue(MapToDto(doc),
                "Debe conciliar la recepción en Hacienda antes de volver a enviar este DTE.", "DTE_RESULTADO_INCIERTO");

        var campaignAccess = await NeoSTP.Infrastructure.Dte.Certificacion.CertificationCampaignAccess.ValidateAsync(_db, empresaId, doc, ct);
        if (campaignAccess.IsFailure) return Result<DteDocumentoDto>.Fail(campaignAccess.Error!, campaignAccess.ErrorCode);
        var tipoAutorizado = await DteTypeAuthorization.ValidateAsync(_db, empresaId, doc.TipoDteCodigo, ct);
        if (tipoAutorizado.IsFailure) return Result<DteDocumentoDto>.Fail(tipoAutorizado.Error!, tipoAutorizado.ErrorCode);
        if (doc.EstadoCodigo is not DteEstadoCodigos.Enviado)
        {
            var gen = await GenerarAsync(empresaId, id, actor, ct);
            if (gen.IsFailure) return gen;
            doc = await _db.DteDocumentos
                .Include(d => d.Json)
                .FirstAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        }

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

        // Guardrail anti-mock: nunca enviar una firma mock/none al Hacienda real (cliente Http).
        // El firmador Mock produce header {"alg":"none-mock",...}; si se enviara, MH responde 802
        // y se desperdicia un intento de la matriz de pruebas. Se bloquea antes de llamar a MH.
        if (_reception is NeoSTP.Infrastructure.Dte.HttpHaciendaReceptionClient
            && EsFirmaMock(doc.Json?.JsonFirmado))
        {
            await Audit(empresaId, actor, "ENVIAR", "FAIL",
                "Bloqueado: firma mock/none no enviable a Hacienda real. Configurar Dte:Signer=HaciendaCert.", doc.Id);
            return Result<DteDocumentoDto>.Fail(
                "No se puede enviar a Hacienda con firma mock. Configure el firmador real (Dte:Signer=HaciendaCert) y vuelva a firmar.",
                "FIRMA_MOCK_NO_ENVIABLE");
        }

        var contextoEnvio = DteFiscalContext.Validar(doc.AmbienteCodigo, config);
        if (contextoEnvio.IsFailure) return Result<DteDocumentoDto>.Fail(contextoEnvio.Error!, contextoEnvio.ErrorCode);
        if (!DteFiscalContext.CoincideJws(doc.Json?.JsonFirmado, doc.AmbienteCodigo, doc))
            return Result<DteDocumentoDto>.Fail("La firma contiene un ambiente o identidad diferente al DTE. No se transmitió.", "DTE_PAYLOAD_INCOMPATIBLE");
        var tokenResult = await ObtenerTokenAsync(config, ct);
        if (!tokenResult.Success)
            return Result<DteDocumentoDto>.Fail(tokenResult.Mensaje ?? "No se pudo obtener token Hacienda.", "HACIENDA_AUTH_FAILED");

        // Marca durable antes de cruzar la frontera HTTP: si se pierde la respuesta o
        // se cancela la petición, otro intento no debe regenerar y volver a emitir a ciegas.
        var campaignBeforeClaim = await NeoSTP.Infrastructure.Dte.Certificacion.CertificationCampaignAccess.ValidateAsync(_db, empresaId, doc, ct);
        if (campaignBeforeClaim.IsFailure) return Result<DteDocumentoDto>.Fail(campaignBeforeClaim.Error!, campaignBeforeClaim.ErrorCode);
        doc.EstadoCodigo = DteEstadoCodigos.Enviado;
        doc.EnviadoAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = actor;
        // EF includes the original state, EnviadoAt and GeneradoAt in the UPDATE predicate.
        // Only the winner reaches HTTP; a conflict must never retry this whole operation.
        await _db.SaveChangesAsync(ct);

        var nitEmisor = await _db.Empresas.AsNoTracking().Where(e => e.Id == empresaId)
            .Select(e => e.Nit).FirstOrDefaultAsync(ct);
        var resp = await _reception.EnviarAsync(new HaciendaReceptionRequest
        {
            Nit = nitEmisor,                                // NIT del emisor para lookup de cert
            Ambiente = DteAmbientes.CodigoMh(doc.AmbienteCodigo),
            AmbienteCodigo = config.AmbienteCodigo,
            IdEnvio = doc.Id,
            Version = doc.VersionDte,
            TipoDte = doc.TipoDteCodigo,
            Documento = doc.Json!.JsonFirmado!,
            CodigoGeneracion = doc.CodigoGeneracion,
            Token = tokenResult.Token!,
        }, ct);

        // Conservar también el diagnóstico cuando no hubo cuerpo HTTP (timeout/red).
        doc.Json.RespuestaHacienda = NeoSTP.Infrastructure.Dte.HaciendaReceptionEvidence.ForStorage(resp);
        doc.Json.RespuestaAt = DateTime.UtcNow;
        doc.Json.UpdatedAt = DateTime.UtcNow;
        doc.Json.UpdatedBy = actor;
        doc.EnviadoAt = DateTime.UtcNow;

        // Map response -> estado interno
        var nuevoEstado = (resp.Estado ?? string.Empty).ToUpperInvariant() switch
        {
            _ when resp.CodigoHttp is 0 or >= 500 || resp.ClasificaMsg is "TIMEOUT" or "NETWORK_ERROR" or "RESPUESTA_INVALIDA" => DteEstadoCodigos.Enviado,
            "PROCESADO" when resp.Success && !string.IsNullOrWhiteSpace(resp.SelloRecibido) => DteEstadoCodigos.Procesado,
            "PROCESADO" => DteEstadoCodigos.Enviado,
            "ENVIADO" or "RECIBIDO" => DteEstadoCodigos.Enviado,
            "RECHAZADO" => DteEstadoCodigos.Rechazado,
            "CONTINGENCIA" => DteEstadoCodigos.Contingencia,
            "NO_AUTORIZADO" => DteEstadoCodigos.Error,
            _ => resp.Success ? DteEstadoCodigos.Enviado : DteEstadoCodigos.Error,
        };
        doc.EstadoCodigo = nuevoEstado;
        _metrics?.DteEmitido(empresaId, doc.TipoDteCodigo, nuevoEstado);
        if (nuevoEstado == DteEstadoCodigos.Procesado)
        {
            doc.SelloRecibido = resp.SelloRecibido;
            doc.ProcesadoAt = resp.FhProcesamiento ?? DateTime.UtcNow;
        }
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = actor;

        if (nuevoEstado != DteEstadoCodigos.Procesado)
            RegistrarRespuestaNoProcesada(doc, resp.CodigoMsg, resp.DescripcionMsg);
        await _db.SaveChangesAsync(ct);
        if (nuevoEstado == DteEstadoCodigos.Procesado)
            await EnviarCorreoAutomaticoAsync(empresaId, doc.Id, actor);

        await Audit(empresaId, actor, "ENVIAR",
            resp.Success && nuevoEstado == DteEstadoCodigos.Procesado ? "OK" : "FAIL",
            $"[{resp.CodigoHttp}] estado={resp.Estado} cod={resp.CodigoMsg} desc={resp.DescripcionMsg}",
            doc.Id);

        // NeoConnect: notificar webhooks suscritos al cambio de estado (best-effort)
        if (nuevoEstado is DteEstadoCodigos.Procesado or DteEstadoCodigos.Rechazado or DteEstadoCodigos.Contingencia)
        {
            var connectEvento = nuevoEstado switch
            {
                DteEstadoCodigos.Procesado => ConnectEventos.DteProcesado,
                DteEstadoCodigos.Rechazado => ConnectEventos.DteRechazado,
                _ => ConnectEventos.DteContingencia,
            };
            await _webhookDispatcher.DispatchAsync(new ConnectDteEventoPayload
            {
                Evento = connectEvento,
                EmpresaId = empresaId,
                DteId = doc.Id,
                CodigoGeneracion = doc.CodigoGeneracion,
                TipoDte = doc.TipoDteCodigo,
                Estado = nuevoEstado,
                OcurrioAt = DateTime.UtcNow,
            }, ct);
        }

        var resultado = await GetByIdAsync(empresaId, doc.Id, ct);
        if (nuevoEstado == DteEstadoCodigos.Procesado || resultado.IsFailure) return resultado;
        var diagnostico = resultado.Value!.Diagnostico!;
        var errorEnvio = diagnostico.RequiereConsultaHacienda ? "DTE_RESULTADO_INCIERTO"
            : diagnostico.Codigo == "HACIENDA_AUTH_FAILED" ? "HACIENDA_AUTH_FAILED"
            : diagnostico.Campos.Count > 0 ? "HACIENDA_DATOS_INVALIDOS" : "HACIENDA_RECHAZO";
        return Result<DteDocumentoDto>.FailWithValue(resultado.Value, diagnostico.Mensaje, errorEnvio, resp.Observaciones);
    }

    private void RegistrarRespuestaNoProcesada(DteDocumento doc, string? codigo, string? mensaje)
    {
        var codigoError = codigo ?? "DTE_RESULTADO_INCIERTO";
        var descripcion = mensaje ?? "La recepción del DTE no está confirmada. Consulte el diagnóstico.";
        _db.DteErrorOcurrencias.Add(new NeoSTP.Domain.Core.Dte.Diagnostico.DteErrorOcurrencia
        {
            EmpresaId = doc.EmpresaId, DteDocumentoId = doc.Id,
            CodigoError = codigoError.Length > 50 ? codigoError[..50] : codigoError,
            Mensaje = descripcion.Length > 1000 ? descripcion[..1000] : descripcion,
            RespuestaMhJson = doc.Json?.RespuestaHacienda, JsonEnviado = doc.Json?.JsonFirmado ?? doc.Json?.JsonDte,
            OcurrioAt = doc.Json?.RespuestaAt ?? doc.EnviadoAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = doc.UpdatedBy,
        });
    }

    private Task<(bool Success, string? Token, string? Mensaje)> ObtenerTokenAsync(
        DteConfiguracion config, CancellationToken ct)
        => HaciendaTokenProvider.GetAsync(_db, config, _haciendaAuth, _protector, ct);

    public async Task<Result<DteArchivosDto>> ObtenerArchivosAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var doc = await CargarDocumentoParaRepresentacionAsync(empresaId, id, ct);
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
        var doc = await CargarDocumentoParaRepresentacionAsync(empresaId, id, ct);
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
        var logoValido = BrandingImageValidator.TryValidate(doc.Empresa?.LogoBlob, null,
            out var logoContentType, out _) ? doc.Empresa!.LogoBlob : null;
        var tieneLogo = logoValido is { Length: > 0 };
        var body = BuildBody(doc, emisor, tieneLogo);

        var message = new EmailMessage
        {
            To = to,
            Cc = CopiaCorreoEmisor(to, doc.Empresa?.Correo),
            Subject = subject,
            HtmlBody = body,
        };
        message.Attachments.AddRange(attachments);
        if (tieneLogo)
            message.InlineImages.Add(new EmailInlineImage
            {
                ContentId = "logo",
                MediaType = logoContentType,
                Content = logoValido!,
            });
        var result = await _email.EnviarAsync(empresaId, message, ct);

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

    private async Task<DteDocumento?> CargarDocumentoParaRepresentacionAsync(int empresaId, int id, CancellationToken ct)
    {
        var doc = await _db.DteDocumentos.Include(d => d.Detalles).Include(d => d.Json)
            .AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return null;
        var empresa = await _db.Empresas.AsNoTracking().Where(e => e.Id == empresaId)
            .Select(e => new Empresa
            {
                Id = e.Id,
                RazonSocial = e.RazonSocial,
                NombreComercial = e.NombreComercial,
                Nit = e.Nit,
                Nrc = e.Nrc,
                CodigoActividad = e.CodigoActividad,
                ActividadEconomica = e.ActividadEconomica,
                FirmaTexto = e.FirmaTexto,
                Correo = e.Correo,
                LogoContentType = e.LogoContentType,
                FirmaContentType = e.FirmaContentType,
                LogoBlob = e.LogoBlob != null && e.LogoBlob.Length <= BrandingImageValidator.MaxBytes ? e.LogoBlob : null,
                FirmaBlob = e.FirmaBlob != null && e.FirmaBlob.Length <= BrandingImageValidator.MaxBytes ? e.FirmaBlob : null,
            }).FirstOrDefaultAsync(ct);
        if (empresa is null) return null;
        doc.Empresa = empresa;
        return doc;
    }

    private Task<Empresa?> CargarEmpresaFiscalAsync(int empresaId, CancellationToken ct)
        => _db.Empresas.AsNoTracking().Where(e => e.Id == empresaId)
            .Select(e => new Empresa
            {
                Id = e.Id,
                Nit = e.Nit,
                Nrc = e.Nrc,
                RazonSocial = e.RazonSocial,
                NombreComercial = e.NombreComercial,
                CodigoActividad = e.CodigoActividad,
                ActividadEconomica = e.ActividadEconomica,
                Departamento = e.Departamento,
                Municipio = e.Municipio,
                Distrito = e.Distrito,
                Direccion = e.Direccion,
                Telefono = e.Telefono,
                Correo = e.Correo,
                EstadoCodigo = e.EstadoCodigo,
            }).FirstOrDefaultAsync(ct);

    internal static string BuildBody(DteDocumento d, string emisor, bool incluirLogo = false)
    {
        var receptor = d.ReceptorNombre ?? "Estimado(a) cliente";
        var tipo = TipoDteNombreEmail(d.TipoDteCodigo);
        var logoHtml = incluirLogo
            ? "<div style=\"margin-bottom:8px\"><img src=\"cid:logo\" alt=\"\" style=\"max-height:40px;max-width:200px\"></div>"
            : "";
        var (badgeBg, badgeFg) = d.EstadoCodigo switch
        {
            DteEstadoCodigos.Procesado => ("#DCFCE7", "#15803D"),
            DteEstadoCodigos.Rechazado => ("#FEE2E2", "#B91C1C"),
            DteEstadoCodigos.Contingencia => ("#FEF3C7", "#B45309"),
            DteEstadoCodigos.Invalidado => ("#E2E8F0", "#334155"),
            _ => ("#E0E7FF", "#3730A3"),
        };

        // Fila del cuadro de datos (label + valor)
        static string Fila(string label, string valor, bool mono = false)
            => $"""
               <tr>
                 <td style="padding:7px 14px;border-bottom:1px solid #EEF2F7;color:#64748B;font-size:12px;white-space:nowrap">{label}</td>
                 <td style="padding:7px 14px;border-bottom:1px solid #EEF2F7;color:#1E293B;font-size:13px;{(mono ? "font-family:Consolas,'Courier New',monospace;" : "")}text-align:right">{valor}</td>
               </tr>
               """;

        var selloFila = string.IsNullOrEmpty(d.SelloRecibido)
            ? ""
            : Fila("Sello de recepción", $"<span style=\"word-break:break-all;font-size:11px\">{d.SelloRecibido}</span>", mono: true);

        return $$"""
            <!doctype html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#F1F5F9;font-family:'Segoe UI',Arial,sans-serif;color:#1E293B">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#F1F5F9;padding:24px 0">
                <tr><td align="center">
                  <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:600px;max-width:96%;background:#FFFFFF;border-radius:12px;overflow:hidden;box-shadow:0 1px 4px rgba(15,23,42,.08)">

                    <!-- Banda de marca -->
                    <tr><td style="background:#131B2E;padding:20px 24px">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                        <td style="vertical-align:middle">
                          {{logoHtml}}
                          <span style="color:#FFFFFF;font-size:17px;font-weight:700">{{emisor}}</span>
                        </td>
                        <td align="right" style="color:#A5B4FC;font-size:12px;vertical-align:middle">{{tipo}}</td>
                      </tr></table>
                    </td></tr>

                    <!-- Saludo -->
                    <tr><td style="padding:24px 24px 8px">
                      <p style="margin:0 0 6px">Estimado(a) <strong>{{receptor}}</strong>,</p>
                      <p style="margin:0;color:#475569;font-size:14px">Adjuntamos su Documento Tributario Electrónico emitido por <strong>{{emisor}}</strong>. Encontrará la representación gráfica (PDF) y el archivo oficial (JSON) como adjuntos de este correo.</p>
                    </td></tr>

                    <!-- Cuadro de datos del DTE -->
                    <tr><td style="padding:8px 24px 4px">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border:1px solid #E2E8F0;border-radius:10px;overflow:hidden">
                        <tr><td colspan="2" style="background:#F8FAFC;padding:9px 14px;font-size:11px;font-weight:700;color:#6B38D4;letter-spacing:.04em">DATOS DEL DOCUMENTO</td></tr>
                        {{Fila("Tipo de DTE", tipo)}}
                        {{Fila("Número de control", $"<span style=\"font-size:12px\">{d.NumeroControl}</span>", mono: true)}}
                        {{Fila("Código de generación", $"<span style=\"font-size:12px\">{d.CodigoGeneracion}</span>", mono: true)}}
                        {{Fila("Fecha de emisión", $"{d.FechaEmision:dd/MM/yyyy} {d.HoraEmision:hh\\:mm}")}}
                        {{Fila("Estado", $"<span style=\"background:{badgeBg};color:{badgeFg};padding:2px 9px;border-radius:10px;font-size:11px;font-weight:700\">{d.EstadoCodigo}</span>")}}
                        {{selloFila}}
                      </table>
                    </td></tr>

                    <!-- Total destacado -->
                    <tr><td style="padding:12px 24px 4px">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#131B2E;border-radius:10px">
                        <tr>
                          <td style="padding:14px 16px;color:#FFFFFF;font-size:13px;font-weight:600">TOTAL A PAGAR</td>
                          <td align="right" style="padding:14px 16px;color:#FFFFFF;font-size:20px;font-weight:700">$ {{d.TotalPagar.ToString("N2")}}</td>
                        </tr>
                      </table>
                    </td></tr>

                    <!-- Footer -->
                    <tr><td style="padding:18px 24px 24px">
                      <p style="margin:0;color:#94A3B8;font-size:11px;line-height:1.6">
                        Documento Tributario Electrónico generado por NeoSTP Cloud.<br>
                        Consulte la validez de este documento en el portal del Ministerio de Hacienda de El Salvador.<br>
                        Enviado el {{DateTime.UtcNow:dd/MM/yyyy HH:mm}} UTC.
                      </p>
                    </td></tr>

                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string TipoDteNombreEmail(string codigo) => codigo switch
    {
        TipoDteCodigos.FacturaConsumidorFinal => "Factura (DTE-01)",
        TipoDteCodigos.ComprobanteCreditoFiscal => "Comprobante de Crédito Fiscal (DTE-03)",
        TipoDteCodigos.NotaCredito => "Nota de Crédito (DTE-05)",
        TipoDteCodigos.NotaDebito => "Nota de Débito (DTE-06)",
        TipoDteCodigos.FacturaSujetoExcluido => "Factura Sujeto Excluido (DTE-14)",
        TipoDteCodigos.ComprobanteRetencion => "Comprobante de Retención (DTE-07)",
        TipoDteCodigos.ComprobanteLiquidacion => "Comprobante de Liquidación (DTE-08)",
        TipoDteCodigos.DocumentoContableLiquidacion => "Documento Contable de Liquidación (DTE-09)",
        _ => $"DTE-{codigo}",
    };

    public async Task<Result> InvalidarAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default)
    {
        try { return await InvalidarCoreAsync(empresaId, id, motivo, actor, ct); }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Result.Fail("Otro proceso modificó este DTE. Recargue su estado antes de continuar; no se invalidó.", "DTE_CONCURRENCY_CONFLICT");
        }
    }

    private async Task<Result> InvalidarCoreAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct)
    {
        var doc = await _db.DteDocumentos.Include(d => d.Json).FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado)
            return Result.Fail("No se puede invalidar un DTE ya procesado en Hacienda. Use anulación.", "INVALID_STATE");
        if (doc.EstadoCodigo == DteEstadoCodigos.Invalidado)
            return Result.Fail("El documento ya está invalidado.", "INVALID_STATE");
        if (RequiereConciliacion(doc))
            return Result.Fail("No se puede invalidar localmente un envío sin respuesta confirmada. Consulte primero su recepción en Hacienda.", "DTE_RESULTADO_INCIERTO");

        doc.EstadoCodigo = DteEstadoCodigos.Invalidado;
        doc.Observaciones = string.IsNullOrEmpty(motivo) ? doc.Observaciones : $"[INVALIDADO] {motivo}";
        doc.UpdatedAt = DateTime.UtcNow; doc.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INVALIDAR", "OK", motivo ?? "Sin motivo", doc.Id);

        // NeoConnect: notificar webhooks suscritos al evento de invalidación (best-effort)
        await _webhookDispatcher.DispatchAsync(new ConnectDteEventoPayload
        {
            Evento = ConnectEventos.DteInvalidado,
            EmpresaId = empresaId,
            DteId = doc.Id,
            CodigoGeneracion = doc.CodigoGeneracion,
            TipoDte = doc.TipoDteCodigo,
            Estado = DteEstadoCodigos.Invalidado,
            OcurrioAt = DateTime.UtcNow,
        }, ct);

        return Result.Ok();
    }

    public async Task<Result> GuardarNotaInternaAsync(int empresaId, int id, string? nota, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos.FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        var limpia = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        if (limpia is { Length: > 2000 })
            return Result.Fail("La nota interna no puede exceder 2000 caracteres.", "VALIDATION");

        doc.NotaInterna = limpia;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "NOTA_INTERNA", "OK", limpia is null ? "Nota borrada" : "Nota actualizada", doc.Id);
        return Result.Ok();
    }


    private Task Audit(int empresaId, string? actor, string accion, string resultado, string? detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "DteDocumento", EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });

    /// <summary>
    /// Resuelve país (código/nombre MH) y tipo de persona del receptor de una FEX.
    /// Prioridad: request explícito → receptor manual → país de residencia del cliente
    /// (resuelto contra el catálogo PAIS). Interno para poder probarse en unit tests.
    /// </summary>
    internal async Task<Result> AplicarDatosExportacionAsync(
        DteDocumento doc, CreateDteDocumentoRequest request, Cliente? cliente, CancellationToken ct)
    {
        var paisCodigo = request.ReceptorPaisCodigo;
        var paisNombre = request.ReceptorPaisNombre;
        var tipoPersona = request.ReceptorTipoPersona;

        // Fallback si el receptor manual lo trae dentro del DTO.
        if (request.ReceptorManual is not null)
        {
            paisCodigo = string.IsNullOrWhiteSpace(paisCodigo)
                ? request.ReceptorManual.PaisCodigo
                : paisCodigo;

            paisNombre = string.IsNullOrWhiteSpace(paisNombre)
                ? request.ReceptorManual.PaisNombre
                : paisNombre;

            tipoPersona ??= request.ReceptorManual.TipoPersona;
        }

        // Precarga desde el catálogo de clientes: si el cliente tiene país de
        // residencia, la FEX se puede emitir sin volver a capturar sus datos.
        if (string.IsNullOrWhiteSpace(paisCodigo) && !string.IsNullOrEmpty(cliente?.PaisCodigo))
        {
            var pais = await _db.CatalogoItems.AsNoTracking()
                .Where(i => i.Catalogo.Codigo == "PAIS" && i.Codigo == cliente.PaisCodigo && i.Activo)
                .Select(i => new { i.Codigo, i.Valor, i.MetadataJson })
                .FirstOrDefaultAsync(ct);
            if (pais is null)
                return Result.Fail(
                    $"El país '{cliente.PaisCodigo}' del cliente no existe o está inactivo en el catálogo PAIS.", "VALIDATION");

            paisCodigo = CatalogoMetadataValue(pais.MetadataJson, "codigoMH") ?? pais.Codigo;
            paisNombre = CatalogoMetadataValue(pais.MetadataJson, "nombreMH") ?? pais.Valor;
        }

        if (cliente is not null)
            tipoPersona ??= cliente.TipoPersona ?? (cliente.EsContribuyente ? 2 : 1);

        if (string.IsNullOrWhiteSpace(paisCodigo))
            return Result.Fail("La factura de exportación requiere país receptor.", "VALIDATION");

        if (string.IsNullOrWhiteSpace(paisNombre))
            return Result.Fail("La factura de exportación requiere nombre de país receptor.", "VALIDATION");

        if (!tipoPersona.HasValue)
            return Result.Fail("La factura de exportación requiere tipo de persona del receptor.", "VALIDATION");

        doc.ReceptorPaisCodigo = paisCodigo;
        doc.ReceptorPaisNombre = paisNombre.ToUpperInvariant();
        doc.ReceptorTipoPersona = tipoPersona;
        return Result.Ok();
    }

    private static string? CatalogoMetadataValue(string? metadataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty(key, out var v) ? v.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Obtiene el siguiente correlativo de forma atómica usando UPSERT + UPDATE SQL.
    /// Evita la race condition del COUNT(*)+1 en entornos concurrentes.
    /// </summary>
    /// <summary>
    /// Detecta si un JWS fue producido por un firmador mock/none inspeccionando el header.
    /// El firmador real de Hacienda usa <c>alg=RS512</c>; el mock usa un alg que contiene "none"/"mock".
    /// </summary>
    internal static bool EsFirmaMock(string? jws)
    {
        if (string.IsNullOrEmpty(jws)) return false;
        var dot = jws.IndexOf('.');
        if (dot <= 0) return false;
        try
        {
            var b64 = jws[..dot].Replace('-', '+').Replace('_', '/');
            b64 = (b64.Length % 4) switch { 2 => b64 + "==", 3 => b64 + "=", _ => b64 };
            var headerJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            return headerJson.Contains("none", StringComparison.OrdinalIgnoreCase)
                || headerJson.Contains("mock", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>
    /// Construye el bloque de 8 caracteres del numeroControl según el formato oficial MH:
    /// <c>(M|B|S|P)([0-9]{3})(P)([0-9]{3})</c> = letraTipoEstablecimiento + codEstable(3) + 'P' + codPuntoVenta(3).
    /// Ej: <c>M001P001</c>. Cuando no hay códigos asignados se usan 001/001.
    /// </summary>
    private static string BuildBloqueEstablecimiento(Domain.Core.Dte.DteConfiguracion? config)
    {
        // Letra de tipo de establecimiento (CAT-009). Acepta el código numérico MH o el código interno textual.
        var letra = (config?.TipoEstablecimientoCodigo) switch
        {
            "01" or "SUCURSAL" => "S",   // Sucursal / Agencia
            "02" or "CASA_MATRIZ" => "M",// Casa Matriz
            "04" or "BODEGA" => "B",     // Bodega / Almacén
            "07" or "PREDIO" or "PATIO" => "P", // Predio / Patio
            _ => "M",
        };
        var est3 = Digits3(config?.CodigoEstablecimientoMh);
        var pv3  = Digits3(config?.CodigoPuntoVentaMh);
        return $"{letra}{est3}P{pv3}";
    }

    /// <summary>Extrae hasta 3 dígitos del valor (rellenando con ceros a la izquierda). Default "001".</summary>
    private static string Digits3(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits)) return "001";
        if (digits.Length > 3) digits = digits[^3..];
        return digits.PadLeft(3, '0');
    }

    private Task<int> NextCorrelativoAsync(int empresaId, string tipoDte, CancellationToken ct)
        => DteCorrelativoAllocator.NextAsync(_db, empresaId, tipoDte, ct);
}
