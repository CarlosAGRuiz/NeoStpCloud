using NeoSTP.Application.Common;

namespace NeoSTP.Application.Cobranza;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed class CuentaCobroDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = "TRANSFERENCIA";
    public string Nombre { get; set; } = string.Empty;
    public string? Banco { get; set; }
    public string? NumeroCuenta { get; set; }
    public string? Titular { get; set; }
    public string? UrlPago { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
}

public sealed class CrearCuentaCobroRequest
{
    public string Tipo { get; set; } = "TRANSFERENCIA";
    public string Nombre { get; set; } = null!;
    public string? Banco { get; set; }
    public string? NumeroCuenta { get; set; }
    public string? Titular { get; set; }
    public string? UrlPago { get; set; }
}

public sealed class GenerarQrCobroRequest
{
    /// <summary>Factura a cobrar (opcional). Si se indica, el monto/referencia se toman de ella.</summary>
    public int? DteDocumentoId { get; set; }
    /// <summary>Cuenta de cobro a usar. Si se omite, se usa la primera activa.</summary>
    public int? CuentaCobroId { get; set; }
    /// <summary>Monto del cobro (obligatorio si no hay DteDocumentoId).</summary>
    public decimal? Monto { get; set; }
    public string? Referencia { get; set; }
}

public sealed class CobroQrDto
{
    public decimal Monto { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public string CuentaNombre { get; set; } = string.Empty;
    /// <summary>Contenido codificado en el QR (URL de pago o texto de transferencia).</summary>
    public string Payload { get; set; } = string.Empty;
    /// <summary>Imagen PNG del QR en base64 (para mostrar/compartir en la app).</summary>
    public string QrPngBase64 { get; set; } = string.Empty;
}

// ─── Interfaz ────────────────────────────────────────────────────────────────

/// <summary>
/// QR/enlaces de cobro: administra las cuentas de cobro de la empresa y genera un código QR
/// de pago (asociado a una factura o a un monto). Aislado por EmpresaId.
/// </summary>
public interface ICobroQrService
{
    Task<IReadOnlyList<CuentaCobroDto>> ListarCuentasAsync(int empresaId, CancellationToken ct = default);
    Task<Result<CuentaCobroDto>> CrearCuentaAsync(int empresaId, CrearCuentaCobroRequest request, string? actor, CancellationToken ct = default);
    Task<Result<CuentaCobroDto>> ActualizarCuentaAsync(int empresaId, int id, CrearCuentaCobroRequest request, string? actor, CancellationToken ct = default);
    Task<Result> InactivarCuentaAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    /// <summary>Genera el QR de pago (payload + PNG base64) para una factura o un monto.</summary>
    Task<Result<CobroQrDto>> GenerarQrAsync(int empresaId, GenerarQrCobroRequest request, CancellationToken ct = default);
}
