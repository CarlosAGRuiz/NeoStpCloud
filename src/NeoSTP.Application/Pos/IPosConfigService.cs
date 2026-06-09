using NeoSTP.Application.Common;
using NeoSTP.Application.Pos.Dtos;

namespace NeoSTP.Application.Pos;

/// <summary>NEOPOS — configuración de impresoras e impresión por red (ESC/POS).</summary>
public interface IPosConfigService
{
    Task<Result<List<ImpresoraPosDto>>> ListImpresorasAsync(int empresaId, CancellationToken ct = default);
    Task<Result<ImpresoraPosDto>> GetImpresoraAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<ImpresoraPosDto>> GuardarImpresoraAsync(int empresaId, int? id, GuardarImpresoraRequest request, string? actor, CancellationToken ct = default);
    Task<Result> EliminarImpresoraAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    /// <summary>Imprime el ticket de una venta enviando ESC/POS a la impresora de red indicada.</summary>
    Task<Result> ImprimirVentaEnRedAsync(int empresaId, int ventaId, int impresoraId, string? actor, CancellationToken ct = default);

    /// <summary>Envía un ticket de prueba ESC/POS a la impresora de red.</summary>
    Task<Result> ProbarImpresoraAsync(int empresaId, int impresoraId, string? actor, CancellationToken ct = default);
}

/// <summary>Envía bytes crudos (ESC/POS) a una impresora de red por TCP.</summary>
public interface INetworkPrinter
{
    Task<Result> EnviarAsync(string ip, int puerto, byte[] datos, CancellationToken ct = default);
}
