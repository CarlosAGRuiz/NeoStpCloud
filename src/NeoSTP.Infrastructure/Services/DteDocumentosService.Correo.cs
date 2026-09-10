using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    internal static string? CopiaCorreoEmisor(string destinatarios, string? correoEmpresa)
    {
        if (string.IsNullOrWhiteSpace(correoEmpresa) || !MailAddress.TryCreate(correoEmpresa.Trim(), out var emisor)) return null;
        // Never copy to a platform/support/default SMTP address or another empresa.
        foreach (var to in destinatarios.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (MailAddress.TryCreate(to, out var receptor) && string.Equals(receptor.Address, emisor.Address, StringComparison.OrdinalIgnoreCase)) return null;
        return emisor.Address;
    }

    // Called only by the winning transition to PROCESADO, after fiscal persistence.
    // Repeated Enviar/Conciliar of an already processed document must not call this.
    // SMTP has no exactly-once guarantee: an uncertain/failed attempt is audited for
    // explicit manual resend, never automatically retransmitted or treated as DTE failure.
    internal async Task EnviarCorreoAutomaticoAsync(int empresaId, int id, string? actor)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            if (!await _db.DteDocumentos.AsNoTracking().AnyAsync(d => d.EmpresaId == empresaId && d.Id == id
                && d.EstadoCodigo == DteEstadoCodigos.Procesado && d.SelloRecibido != null && d.SelloRecibido != "", timeout.Token)) return;
            var result = await ReenviarPorCorreoAsync(empresaId, id, null, actor, timeout.Token);
            var enviado = result.IsSuccess && result.Value?.Enviado == true;
            await Audit(empresaId, actor, "CORREO_AUTOMATICO", enviado ? "OK" : "FAIL",
                enviado ? "Correo de DTE procesado aceptado por el proveedor. Copia al correo válido de la empresa emisora cuando es distinto del receptor."
                    : "No se confirmó el envío del correo automático. El DTE continúa PROCESADO. Revise el correo del receptor y SMTP; use Reenviar para reintentar.", id);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Correo automático no confirmado para empresa {EmpresaId}, DTE {DteId}: {FailureType}. El estado fiscal se conserva.", empresaId, id, ex.GetType().Name);
            try { await Audit(empresaId, actor, "CORREO_AUTOMATICO", "FAIL", "Envío de correo fallido o incierto; verificar entrega antes de reenviar. DTE procesado conservado.", id); }
            catch (Exception) { /* Secondary notification/audit errors must not hide fiscal acceptance. */ }
        }
    }
}
