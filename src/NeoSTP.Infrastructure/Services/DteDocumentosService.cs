using System.Data;
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

    // Corte de esquemas MH 2026-08-25: cuando Dte:EsquemaNuevo=true los eventos usan las
    // versiones nuevas (invalidación v3). Contingencia ya migró a v4 sin toggle porque apitest
    // la exige desde ya. Default false = versiones que apitest aún acepta hoy.
    private readonly bool _esquemaNuevo;

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
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
    {
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
    private async Task ResolverReceptorTerritorialMhAsync(DteDocumento doc, int empresaId, CancellationToken ct)
    {
        if (_lookup is null) return; // sin lookup (p. ej. tests antiguos) se conserva el comportamiento previo

        doc.ReceptorDepartamentoCodigo = await MapCodigoMhAsync(CatalogCodes.DepartamentoEs, doc.ReceptorDepartamentoCodigo, empresaId, ct);
        doc.ReceptorMunicipioCodigo    = await MapCodigoMhAsync(CatalogCodes.MunicipioEs,    doc.ReceptorMunicipioCodigo,    empresaId, ct);
        doc.ReceptorDistritoCodigo     = await MapCodigoMhAsync(CatalogCodes.DistritoEs,     doc.ReceptorDistritoCodigo,     empresaId, ct);
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
    private async Task SanearEmisorParaMhAsync(Empresa e, DteConfiguracion? config, int empresaId, CancellationToken ct)
    {
        e.Nit = ClienteValidator.StripToDigits(e.Nit) ?? e.Nit;
        e.Nrc = ClienteValidator.StripToDigits(e.Nrc);
        e.Departamento = await MapCodigoMhAsync(CatalogCodes.DepartamentoEs, e.Departamento, empresaId, ct);
        e.Municipio    = await MapCodigoMhAsync(CatalogCodes.MunicipioEs,    e.Municipio,    empresaId, ct);
        e.Distrito     = await MapCodigoMhAsync(CatalogCodes.DistritoEs,     e.Distrito,     empresaId, ct);
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
            errors.Add("La empresa no tiene NIT registrado.");
        if (string.IsNullOrWhiteSpace(e.Correo))
            errors.Add("La empresa no tiene correo registrado. Configúralo en Empresa → Editar antes de emitir.");
        if (string.IsNullOrWhiteSpace(e.Telefono))
            errors.Add("La empresa no tiene teléfono registrado. Configúralo en Empresa → Editar antes de emitir.");
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
        var ambiente = config?.AmbienteCodigo ?? "PRUEBAS";
        // numeroControl: DTE-XX-{bloqueEstab}-{15 digitos}.
        // Formato oficial MH (esquemas svfe): el bloque de 8 chars es
        //   (M|B|S|P)([0-9]{3})(P)([0-9]{3})  →  letraTipoEstablecimiento + codEstable(3) + 'P' + codPuntoVenta(3)
        // Ej: DTE-01-M001P001-000000000000001  (NO es codEstable(4)+codPuntoVenta(4) como se creía).
        var bloqueEstab = BuildBloqueEstablecimiento(config);

        var doc = new DteDocumento
        {
            EmpresaId = empresaId,
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
        await ResolverReceptorTerritorialMhAsync(doc, empresaId, ct);

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
        var correlativoNum = await NextCorrelativoAsync(empresaId, request.TipoDteCodigo, ct);
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
        if (limiteTransaction is not null) await limiteTransaction.CommitAsync(ct);
        await Audit(empresaId, actor, "CREATE_BORRADOR", "OK",
            $"DTE {doc.TipoDteCodigo} #{doc.NumeroControl} en borrador (total={doc.TotalPagar:0.00})", doc.Id);

            return await GetByIdAsync(empresaId, doc.Id, ct);
        });
    }

    private async Task<IDbContextTransaction?> BeginDteLimitTransactionAsync(int empresaId, CancellationToken ct)
    {
        if (_licenciaGuard is null || !_db.Database.IsRelational()) return null;

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

    public async Task<Result<DteDocumentoDto>> GenerarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos
            .Include(d => d.Detalles)
            .Include(d => d.Json)
            .Include(d => d.Empresa)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado or DteEstadoCodigos.Enviado)
            return Result<DteDocumentoDto>.Fail("El documento ya fue enviado o procesado.", "INVALID_STATE");

        // Re-snapshot del cálculo por si cambiaron líneas
        _calculator.Recalcular(doc);

        // Cargar config DTE para inyectar codEstable/codPuntoVenta/tipoEstablecimiento en el bloque emisor.
        var configForJson = await _db.DteConfiguracion.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        // Validar datos MH-obligatorios del emisor que no podemos inventar (correo/teléfono/NIT).
        var emisorErrors = ValidarEmisorParaMh(doc.Empresa);
        if (emisorErrors.Count > 0)
            return Result<DteDocumentoDto>.Fail(
                "La empresa emisora tiene datos incompletos para emitir a Hacienda.",
                "VALIDATION", emisorErrors);

        // Sanear emisor DEFENSIVAMENTE: quitar guiones al NIT/NRC, resolver
        // departamento/municipio/distrito/tipoEstablecimiento a códigos MH. La Empresa se detach
        // para no persistir estos cambios y respetar el dato original del usuario en BD.
        if (doc.Empresa is not null)
        {
            _db.Entry(doc.Empresa).State = EntityState.Detached;
            await SanearEmisorParaMhAsync(doc.Empresa, configForJson, empresaId, ct);
        }
        // Sanear receptor DEFENSIVAMENTE: quitar guiones del NIT/NRC persistidos con
        // NormalizeNit (formato con guiones para presentación).
        SanearReceptorParaMh(doc);

        var json = _generator.Generar(doc, configForJson);
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
            doc.Json.JsonFirmado = null;
            doc.Json.FirmadoAt = null;
            doc.Json.RespuestaHacienda = null;
            doc.Json.RespuestaAt = null;
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

        if (doc.EstadoCodigo is not DteEstadoCodigos.Enviado)
        {
            var gen = await GenerarAsync(empresaId, id, actor, ct);
            if (gen.IsFailure) return gen;
            doc = await _db.DteDocumentos
                .Include(d => d.Json)
                .Include(d => d.Empresa)
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

        var tokenResult = await ObtenerTokenAsync(config, ct);
        if (!tokenResult.Success)
            return Result<DteDocumentoDto>.Fail(tokenResult.Mensaje ?? "No se pudo obtener token Hacienda.", "HACIENDA_AUTH_FAILED");

        var resp = await _reception.EnviarAsync(new HaciendaReceptionRequest
        {
            Nit = doc.Empresa?.Nit,                          // NIT del emisor para lookup de cert
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
        _metrics?.DteEmitido(empresaId, doc.TipoDteCodigo, nuevoEstado);
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

        return await GetByIdAsync(empresaId, doc.Id, ct);
    }

    private async Task<(bool Success, string? Token, string? Mensaje)> ObtenerTokenAsync(
        Domain.Core.Dte.DteConfiguracion config, CancellationToken ct)
    {
        // Token cacheado vigente: 5 minutos de margen antes de expirar.
        // Defensivo: quitar prefijo "Bearer " si quedó almacenado con él (bug previo en el cliente MH).
        if (!string.IsNullOrEmpty(config.TokenMhCifrado)
            && config.TokenMhExpiraAt.HasValue
            && config.TokenMhExpiraAt.Value > DateTime.UtcNow.AddMinutes(5))
        {
            try
            {
                var raw = _protector.Unprotect(config.TokenMhCifrado);
                var clean = raw?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                    ? raw[7..].Trim()
                    : raw;
                return (true, clean, null);
            }
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
        var tieneLogo = doc.Empresa?.LogoBlob is { Length: > 0 };
        var body = BuildBody(doc, emisor, tieneLogo);

        var message = new EmailMessage
        {
            To = to,
            Subject = subject,
            HtmlBody = body,
        };
        message.Attachments.AddRange(attachments);
        if (tieneLogo)
            message.InlineImages.Add(new EmailInlineImage
            {
                ContentId = "logo",
                MediaType = doc.Empresa!.LogoContentType ?? "image/png",
                Content = doc.Empresa!.LogoBlob!,
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
        var doc = await _db.DteDocumentos.FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo is DteEstadoCodigos.Procesado)
            return Result.Fail("No se puede invalidar un DTE ya procesado en Hacienda. Use anulación.", "INVALID_STATE");

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
