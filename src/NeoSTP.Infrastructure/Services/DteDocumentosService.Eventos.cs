using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Eventos;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    /// <summary>
    /// MOMENTO 2: transmite el Evento de Contingencia (contingencia-schema v4) informando los
    /// códigos de generación de los DTE generados en contingencia. Firma RS512 + POST /fesv/contingencia.
    /// </summary>
    public async Task<Result<CrearEventoResultadoDto>> TransmitirEventoContingenciaAsync(
        int empresaId, IReadOnlyList<int> documentoIds, int tipoContingencia, string? motivo,
        string nombreResponsable, string tipoDocResponsable, string numeroDocResponsable,
        string? actor, CancellationToken ct = default)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return Result<CrearEventoResultadoDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        var config = await _db.DteConfiguracion.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config?.CertificadoBlob is null || config.CertificadoBlob.Length == 0)
            return Result<CrearEventoResultadoDto>.Fail("Certificado no cargado.", "VALIDATION");

        var docs = await _db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId && documentoIds.Contains(d.Id))
            .ToListAsync(ct);
        if (docs.Count == 0) return Result<CrearEventoResultadoDto>.Fail("No hay documentos para el evento.", "VALIDATION");

        var ahora = DateTime.Now;
        var codGen = Guid.NewGuid().ToString().ToUpperInvariant();
        var fechaMin = docs.Min(d => d.FechaEmision);
        var codEst = string.IsNullOrWhiteSpace(config.CodigoEstablecimientoMh) ? null : config.CodigoEstablecimientoMh;
        var codPv  = string.IsNullOrWhiteSpace(config.CodigoPuntoVentaMh)      ? null : config.CodigoPuntoVentaMh;
        var ambiente = config.AmbienteCodigo == "PRODUCCION" ? "01" : "00";

        var evento = new
        {
            identificacion = new
            {
                version = 3,
                ambiente,
                codigoGeneracion = codGen,
                fTransmision = ahora.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                hTransmision = ahora.ToString(@"HH\:mm\:ss"),
            },
            emisor = new
            {
                nit = empresa.Nit,
                nombre = empresa.RazonSocial,
                nombreResponsable,
                tipoDocResponsable,
                numeroDocResponsable,
                tipoEstablecimiento = string.IsNullOrWhiteSpace(config.TipoEstablecimientoCodigo) ? "02" : config.TipoEstablecimientoCodigo,
                codEstableMH = codEst,    // evento contingencia: establecimiento con MH, punto de venta sin MH (asimétrico)
                codPuntoVenta = codPv,
                telefono = empresa.Telefono,
                correo = empresa.Correo,
            },
            detalleDTE = docs.Select((d, i) => new
            {
                noItem = i + 1,
                tipoDoc = d.TipoDteCodigo,
                codigoGeneracion = d.CodigoGeneracion,
            }).ToArray(),
            motivo = new
            {
                fInicio = fechaMin.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                fFin = ahora.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                hInicio = "00:00:00",
                hFin = ahora.ToString(@"HH\:mm\:ss"),
                tipoContingencia,
                motivoContingencia = tipoContingencia == 5 ? (motivo ?? "Falla de conexión") : motivo,
            },
        };

        var json = System.Text.Json.JsonSerializer.Serialize(evento,
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        var relacionados = docs.Select(d => (d.Id, DteEventoRolCodigos.LoteContingencia, (string?)d.NumeroControl)).ToList();
        var motivoLibre = tipoContingencia == 5 ? (motivo ?? "Falla de conexión") : motivo;

        var firma = await _signer.FirmarAsync(json, config.CertificadoBlob, null, ct);
        if (!firma.Success)
        {
            var idErr = await PersistirEventoAsync(empresaId, TipoEventoCodigos.Contingencia, codGen, 3, config.AmbienteCodigo,
                json, null, firmaOk: false, resp: null, motivoLibre, numeroControlRef: null, relacionados, actor, ct);
            return Result<CrearEventoResultadoDto>.Fail(firma.Detalle ?? "Error firmando evento.", "FIRMA_FAILED");
        }

        var tokenResult = await ObtenerTokenAsync(config, ct);
        if (!tokenResult.Success)
        {
            await PersistirEventoAsync(empresaId, TipoEventoCodigos.Contingencia, codGen, 3, config.AmbienteCodigo,
                json, firma.JsonFirmado, firmaOk: true, resp: null, motivoLibre, numeroControlRef: null, relacionados, actor, ct);
            return Result<CrearEventoResultadoDto>.Fail(tokenResult.Mensaje ?? "No se pudo obtener token.", "HACIENDA_AUTH_FAILED");
        }

        var resp = await _contingencia.EnviarAsync(new ContingenciaRequest
        {
            Nit = empresa.Nit!,
            Ambiente = ambiente,
            AmbienteCodigo = config.AmbienteCodigo,
            Documento = firma.JsonFirmado!,
            Token = tokenResult.Token!,
        }, ct);

        await Audit(empresaId, actor, "EVENTO_CONTINGENCIA", resp.Success ? "OK" : "FAIL",
            $"estado={resp.Estado} cod={resp.CodigoMsg} desc={resp.DescripcionMsg} obs={string.Join("; ", resp.Observaciones)}", 0);

        var captura = new EventoRespuestaCaptura(resp.Success, resp.Estado, resp.CodigoMsg, resp.DescripcionMsg,
            resp.SelloRecibido, resp.Raw ?? System.Text.Json.JsonSerializer.Serialize(new { resp.Estado, resp.CodigoMsg, resp.DescripcionMsg, resp.Observaciones }));
        var eventoId = await PersistirEventoAsync(empresaId, TipoEventoCodigos.Contingencia, codGen, 3, config.AmbienteCodigo,
            json, firma.JsonFirmado, firmaOk: true, captura, motivoLibre, numeroControlRef: null, relacionados, actor, ct);

        return resp.Success
            ? Result<CrearEventoResultadoDto>.Ok(new CrearEventoResultadoDto { SelloOEstado = resp.SelloRecibido ?? resp.Estado ?? "OK", EventoId = eventoId })
            : Result<CrearEventoResultadoDto>.Fail($"[{resp.CodigoMsg}] {resp.DescripcionMsg} {string.Join("; ", resp.Observaciones)}".Trim(), "CONTINGENCIA_RECHAZADA");
    }

    /// <summary>Firma RS512 un evento (JSON) y lo transmite al endpoint indicado vía el cliente genérico.</summary>
    private async Task<Result<CrearEventoResultadoDto>> FirmarYTransmitirEventoAsync(
        string json, string endpointPath, Func<string, object> bodyFactory,
        Domain.Core.Dte.DteConfiguracion config, int empresaId, string accion, string? actor,
        string tipoEvento, string codigoGeneracion, int version,
        IReadOnlyList<(int docId, string rol, string? nc)> relacionados,
        string? motivoLibre, string? numeroControlRef, CancellationToken ct)
    {
        var firma = await _signer.FirmarAsync(json, config.CertificadoBlob, null, ct);
        if (!firma.Success)
        {
            await PersistirEventoAsync(empresaId, tipoEvento, codigoGeneracion, version, config.AmbienteCodigo,
                json, null, firmaOk: false, resp: null, motivoLibre, numeroControlRef, relacionados, actor, ct);
            return Result<CrearEventoResultadoDto>.Fail(firma.Detalle ?? "Error firmando evento.", "FIRMA_FAILED");
        }

        var tok = await ObtenerTokenAsync(config, ct);
        if (!tok.Success)
        {
            await PersistirEventoAsync(empresaId, tipoEvento, codigoGeneracion, version, config.AmbienteCodigo,
                json, firma.JsonFirmado, firmaOk: true, resp: null, motivoLibre, numeroControlRef, relacionados, actor, ct);
            return Result<CrearEventoResultadoDto>.Fail(tok.Mensaje ?? "No se pudo obtener token.", "HACIENDA_AUTH_FAILED");
        }

        var body = System.Text.Json.JsonSerializer.Serialize(bodyFactory(firma.JsonFirmado!),
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var resp = await _evento.PostAsync(endpointPath, body, tok.Token!, config.AmbienteCodigo, ct);

        await Audit(empresaId, actor, accion, resp.Success ? "OK" : "FAIL",
            $"estado={resp.Estado} cod={resp.CodigoMsg} desc={resp.DescripcionMsg} obs={string.Join("; ", resp.Observaciones)} RAW={Truncate(resp.Raw ?? "", 600)}", 0);

        var captura = new EventoRespuestaCaptura(resp.Success, resp.Estado, resp.CodigoMsg, resp.DescripcionMsg,
            resp.SelloRecibido, resp.Raw ?? System.Text.Json.JsonSerializer.Serialize(new { resp.Estado, resp.CodigoMsg, resp.DescripcionMsg, resp.Observaciones }));
        var eventoId = await PersistirEventoAsync(empresaId, tipoEvento, codigoGeneracion, version, config.AmbienteCodigo,
            json, firma.JsonFirmado, firmaOk: true, captura, motivoLibre, numeroControlRef, relacionados, actor, ct);

        return resp.Success
            ? Result<CrearEventoResultadoDto>.Ok(new CrearEventoResultadoDto { SelloOEstado = resp.SelloRecibido ?? resp.Estado ?? "OK", EventoId = eventoId })
            : Result<CrearEventoResultadoDto>.Fail($"[{resp.CodigoMsg}] {resp.DescripcionMsg} {string.Join("; ", resp.Observaciones)}".Trim(), $"{accion}_RECHAZADA");
    }

    private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];

    /// <summary>Captura uniforme de la respuesta MH para persistir (clientes distintos, mismos campos).</summary>
    private sealed record EventoRespuestaCaptura(bool Success, string? Estado, string? CodigoMsg, string? DescripcionMsg, string? SelloRecibido, string? Raw);

    /// <summary>
    /// Sprint 15 — persiste un evento DTE en las tablas Dte_Eventos*. Best-effort:
    /// nunca lanza, para no romper un flujo de transmisión certificado por un error
    /// de persistencia. Devuelve el Id del evento creado o null si falló.
    /// </summary>
    private async Task<int?> PersistirEventoAsync(
        int empresaId, string tipoEvento, string codigoGeneracion, int version, string ambienteCodigo,
        string jsonSinFirmar, string? jws, bool firmaOk, EventoRespuestaCaptura? resp,
        string? motivoLibre, string? numeroControlRef,
        IReadOnlyList<(int docId, string rol, string? nc)> relacionados, string? actor, CancellationToken ct)
    {
        try
        {
            var estado = !firmaOk
                ? DteEventoEstadoCodigos.Error
                : resp is null
                    ? DteEventoEstadoCodigos.Firmado
                    : resp.Success ? DteEventoEstadoCodigos.Procesado : DteEventoEstadoCodigos.Rechazado;

            var ahora = DateTime.UtcNow;
            var finalizado = estado is DteEventoEstadoCodigos.Procesado or DteEventoEstadoCodigos.Rechazado;

            var evento = new DteEvento
            {
                EmpresaId = empresaId,
                TipoEventoCodigo = tipoEvento,
                CodigoGeneracion = codigoGeneracion,
                Version = version,
                AmbienteCodigo = ambienteCodigo,
                FechaTransmision = ahora,
                EstadoCodigo = estado,
                SelloRecibido = resp?.SelloRecibido,
                NumeroControlReferencia = numeroControlRef,
                MotivoLibre = motivoLibre,
                FinalizadoAt = finalizado ? ahora : null,
                CreatedAt = ahora,
                CreatedBy = actor,
                Json = new DteEventoJson
                {
                    JsonSinFirmar = jsonSinFirmar,
                    JwsFirmado = jws,
                    CreatedAt = ahora,
                    CreatedBy = actor,
                },
            };
            _db.DteEventos.Add(evento);
            await _db.SaveChangesAsync(ct);

            if (resp is not null)
            {
                _db.DteEventoRespuestas.Add(new DteEventoRespuestaHacienda
                {
                    EventoId = evento.Id,
                    RespuestaCrudaJson = resp.Raw ?? "{}",
                    Estado = resp.Estado,
                    CodigoMsg = resp.CodigoMsg,
                    DescripcionMsg = resp.DescripcionMsg,
                    SelloRecibido = resp.SelloRecibido,
                    RecibidoAt = ahora,
                    CreatedAt = ahora,
                    CreatedBy = actor,
                });
            }

            foreach (var (docId, rol, nc) in relacionados)
            {
                _db.DteEventoDocumentosRelacionados.Add(new DteEventoDocumentoRelacionado
                {
                    EventoId = evento.Id,
                    DocumentoId = docId,
                    RolCodigo = rol,
                    NumeroControlSnapshot = nc,
                    CreatedAt = ahora,
                    CreatedBy = actor,
                });
            }

            if (resp is not null || relacionados.Count > 0)
            {
                await _db.SaveChangesAsync(ct);
            }

            return evento.Id;
        }
        catch
        {
            // best-effort: la transmisión ya ocurrió; un fallo de persistencia no debe propagarse.
            return null;
        }
    }

    /// <summary>Evento de Invalidación (anulación) de un DTE ya PROCESADO. POST /fesv/anulardte.</summary>
    public async Task<Result<CrearEventoResultadoDto>> TransmitirInvalidacionEventoAsync(
        int empresaId, int documentoId, int tipoAnulacion, string? motivoAnulacion, string? codigoGeneracionReemplazo,
        string nombreResponsable, string tipoDocResponsable, string numDocResponsable, string? actor, CancellationToken ct = default)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return Result<CrearEventoResultadoDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        var config = await _db.DteConfiguracion.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config?.CertificadoBlob is null) return Result<CrearEventoResultadoDto>.Fail("Certificado no cargado.", "VALIDATION");

        var doc = await _db.DteDocumentos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentoId && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<CrearEventoResultadoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo != DteEstadoCodigos.Procesado || string.IsNullOrEmpty(doc.SelloRecibido))
            return Result<CrearEventoResultadoDto>.Fail("Solo se puede invalidar un DTE PROCESADO con sello de recepción.", "INVALID_STATE");
        if (tipoAnulacion is 1 or 3 && string.IsNullOrEmpty(codigoGeneracionReemplazo))
            return Result<CrearEventoResultadoDto>.Fail("Tipo de invalidación 1/3 requiere código de generación del documento de reemplazo.", "VALIDATION");

        var ahora = DateTime.Now;
        var codGen = Guid.NewGuid().ToString().ToUpperInvariant();
        var codEst = string.IsNullOrWhiteSpace(config.CodigoEstablecimientoMh) ? null : config.CodigoEstablecimientoMh;
        var codPv  = string.IsNullOrWhiteSpace(config.CodigoPuntoVentaMh)      ? null : config.CodigoPuntoVentaMh;
        var ambiente = config.AmbienteCodigo == "PRODUCCION" ? "01" : "00";

        var evento = new
        {
            identificacion = new
            {
                version = 2,
                ambiente,
                codigoGeneracion = codGen,
                fecAnula = ahora.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                horAnula = ahora.ToString(@"HH\:mm\:ss"),
            },
            emisor = new
            {
                nit = empresa.Nit,
                nombre = empresa.RazonSocial,
                tipoEstablecimiento = string.IsNullOrWhiteSpace(config.TipoEstablecimientoCodigo) ? "02" : config.TipoEstablecimientoCodigo,
                nomEstablecimiento = string.IsNullOrWhiteSpace(empresa.NombreComercial) ? "Casa Matriz" : empresa.NombreComercial,
                codEstableMH = codEst,
                codEstable = codEst,
                codPuntoVentaMH = codPv,
                codPuntoVenta = codPv,
                telefono = empresa.Telefono,
                correo = empresa.Correo,
            },
            documento = new
            {
                tipoDte = doc.TipoDteCodigo,
                codigoGeneracion = doc.CodigoGeneracion,
                selloRecibido = doc.SelloRecibido,
                numeroControl = doc.NumeroControl,
                fecEmi = doc.FechaEmision.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                montoIva = (double)doc.IvaTotal,
                codigoGeneracionR = tipoAnulacion is 1 or 3 ? codigoGeneracionReemplazo : null,
                tipoDocumento = doc.ReceptorTipoDocumento?.Trim().ToUpperInvariant() switch
                {
                    "NIT" => "36", "DUI" => "13", "PASAPORTE" => "03",
                    "CARNET_RESIDENTE" => "02", "OTRO" => "37", var x => x,
                },
                numDocumento = doc.ReceptorNumeroDocumento,
                nombre = doc.ReceptorNombre,
                telefono = doc.ReceptorTelefono,
                correo = doc.ReceptorCorreo,
            },
            motivo = new
            {
                tipoAnulacion,
                motivoAnulacion = tipoAnulacion == 2 ? (motivoAnulacion ?? "Rescindir de la operación realizada") : motivoAnulacion,
                nombreResponsable,
                tipDocResponsable = tipoDocResponsable,
                numDocResponsable,
                nombreSolicita = nombreResponsable,
                tipDocSolicita = tipoDocResponsable,
                numDocSolicita = numDocResponsable,
            },
        };

        var json = System.Text.Json.JsonSerializer.Serialize(evento,
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        return await FirmarYTransmitirEventoAsync(json, "/fesv/anulardte",
            jws => new { ambiente, idEnvio = doc.Id, version = 2, documento = jws },
            config, empresaId, "EVENTO_INVALIDACION", actor,
            TipoEventoCodigos.Invalidacion, codGen, 2,
            new[] { (doc.Id, DteEventoRolCodigos.Anulado, (string?)doc.NumeroControl) },
            motivoLibre: motivoAnulacion, numeroControlRef: doc.NumeroControl, ct);
    }

    /// <summary>Evento de Operaciones Especiales (EOE, tipoEvento 17). Esquema fe-eop (transmisión vía recepciondte).</summary>
    public async Task<Result<CrearEventoResultadoDto>> TransmitirEventoOperacionesEspecialesAsync(
        int empresaId, string? codigoGeneracionRef, string descripcion, decimal monto, string? actor, CancellationToken ct = default)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return Result<CrearEventoResultadoDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        var config = await _db.DteConfiguracion.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config?.CertificadoBlob is null) return Result<CrearEventoResultadoDto>.Fail("Certificado no cargado.", "VALIDATION");

        var ahora = DateTime.Now;
        var ambiente = config.AmbienteCodigo == "PRODUCCION" ? "01" : "00";
        var codGen = Guid.NewGuid().ToString().ToUpperInvariant();
        var m = (double)monto;

        var evento = new
        {
            identificacion = new
            {
                version = 1,
                ambiente,
                tipoModelo = 1,
                tipoOperacion = 1,
                tipoEvento = "17",
                codigoGeneracion = codGen,
                fecEmi = ahora.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                horEmi = ahora.ToString(@"HH\:mm\:ss"),
                tipoMoneda = "USD",
            },
            emisor = new { nit = empresa.Nit, nombre = empresa.RazonSocial },
            cuerpoDocumento = new[]
            {
                new
                {
                    numItem = 1,
                    codigoGeneracionRef = (string?)null,   // referencia a OTRO EOE (anulados); null en caso normal
                    tipoDocumento = "97",                  // CAT: 97 = Comprobante de Control Interno
                    numDocumento = string.IsNullOrWhiteSpace(codigoGeneracionRef) ? "DCI0001" : codigoGeneracionRef,  // Documento origen
                    fechaEmision = ahora.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    cantidad = 1,
                    descripcion,
                    docDel = (string?)null,
                    docAl = (string?)null,
                    precioUni = m,
                    ventaNoSuj = 0d,
                    ventaExenta = 0d,
                    ventaGravada = m,
                    tributos = new[] { "20" },
                },
            },
            resumen = new
            {
                totalNoSuj = 0d,
                totalExenta = 0d,
                totalGravada = m,
                subTotal = m,
                tributos = new[]
                {
                    new { codigo = "20", descripcion = "Impuesto al Valor Agregado 13%", valor = Math.Round(m * 0.13, 2) },
                },
                total = Math.Round(m + m * 0.13, 2),
                totalLetras = (string?)null,
            },
            apendice = (object?)null,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(evento,
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        return await FirmarYTransmitirEventoAsync(json, "/fesv/recepciondte",
            jws => new { ambiente, idEnvio = ahora.Millisecond + 1, version = 1, tipoDte = "17", documento = jws, codigoGeneracion = codGen },
            config, empresaId, "EVENTO_OPERACIONES_ESPECIALES", actor,
            TipoEventoCodigos.OperacionesEspeciales, codGen, 1,
            Array.Empty<(int, string, string?)>(),
            motivoLibre: descripcion, numeroControlRef: null, ct);
    }

    /// <summary>Evento de Retorno (ERET, tipoEvento 18). Esquema fe-eret v1, transmisión vía recepciondte. Aplica a FE/FEXE/FSEE.</summary>
    public async Task<Result<CrearEventoResultadoDto>> TransmitirEventoRetornoAsync(
        int empresaId, int documentoOrigenId, string? actor, CancellationToken ct = default)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return Result<CrearEventoResultadoDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        var config = await _db.DteConfiguracion.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config?.CertificadoBlob is null) return Result<CrearEventoResultadoDto>.Fail("Certificado no cargado.", "VALIDATION");

        var orig = await _db.DteDocumentos.Include(d => d.Detalles).AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentoOrigenId && d.EmpresaId == empresaId, ct);
        if (orig is null) return Result<CrearEventoResultadoDto>.Fail("Documento origen no encontrado.", "DTE_NOT_FOUND");
        if (orig.EstadoCodigo != DteEstadoCodigos.Procesado)
            return Result<CrearEventoResultadoDto>.Fail("El documento origen del retorno debe estar PROCESADO.", "INVALID_STATE");

        var ahora = DateTime.Now;
        var ambiente = config.AmbienteCodigo == "PRODUCCION" ? "01" : "00";
        var codGen = Guid.NewGuid().ToString().ToUpperInvariant();
        var codEst = string.IsNullOrWhiteSpace(config.CodigoEstablecimientoMh) ? null : config.CodigoEstablecimientoMh;
        var codPv  = string.IsNullOrWhiteSpace(config.CodigoPuntoVentaMh)      ? null : config.CodigoPuntoVentaMh;
        var gravada = (double)orig.TotalGravada;
        var iva = Math.Round(gravada * 0.13 / 1.13, 2);

        var evento = new
        {
            identificacion = new
            {
                version = 1,
                ambiente,
                tipoModelo = 1,
                tipoOperacion = 1,
                tipoEvento = "18",
                tipoContingencia = (int?)null,
                motivoContin = (string?)null,
                codigoGeneracion = codGen,
                fecEmi = ahora.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                horEmi = ahora.ToString(@"HH\:mm\:ss"),
                fusion = (string?)null,
                tipoMoneda = "USD",
            },
            documentoRelacionado = new[]
            {
                new { tipoDocumento = orig.TipoDteCodigo, codigoGeneracion = orig.CodigoGeneracion,
                      fechaEmision = orig.FechaEmision.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
            },
            emisor = new
            {
                nit = empresa.Nit,
                nombre = empresa.RazonSocial,
                codEstableMH = codEst,   // requiere el código MH real registrado del establecimiento
                codEstable = codEst,
                codPuntoVentaMH = codPv,
                codPuntoVenta = codPv,
                recintoFiscal = (string?)null,
                tipoRegimen = (string?)null,
                regimen = (string?)null,
                tipoItemExpor = (int?)null,
            },
            documento = (object?)null,
            ventaTercero = (object?)null,
            compraTercero = (object?)null,
            cuerpoDocumento = new[]
            {
                new
                {
                    numItem = 1,
                    tipoItem = 1,
                    codigoGeneracion = orig.CodigoGeneracion,
                    cantidad = 1d,
                    precioUni = gravada,
                    descripcion = "Retorno de operación",
                    codigo = (string?)null,
                    uniMedida = 59,
                    montoDescu = 0d,
                    codTributo = (string?)null,
                    ventaNoSuj = 0d,
                    ventaExenta = 0d,
                    ventaGravada = gravada,
                    compra = 0d,
                    tributos = (object?)null,
                    psv = 0d,
                    ivaItem = iva,
                    noGravado = 0d,
                    seguro = 0d,
                    flete = 0d,
                    ivaRete = 0d,
                    reteRenta = 0d,
                },
            },
            resumen = new
            {
                totalNoSuj = 0d,
                totalExenta = 0d,
                totalGravada = gravada,
                totalCompraExcluidos = 0d,
                subTotalVentas = gravada,
                tributos = (object?)null,
                totalSeguro = 0d,
                totalFlete = 0d,
                montoTotalOperacion = gravada,
                ivaRete = 0d,
                reteRenta = (double?)0d,
                totalNoGravado = 0d,
                totalPagar = gravada,
                totalLetras = (string?)null,
                totalNoOnerosas = 0d,
                totalIva = iva,
                saldoFavor = 0d,
            },
            apendice = (object?)null,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(evento,
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        return await FirmarYTransmitirEventoAsync(json, "/fesv/recepciondte",
            jws => new { ambiente, idEnvio = ahora.Millisecond + 1, version = 1, tipoDte = "18", documento = jws, codigoGeneracion = codGen },
            config, empresaId, "EVENTO_RETORNO", actor,
            TipoEventoCodigos.Retorno, codGen, 1,
            new[] { (orig.Id, DteEventoRolCodigos.Origen, (string?)orig.NumeroControl) },
            motivoLibre: null, numeroControlRef: orig.NumeroControl, ct);
    }
}
