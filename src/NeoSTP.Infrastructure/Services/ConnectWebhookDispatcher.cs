using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class ConnectWebhookDispatcher : IConnectWebhookDispatcher
{
    private const int MaxIntentos = 5;

    private readonly NeoStpDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConnectWebhookDispatcher> _logger;

    public ConnectWebhookDispatcher(NeoStpDbContext db, IHttpClientFactory httpClientFactory, ILogger<ConnectWebhookDispatcher> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Task DispatchAsync(ConnectDteEventoPayload payload, CancellationToken ct = default)
        => EncolarAsync(payload.EmpresaId, payload.Evento, payload, ct);

    public Task DispatchNegocioAsync(ConnectEventoNegocioPayload payload, CancellationToken ct = default)
        => EncolarAsync(payload.EmpresaId, payload.Evento, payload, ct);

    /// <summary>
    /// Deja una entrega PENDIENTE por cada webhook suscrito. El envío real lo hace el worker,
    /// así que la operación que emite el evento no espera ni falla por la integración.
    /// </summary>
    private async Task EncolarAsync(int empresaId, string evento, object payload, CancellationToken ct)
    {
        try
        {
            var webhooks = await _db.ConnectWebhooks.AsNoTracking()
                .Where(w => w.EmpresaId == empresaId && w.Activo)
                .ToListAsync(ct);

            var suscritos = webhooks.Where(w =>
                w.Eventos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Contains(evento, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (suscritos.Count == 0)
                return;

            var payloadJson = JsonSerializer.Serialize(payload, payload.GetType(), PayloadJson);

            var ahora = DateTime.UtcNow;
            var deliveries = suscritos.Select(w => new ConnectWebhookDelivery
            {
                WebhookId = w.Id,
                EmpresaId = empresaId,
                Evento = evento,
                Payload = payloadJson,
                Estado = ConnectDeliveryEstados.Pendiente,
                Intentos = 0,
                ProximoIntento = ahora,
                CreatedAt = ahora,
            }).ToList();

            _db.ConnectWebhookDeliveries.AddRange(deliveries);
            await _db.SaveChangesAsync(ct);

            _logger.LogDebug("ConnectWebhookDispatcher: {Count} entregas creadas para evento {Evento} empresa {EmpresaId}",
                deliveries.Count, evento, empresaId);
        }
        catch (Exception ex)
        {
            // Best-effort: la integración nunca debe tumbar la operación del negocio.
            _logger.LogWarning(ex, "ConnectWebhookDispatcher: error al despachar evento {Evento} empresa {EmpresaId}",
                evento, empresaId);
        }
    }

    public async Task<int> ProcesarPendientesAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;

        var pendientes = await _db.ConnectWebhookDeliveries
            .Include(d => d.Webhook)
            .Where(d => d.Estado == ConnectDeliveryEstados.Pendiente
                     && d.ProximoIntento <= ahora)
            .OrderBy(d => d.ProximoIntento)
            .Take(50)
            .ToListAsync(ct);

        if (pendientes.Count == 0)
            return 0;

        var procesados = 0;
        foreach (var delivery in pendientes)
        {
            try
            {
                await ProcesarDeliveryAsync(delivery, ct);
                procesados++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ConnectWebhookDispatcher: error procesando delivery id={Id}", delivery.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
        return procesados;
    }

    private async Task ProcesarDeliveryAsync(ConnectWebhookDelivery delivery, CancellationToken ct)
    {
        delivery.Intentos++;
        var (httpStatus, error) = await EnviarAsync(
            delivery.Webhook.Url,
            delivery.Webhook.SecretoHmac,
            delivery.Payload,
            delivery.Evento,
            ct);

        if (httpStatus is >= 200 and < 300)
        {
            delivery.Estado = ConnectDeliveryEstados.Entregado;
            delivery.HttpStatus = httpStatus;
            delivery.EntregadoAt = DateTime.UtcNow;
            delivery.Error = null;

            // Actualizar UltimaEntregaAt del webhook
            await _db.ConnectWebhooks
                .Where(w => w.Id == delivery.WebhookId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.UltimaEntregaAt, DateTime.UtcNow), ct);

            _logger.LogInformation("ConnectWebhook entregado: deliveryId={Id} webhookId={Wid} status={Status}",
                delivery.Id, delivery.WebhookId, httpStatus);
        }
        else if (delivery.Intentos >= MaxIntentos)
        {
            delivery.Estado = ConnectDeliveryEstados.Fallido;
            delivery.HttpStatus = httpStatus;
            delivery.Error = error;
            _logger.LogWarning("ConnectWebhook fallido tras {N} intentos: deliveryId={Id} error={Error}",
                delivery.Intentos, delivery.Id, error);
        }
        else
        {
            // Backoff exponencial: 2^intentos minutos (2, 4, 8, 16 min)
            var backoffMinutes = (int)Math.Pow(2, delivery.Intentos);
            delivery.ProximoIntento = DateTime.UtcNow.AddMinutes(backoffMinutes);
            delivery.HttpStatus = httpStatus;
            delivery.Error = error;
            _logger.LogDebug("ConnectWebhook reintento {N} en {M}min: deliveryId={Id}",
                delivery.Intentos, backoffMinutes, delivery.Id);
        }
    }

    private async Task<(int? Status, string? Error)> EnviarAsync(
        string url, string secreto, string payload, string evento, CancellationToken ct)
    {
        try
        {
            var signature = ConnectWebhookService.ComputeHmac(secreto, payload);
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            request.Headers.Add("X-NeoConnect-Signature", $"sha256={signature}");
            request.Headers.Add("X-NeoConnect-Event", evento);

            var response = await client.SendAsync(request, ct);
            return ((int)response.StatusCode, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message[..Math.Min(ex.Message.Length, 500)]);
        }
    }
}
