using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Recordatorios salientes de cobranza. Reusa el saldo derivado de CobranzaService, envia por
/// email/WhatsApp y registra log diario por documento/canal para no duplicar.
/// </summary>
public class RecordatorioCobroService : IRecordatorioCobroService
{
    private const string AuditModule = "COBRANZA";

    private readonly NeoStpDbContext _db;
    private readonly ICobranzaService _cobranza;
    private readonly ITenantEmailSender _email;
    private readonly IWhatsAppSender _whatsApp;
    private readonly IAuditoriaService _auditoria;

    public RecordatorioCobroService(
        NeoStpDbContext db,
        ICobranzaService cobranza,
        ITenantEmailSender email,
        IWhatsAppSender whatsApp,
        IAuditoriaService auditoria)
    {
        _db = db;
        _cobranza = cobranza;
        _email = email;
        _whatsApp = whatsApp;
        _auditoria = auditoria;
    }

    public async Task<Result<RecordatorioCobroResumenDto>> EjecutarAsync(
        int empresaId,
        EjecutarRecordatoriosCobroRequest request,
        string? actor,
        CancellationToken ct = default)
    {
        var max = Math.Clamp(request.Maximo <= 0 ? 50 : request.Maximo, 1, 500);
        var diasMin = Math.Max(0, request.DiasVencidoMinimo);
        if (!request.EnviarEmail && !request.EnviarWhatsApp)
            return Result<RecordatorioCobroResumenDto>.Fail("Debe habilitar al menos un canal.", "VALIDATION");

        var pendientes = await _cobranza.GetPendientesAsync(empresaId, new CobranzaQuery
        {
            SoloVencidas = true,
            Page = 1,
            PageSize = max,
        }, ct);
        if (!pendientes.IsSuccess)
            return Result<RecordatorioCobroResumenDto>.Fail(pendientes.Error ?? "No se pudo consultar cobranza.", pendientes.ErrorCode);

        var facturas = pendientes.Value!.Items
            .Where(x => x.DiasVencido >= diasMin)
            .Take(max)
            .ToList();

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var resumen = new RecordatorioCobroResumenDto { Evaluadas = facturas.Count };

        foreach (var f in facturas)
        {
            var contacto = await CargarContactoAsync(empresaId, f.DteDocumentoId, ct);
            if (request.EnviarEmail)
                await ProcesarCanalAsync(empresaId, f, contacto.Email, RecordatorioCanales.Email, hoy, request.Forzar, actor, resumen, ct);
            if (request.EnviarWhatsApp)
                await ProcesarCanalAsync(empresaId, f, contacto.Telefono, RecordatorioCanales.WhatsApp, hoy, request.Forzar, actor, resumen, ct);
        }

        await _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = "RECORDATORIOS_COBRO",
            Entidad = "RecordatorioCobro",
            Resultado = "OK",
            Detalle = $"Evaluadas={resumen.Evaluadas}; Email={resumen.EnviadosEmail}; WhatsApp={resumen.EnviadosWhatsApp}; Omitidos={resumen.Omitidos}; Fallidos={resumen.Fallidos}",
        });

        return Result<RecordatorioCobroResumenDto>.Ok(resumen);
    }

    private async Task ProcesarCanalAsync(
        int empresaId,
        CobroPendienteDto factura,
        string? destinatario,
        string canal,
        DateOnly fecha,
        bool forzar,
        string? actor,
        RecordatorioCobroResumenDto resumen,
        CancellationToken ct)
    {
        if (!forzar)
        {
            var yaExiste = await _db.RecordatoriosCobro.AsNoTracking()
                .AnyAsync(r => r.EmpresaId == empresaId
                            && r.DteDocumentoId == factura.DteDocumentoId
                            && r.Canal == canal
                            && r.FechaRecordatorio == fecha
                            && r.EstadoCodigo == RecordatorioEstados.Enviado, ct);
            if (yaExiste)
            {
                resumen.Omitidos++;
                resumen.Detalles.Add(Detalle(factura, canal, RecordatorioEstados.Omitido, destinatario, "Ya enviado hoy."));
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(destinatario))
        {
            await RegistrarAsync(empresaId, factura, canal, string.Empty, RecordatorioEstados.Omitido, "Sin destinatario.", null, actor, fecha, ct);
            resumen.Omitidos++;
            resumen.Detalles.Add(Detalle(factura, canal, RecordatorioEstados.Omitido, null, "Sin destinatario."));
            return;
        }

        var body = BuildTexto(factura);
        string estado;
        string? motivo;
        string? messageId;

        if (canal == RecordatorioCanales.Email)
        {
            var result = await _email.EnviarAsync(empresaId, new EmailMessage
            {
                To = destinatario.Trim(),
                Subject = $"Recordatorio de pago {factura.NumeroControl}",
                HtmlBody = BuildHtml(factura),
                TextBody = body,
            }, ct);
            estado = result.Success ? RecordatorioEstados.Enviado : RecordatorioEstados.Fallido;
            motivo = result.Success ? null : $"{result.Mensaje}: {result.Detalle}";
            messageId = result.MessageId;
        }
        else
        {
            var result = await _whatsApp.EnviarAsync(new WhatsAppMessage
            {
                To = destinatario.Trim(),
                Body = body,
                Data =
                {
                    ["empresaId"] = empresaId.ToString(CultureInfo.InvariantCulture),
                    ["dteDocumentoId"] = factura.DteDocumentoId.ToString(CultureInfo.InvariantCulture),
                    ["numeroControl"] = factura.NumeroControl,
                },
            }, ct);
            estado = result.Success ? RecordatorioEstados.Enviado : RecordatorioEstados.Fallido;
            motivo = result.Error;
            messageId = result.MessageId;
        }

        await RegistrarAsync(empresaId, factura, canal, destinatario.Trim(), estado, motivo, messageId, actor, fecha, ct);
        if (estado == RecordatorioEstados.Enviado)
        {
            if (canal == RecordatorioCanales.Email) resumen.EnviadosEmail++;
            else resumen.EnviadosWhatsApp++;
        }
        else
        {
            resumen.Fallidos++;
        }
        resumen.Detalles.Add(Detalle(factura, canal, estado, destinatario, motivo));
    }

    private async Task RegistrarAsync(
        int empresaId,
        CobroPendienteDto factura,
        string canal,
        string destinatario,
        string estado,
        string? motivo,
        string? messageId,
        string? actor,
        DateOnly fecha,
        CancellationToken ct)
    {
        _db.RecordatoriosCobro.Add(new RecordatorioCobro
        {
            EmpresaId = empresaId,
            DteDocumentoId = factura.DteDocumentoId,
            ClienteId = factura.ClienteId,
            FechaRecordatorio = fecha,
            Canal = canal,
            Destinatario = destinatario,
            EstadoCodigo = estado,
            Motivo = motivo,
            MessageId = messageId,
            Saldo = factura.Saldo,
            DiasVencido = factura.DiasVencido,
            CreatedBy = actor,
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<(string? Email, string? Telefono)> CargarContactoAsync(int empresaId, int dteId, CancellationToken ct)
    {
        var contacto = await _db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId && d.Id == dteId)
            .Select(d => new
            {
                d.ReceptorCorreo,
                d.ReceptorTelefono,
                ClienteCorreo = d.Cliente != null ? d.Cliente.Correo : null,
                ClienteTelefono = d.Cliente != null ? d.Cliente.Telefono : null,
            })
            .FirstOrDefaultAsync(ct);

        if (contacto is null)
            return (null, null);

        var email = !string.IsNullOrWhiteSpace(contacto.ReceptorCorreo) ? contacto.ReceptorCorreo : contacto.ClienteCorreo;
        var telefono = !string.IsNullOrWhiteSpace(contacto.ReceptorTelefono) ? contacto.ReceptorTelefono : contacto.ClienteTelefono;
        return (email, telefono);
    }

    private static RecordatorioCobroDetalleDto Detalle(CobroPendienteDto f, string canal, string estado, string? destinatario, string? motivo) => new()
    {
        DteDocumentoId = f.DteDocumentoId,
        NumeroControl = f.NumeroControl,
        Canal = canal,
        EstadoCodigo = estado,
        Destinatario = destinatario,
        Motivo = motivo,
    };

    private static string BuildTexto(CobroPendienteDto f)
        => $"Estimado cliente, le recordamos que el documento {f.NumeroControl} tiene saldo pendiente de $ {f.Saldo:N2} y vencio hace {f.DiasVencido} dia(s).";

    private static string BuildHtml(CobroPendienteDto f)
        => $"""
        <p>Estimado cliente,</p>
        <p>Le recordamos que el documento <strong>{f.NumeroControl}</strong> tiene saldo pendiente.</p>
        <ul>
          <li>Cliente: {System.Net.WebUtility.HtmlEncode(f.ClienteNombre)}</li>
          <li>Vencimiento: {f.Vencimiento:dd/MM/yyyy}</li>
          <li>Dias vencido: {f.DiasVencido}</li>
          <li>Saldo: <strong>$ {f.Saldo:N2}</strong></li>
        </ul>
        <p>Si ya realizo el pago, puede ignorar este mensaje o enviar el comprobante para conciliacion.</p>
        """;
}
