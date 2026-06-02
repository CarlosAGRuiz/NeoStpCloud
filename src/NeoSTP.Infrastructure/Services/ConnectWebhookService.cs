using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class ConnectWebhookService : IConnectWebhookService
{
    private readonly NeoStpDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConnectWebhookService> _logger;

    public ConnectWebhookService(NeoStpDbContext db, IHttpClientFactory httpClientFactory, ILogger<ConnectWebhookService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConnectWebhookDto>> ListarAsync(int empresaId, CancellationToken ct = default)
    {
        var list = await _db.ConnectWebhooks.AsNoTracking()
            .Where(w => w.EmpresaId == empresaId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<Result<ConnectWebhookDto>> ObtenerAsync(int id, int empresaId, CancellationToken ct = default)
    {
        var wh = await _db.ConnectWebhooks.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id && w.EmpresaId == empresaId, ct);

        return wh is null
            ? Result<ConnectWebhookDto>.Fail("Webhook no encontrado.", "WEBHOOK_NOT_FOUND")
            : Result<ConnectWebhookDto>.Ok(ToDto(wh));
    }

    public async Task<Result<ConnectWebhookDto>> CrearAsync(CrearWebhookRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return Result<ConnectWebhookDto>.Fail("La URL es obligatoria.", "VALIDATION");

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
            return Result<ConnectWebhookDto>.Fail("La URL debe ser una dirección HTTP/HTTPS válida.", "VALIDATION");

        if (request.Eventos.Length == 0)
            return Result<ConnectWebhookDto>.Fail("Debe suscribirse a al menos un evento.", "VALIDATION");

        var secreto = GenerateHmacSecret();

        var entity = new ConnectWebhook
        {
            EmpresaId = request.EmpresaId,
            ApiKeyId = request.ApiKeyId,
            Url = request.Url.Trim(),
            SecretoHmac = secreto,
            Eventos = string.Join(",", request.Eventos),
            Activo = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };

        _db.ConnectWebhooks.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ConnectWebhook creado: id={Id} empresa={EmpresaId} url={Url}",
            entity.Id, entity.EmpresaId, entity.Url);

        return Result<ConnectWebhookDto>.Ok(ToDto(entity));
    }

    public async Task<Result> EliminarAsync(int id, int empresaId, string? actor, CancellationToken ct = default)
    {
        var wh = await _db.ConnectWebhooks
            .FirstOrDefaultAsync(w => w.Id == id && w.EmpresaId == empresaId, ct);

        if (wh is null)
            return Result.Fail("Webhook no encontrado.", "WEBHOOK_NOT_FOUND");

        _db.ConnectWebhooks.Remove(wh);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ConnectWebhook eliminado: id={Id} empresa={EmpresaId} por={Actor}", id, empresaId, actor);
        return Result.Ok();
    }

    public async Task<Result<ConnectWebhookDeliveryDto>> TestAsync(int webhookId, int empresaId, string? actor, CancellationToken ct = default)
    {
        var wh = await _db.ConnectWebhooks.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == webhookId && w.EmpresaId == empresaId, ct);

        if (wh is null)
            return Result<ConnectWebhookDeliveryDto>.Fail("Webhook no encontrado.", "WEBHOOK_NOT_FOUND");

        var payload = JsonSerializer.Serialize(new
        {
            evento = "TEST",
            empresaId,
            webhookId,
            timestamp = DateTime.UtcNow,
            message = "NeoConnect webhook test"
        });

        var (httpStatus, error) = await EnviarAsync(wh.Url, wh.SecretoHmac, payload, ct);
        var estado = httpStatus is >= 200 and < 300 ? ConnectDeliveryEstados.Entregado : ConnectDeliveryEstados.Fallido;

        var delivery = new ConnectWebhookDelivery
        {
            WebhookId = webhookId,
            EmpresaId = empresaId,
            Evento = "TEST",
            Payload = payload,
            Estado = estado,
            HttpStatus = httpStatus,
            Intentos = 1,
            EntregadoAt = estado == ConnectDeliveryEstados.Entregado ? DateTime.UtcNow : null,
            Error = error,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };

        _db.ConnectWebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);

        return estado == ConnectDeliveryEstados.Entregado
            ? Result<ConnectWebhookDeliveryDto>.Ok(ToDeliveryDto(delivery))
            : Result<ConnectWebhookDeliveryDto>.Fail(
                $"Test fallido (HTTP {httpStatus}): {error}",
                "WEBHOOK_TEST_FAILED");
    }

    public async Task<IReadOnlyList<ConnectWebhookDeliveryDto>> ListarDeliveriesAsync(int empresaId, int limit, CancellationToken ct = default)
    {
        var list = await _db.ConnectWebhookDeliveries.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return list.Select(ToDeliveryDto).ToList();
    }

    public async Task<IReadOnlyList<ConnectUsageDto>> ObtenerUsageAsync(int empresaId, CancellationToken ct = default)
    {
        var hoy = DateTime.UtcNow.Date;
        var hace7 = hoy.AddDays(-7);

        var keys = await _db.ConnectApiKeys.AsNoTracking()
            .Where(k => k.EmpresaId == empresaId)
            .Select(k => new { k.Id, k.Nombre })
            .ToListAsync(ct);

        var keyIds = keys.Select(k => k.Id).ToList();

        var usagePorKey = await _db.ApiUsageLogs.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.ApiKeyId.HasValue && keyIds.Contains(l.ApiKeyId!.Value))
            .GroupBy(l => l.ApiKeyId)
            .Select(g => new
            {
                ApiKeyId = g.Key,
                Total = g.Count(),
                Hoy = g.Count(l => l.OcurrioAt >= hoy),
                Ultimos7 = g.Count(l => l.OcurrioAt >= hace7),
            })
            .ToListAsync(ct);

        var deliveryPorKey = await _db.ConnectWebhooks.AsNoTracking()
            .Where(w => w.EmpresaId == empresaId && w.ApiKeyId.HasValue && keyIds.Contains(w.ApiKeyId!.Value))
            .Join(_db.ConnectWebhookDeliveries,
                w => w.Id,
                d => d.WebhookId,
                (w, d) => new { w.ApiKeyId, d.Estado })
            .GroupBy(x => x.ApiKeyId)
            .Select(g => new
            {
                ApiKeyId = g.Key,
                Entregados = g.Count(x => x.Estado == ConnectDeliveryEstados.Entregado),
                Fallidos = g.Count(x => x.Estado == ConnectDeliveryEstados.Fallido),
            })
            .ToListAsync(ct);

        return keys.Select(k =>
        {
            var u = usagePorKey.FirstOrDefault(x => x.ApiKeyId == k.Id);
            var d = deliveryPorKey.FirstOrDefault(x => x.ApiKeyId == k.Id);
            return new ConnectUsageDto
            {
                ApiKeyId = k.Id,
                ApiKeyNombre = k.Nombre,
                TotalPeticiones = u?.Total ?? 0,
                PeticionesHoy = u?.Hoy ?? 0,
                PeticionesUltimos7Dias = u?.Ultimos7 ?? 0,
                WebhooksEntregados = d?.Entregados ?? 0,
                WebhooksFallidos = d?.Fallidos ?? 0,
            };
        }).ToList();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string GenerateHmacSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private async Task<(int? Status, string? Error)> EnviarAsync(string url, string secreto, string payload, CancellationToken ct)
    {
        try
        {
            var signature = ComputeHmac(secreto, payload);
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            request.Headers.Add("X-NeoConnect-Signature", $"sha256={signature}");
            request.Headers.Add("X-NeoConnect-Event", "TEST");

            var response = await client.SendAsync(request, ct);
            return ((int)response.StatusCode, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enviando webhook test a {Url}", url);
            return (null, ex.Message[..Math.Min(ex.Message.Length, 500)]);
        }
    }

    public static string ComputeHmac(string secret, string payload)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static ConnectWebhookDto ToDto(ConnectWebhook w) => new()
    {
        Id = w.Id,
        EmpresaId = w.EmpresaId,
        ApiKeyId = w.ApiKeyId,
        Url = w.Url,
        SecretoHmacDisplay = w.SecretoHmac.Length > 8
            ? w.SecretoHmac[..8] + "…"
            : w.SecretoHmac,
        Eventos = w.Eventos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Activo = w.Activo,
        UltimaEntregaAt = w.UltimaEntregaAt,
        CreatedAt = w.CreatedAt,
    };

    private static ConnectWebhookDeliveryDto ToDeliveryDto(ConnectWebhookDelivery d) => new()
    {
        Id = d.Id,
        WebhookId = d.WebhookId,
        Evento = d.Evento,
        Estado = d.Estado,
        HttpStatus = d.HttpStatus,
        Intentos = d.Intentos,
        EntregadoAt = d.EntregadoAt,
        Error = d.Error,
        CreatedAt = d.CreatedAt,
    };
}
