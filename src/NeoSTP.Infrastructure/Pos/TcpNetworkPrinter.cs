using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Common;
using NeoSTP.Application.Pos;

namespace NeoSTP.Infrastructure.Pos;

/// <summary>
/// Envía bytes ESC/POS a una impresora térmica de red por TCP (puerto típico 9100, RAW/JetDirect).
/// Tiempo de espera acotado; cualquier fallo de red se devuelve como Result fallido.
/// </summary>
public class TcpNetworkPrinter : INetworkPrinter
{
    private readonly ILogger<TcpNetworkPrinter> _logger;

    public TcpNetworkPrinter(ILogger<TcpNetworkPrinter> logger) => _logger = logger;

    public async Task<Result> EnviarAsync(string ip, int puerto, byte[] datos, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ip)) return Result.Fail("La impresora no tiene IP configurada.", "PRINTER_NO_IP");

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            await client.ConnectAsync(ip, puerto, cts.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(datos, cts.Token);
            await stream.FlushAsync(cts.Token);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            return Result.Fail($"No se pudo conectar a la impresora {ip}:{puerto} (tiempo agotado).", "PRINTER_TIMEOUT");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error imprimiendo en {Ip}:{Puerto}", ip, puerto);
            return Result.Fail($"Error al imprimir en {ip}:{puerto}: {ex.Message}", "PRINTER_FAILED");
        }
    }
}
