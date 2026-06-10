using System.ComponentModel.DataAnnotations;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Dtos;

namespace NeoSTP.Application.Portal;

/// <summary>
/// NEOPORTAL (107) — autoservicio del receptor. Gestión interna de enlaces públicos
/// (firmados por hash, expirables y revocables) y resolución pública por token:
/// documento (PDF/JSON), estado de cuenta, reenvío por correo y QR de pago.
/// El token nunca permite cruzar empresa ni ver documentos de otros clientes.
/// </summary>
public interface IPortalService
{
    // ── Gestión interna (requiere permisos) ──
    Task<Result<PortalEnlaceDto>> GenerarEnlaceDocumentoAsync(int empresaId, int dteDocumentoId, GenerarEnlacePortalRequest request, string? actor, CancellationToken ct = default);
    Task<Result<PortalEnlaceDto>> GenerarEnlaceEstadoCuentaAsync(int empresaId, int clienteId, GenerarEnlacePortalRequest request, string? actor, CancellationToken ct = default);
    Task<Result<PagedResult<PortalEnlaceDto>>> ListEnlacesAsync(int empresaId, PagedQuery query, CancellationToken ct = default);
    Task<Result> RevocarAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    // ── Resolución pública por token (sin sesión) ──
    Task<Result<PortalDocumentoDto>> GetDocumentoAsync(string token, CancellationToken ct = default);
    Task<Result<DteArchivosDto>> GetArchivosAsync(string token, CancellationToken ct = default);
    Task<Result<PortalEstadoCuentaDto>> GetEstadoCuentaAsync(string token, CancellationToken ct = default);
    /// <summary>QR de pago: del documento del token, o de una factura del estado de cuenta del token.</summary>
    Task<Result<CobroQrDto>> GetQrPagoAsync(string token, int? dteDocumentoId, CancellationToken ct = default);
    /// <summary>Reenvía el DTE del token al correo indicado (o al del receptor).</summary>
    Task<Result> ReenviarCorreoAsync(string token, string? destinatario, CancellationToken ct = default);
}

public class GenerarEnlacePortalRequest
{
    [Range(1, 365)] public int DiasValidez { get; set; } = 30;
    [StringLength(200)] public string? Nota { get; set; }
}

public class PortalEnlaceDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = null!;
    public int? DteDocumentoId { get; set; }
    public string? NumeroControl { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public DateTime ExpiraAt { get; set; }
    public DateTime? RevocadoAt { get; set; }
    public int Accesos { get; set; }
    public DateTime? UltimoAccesoAt { get; set; }
    public string? Nota { get; set; }
    public bool Activo { get; set; }
    /// <summary>Token en claro — SOLO se devuelve al crear el enlace (no se persiste).</summary>
    public string? Token { get; set; }
}

public class PortalDocumentoDto
{
    public string EmpresaNombre { get; set; } = null!;
    public string TipoDteCodigo { get; set; } = null!;
    public string NumeroControl { get; set; } = null!;
    public string CodigoGeneracion { get; set; } = null!;
    public string? SelloRecibido { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public DateTime FechaEmision { get; set; }
    public string? ReceptorNombre { get; set; }
    public string? ReceptorCorreo { get; set; }
    public decimal TotalPagar { get; set; }
    public string? TotalLetras { get; set; }
    /// <summary>Hay cuenta de cobro activa → puede mostrarse QR/enlace de pago.</summary>
    public bool PagoDisponible { get; set; }
}

public class PortalEstadoCuentaDto
{
    public string EmpresaNombre { get; set; } = null!;
    public SaldoClienteDto Saldo { get; set; } = null!;
    public bool PagoDisponible { get; set; }
}
