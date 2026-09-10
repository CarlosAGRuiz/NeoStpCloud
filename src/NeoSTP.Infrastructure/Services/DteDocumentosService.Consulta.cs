using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Diagnostico;
using NeoSTP.Infrastructure.Dte;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    public Task<Result<DteDocumentoDto>> ConciliarHaciendaAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => EjecutarCambioFiscalAsync(empresaId, id, () => ConciliarHaciendaCoreAsync(empresaId, id, actor, ct), ct);

    private async Task<Result<DteDocumentoDto>> ConciliarHaciendaCoreAsync(int empresaId, int id, string? actor, CancellationToken ct)
    {
        var doc = await _db.DteDocumentos.Include(d => d.Json)
            .FirstOrDefaultAsync(d => d.Id == id && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<DteDocumentoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (doc.EstadoCodigo == DteEstadoCodigos.Procesado && !string.IsNullOrWhiteSpace(doc.SelloRecibido))
            return await GetByIdAsync(empresaId, id, ct);
        if (doc.EstadoCodigo == DteEstadoCodigos.Invalidado)
            return Result<DteDocumentoDto>.Fail("Un documento invalidado no puede conciliarse como procesado.", "INVALID_STATE");
        if (!RequiereConciliacion(doc) || doc.EnviadoAt is null || doc.Json is null
            || string.IsNullOrWhiteSpace(doc.Json.JsonFirmado))
            return Result<DteDocumentoDto>.Fail("No existe un intento transmitido verificable para consultar en Hacienda.", "DTE_CONSULTA_NO_DISPONIBLE");
        if (_consultaDte is null)
            return Result<DteDocumentoDto>.Fail("El cliente de consulta de Hacienda no está configurado.", "DTE_CONSULTA_NO_DISPONIBLE");

        var identidad = LeerIdentidadFirmada(doc);
        var nitEmpresa = NormalizarNit(await _db.Empresas.AsNoTracking().Where(e => e.Id == empresaId)
            .Select(e => e.Nit).FirstOrDefaultAsync(ct));
        if (identidad is null || nitEmpresa is null || identidad.Nit != nitEmpresa)
            return Result<DteDocumentoDto>.Fail("El JSON firmado no coincide con la identidad fiscal persistida. No se consultó Hacienda.", "DTE_CONSULTA_INCOMPATIBLE");
        var config = await _db.DteConfiguracion.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config is null) return Result<DteDocumentoDto>.Fail("Configuración DTE no encontrada.", "CONFIG_NOT_FOUND");
        var contexto = DteFiscalContext.Validar(doc.AmbienteCodigo, config);
        if (contexto.IsFailure) return Result<DteDocumentoDto>.Fail(contexto.Error!, contexto.ErrorCode);
        var token = await ObtenerTokenAsync(config, ct);
        if (!token.Success)
            return Result<DteDocumentoDto>.Fail(token.Mensaje ?? "No se pudo obtener token Hacienda.", "HACIENDA_AUTH_FAILED");

        var respuesta = await _consultaDte.ConsultarAsync(new HaciendaConsultaDteRequest
        {
            Ambiente = identidad.Ambiente,
            AmbienteCodigo = doc.AmbienteCodigo,
            NitEmisor = identidad.Nit,
            TipoDte = identidad.TipoDte,
            CodigoGeneracion = identidad.CodigoGeneracion,
            Token = token.Token!,
        }, ct);

        var confirmada = respuesta.Success
            && respuesta.CodigoHttp is >= 200 and < 300
            && string.Equals(respuesta.Ambiente?.Trim(), identidad.Ambiente, StringComparison.Ordinal)
            && Guid.TryParse(respuesta.CodigoGeneracion?.Trim(), out var remoto)
            && remoto == Guid.Parse(identidad.CodigoGeneracion)
            && string.Equals(respuesta.CodigoMsg?.Trim(), "001", StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(respuesta.Estado)
                || string.Equals(respuesta.Estado.Trim(), "PROCESADO", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(respuesta.SelloRecibido);

        RegistrarConsulta(doc, respuesta, confirmada, actor);
        if (!confirmada)
        {
            await _db.SaveChangesAsync(ct);
            await AuditarConsultaBestEffortAsync(empresaId, actor, "FAIL",
                $"DTE sin confirmación inequívoca. HTTP={respuesta.CodigoHttp}; código={respuesta.CodigoMsg}; estado={respuesta.Estado}.", doc.Id);
            var actual = await GetByIdAsync(empresaId, id, ct);
            if (actual.IsFailure) return actual;
            return Result<DteDocumentoDto>.FailWithValue(actual.Value!,
                respuesta.DescripcionMsg ?? "Hacienda no confirmó inequívocamente este DTE; se conservó el estado anterior.",
                "DTE_RESULTADO_INCIERTO");
        }

        doc.EstadoCodigo = DteEstadoCodigos.Procesado;
        doc.SelloRecibido = respuesta.SelloRecibido!.Trim();
        doc.ProcesadoAt = respuesta.FhProcesamiento ?? DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        _metrics?.DteEmitido(empresaId, doc.TipoDteCodigo, DteEstadoCodigos.Procesado);
        await EnviarCorreoAutomaticoAsync(empresaId, id, actor);
        await AuditarConsultaBestEffortAsync(empresaId, actor, "OK",
            $"DTE confirmado como PROCESADO por consulta. HTTP={respuesta.CodigoHttp}; código={respuesta.CodigoMsg}.", doc.Id);
        await _webhookDispatcher.DispatchAsync(new ConnectDteEventoPayload
        {
            Evento = ConnectEventos.DteProcesado,
            EmpresaId = empresaId,
            DteId = doc.Id,
            CodigoGeneracion = doc.CodigoGeneracion,
            TipoDte = doc.TipoDteCodigo,
            Estado = DteEstadoCodigos.Procesado,
            OcurrioAt = DateTime.UtcNow,
        }, ct);
        return await GetByIdAsync(empresaId, id, ct);
    }

    private static ConsultaIdentidad? LeerIdentidadFirmada(DteDocumento doc)
    {
        try
        {
            if (!DteFiscalContext.CoincideJws(doc.Json?.JsonFirmado, doc.AmbienteCodigo, doc)) return null;
            var parts = doc.Json!.JsonFirmado!.Split('.');
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = json.RootElement;
            var identificacion = root.GetProperty("identificacion");
            var emisor = root.GetProperty("emisor");
            var ambiente = identificacion.GetProperty("ambiente").GetString()?.Trim();
            var tipo = identificacion.GetProperty("tipoDte").GetString()?.Trim();
            var codigo = identificacion.GetProperty("codigoGeneracion").GetString()?.Trim();
            var nitOriginal = emisor.GetProperty("nit").GetString()?.Trim();
            if (nitOriginal is null || nitOriginal.Any(c => !char.IsAsciiDigit(c) && c != '-')) return null;
            var nit = nitOriginal.Where(char.IsAsciiDigit).ToArray();
            var nitTexto = nit is null ? null : new string(nit);
            if (ambiente != DteAmbientes.CodigoMh(doc.AmbienteCodigo)
                || tipo != doc.TipoDteCodigo || !Guid.TryParse(codigo, out var uuid)
                || uuid != Guid.Parse(doc.CodigoGeneracion) || nitTexto?.Length != 14)
                return null;
            return new(ambiente, nitTexto, tipo, uuid.ToString("D").ToUpperInvariant());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or IndexOutOfRangeException) { return null; }
    }

    private void RegistrarConsulta(DteDocumento doc, HaciendaConsultaDteResult respuesta, bool confirmada, string? actor)
    {
        var mensaje = respuesta.DescripcionMsg ?? (confirmada ? "DTE confirmado por consulta." : "Consulta sin confirmación fiscal.");
        _db.DteErrorOcurrencias.Add(new DteErrorOcurrencia
        {
            EmpresaId = doc.EmpresaId,
            DteDocumentoId = doc.Id,
            CodigoError = confirmada ? "CONSULTA_CONFIRMADA" : Cortar(respuesta.CodigoMsg ?? "CONSULTA_NO_CONFIRMADA", 50),
            Mensaje = Cortar(mensaje, 1000),
            RespuestaMhJson = respuesta.Raw,
            JsonEnviado = doc.Json?.JsonFirmado,
            Fuente = DteErrorFuente.Transmision,
            OcurrioAt = DateTime.UtcNow,
            Resuelta = confirmada,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        });
    }

    private static string Cortar(string value, int max) => value.Length <= max ? value : value[..max];
    private async Task AuditarConsultaBestEffortAsync(int empresaId, string? actor, string resultado, string detalle, int id)
    {
        try { await Audit(empresaId, actor, "CONSULTAR_HACIENDA", resultado, detalle, id); }
        catch (Exception ex)
        {
            // La ocurrencia de consulta ya quedó persistida. Una caída del subsistema
            // secundario de auditoría no debe ocultar el resultado devuelto al operador.
            _logger?.LogWarning(ex, "No se pudo duplicar en auditoría la consulta del DTE {DteId}.", id);
        }
    }
    private static string? NormalizarNit(string? value)
    {
        var raw = value?.Trim();
        if (string.IsNullOrEmpty(raw) || raw.Any(c => !char.IsAsciiDigit(c) && c != '-')) return null;
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 14 ? digits : null;
    }
    private sealed record ConsultaIdentidad(string Ambiente, string Nit, string TipoDte, string CodigoGeneracion);
}
