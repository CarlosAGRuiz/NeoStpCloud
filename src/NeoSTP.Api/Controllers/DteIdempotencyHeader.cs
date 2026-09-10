using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;

namespace NeoSTP.Api.Controllers;

public static class DteIdempotencyHeader
{
    public static Result Apply(HttpRequest? http, CreateDteDocumentoRequest request)
    {
        if (http is not null && http.Headers.TryGetValue("Idempotency-Key", out var values))
        {
            if (values.Count != 1 || (request.IdempotencyKey is not null && request.IdempotencyKey != values[0]))
                return Result.Fail("Envíe una sola Idempotency-Key, igual a la del cuerpo si también la incluye allí.", "IDEMPOTENCY_KEY_INVALID");
            request.IdempotencyKey = values[0];
        }
        return DteIdempotency.ValidateKey(request.IdempotencyKey);
    }
}
