using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Common;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Respond<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<T>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
        }

        var resp = ApiResponse<T>.Fail(result.Error ?? "Error", result.ValidationErrors, HttpContext.TraceIdentifier);
        return MapError(result.ErrorCode, resp);
    }

    protected IActionResult Respond(Result result, string? okMessage = null)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse.Ok(okMessage, HttpContext.TraceIdentifier));
        }

        var resp = ApiResponse.Fail(result.Error ?? "Error", result.ValidationErrors, HttpContext.TraceIdentifier);
        return MapError(result.ErrorCode, resp);
    }

    private IActionResult MapError(string? errorCode, object payload) => errorCode switch
    {
        "USER_NOT_FOUND" or "ROLE_NOT_FOUND" or "CAT_NOT_FOUND"
            or "EMPRESA_NOT_FOUND" or "PLAN_NOT_FOUND" or "MODULO_NOT_FOUND"
            or "SUCURSAL_NOT_FOUND" or "PV_NOT_FOUND" => NotFound(payload),
        "USER_DUPLICATE" or "ROLE_DUPLICATE" or "ROLE_SYSTEM" or "EMPRESA_DUPLICATE"
            or "SUCURSAL_DUPLICATE" or "PV_DUPLICATE" or "LIMIT_EXCEEDED" => Conflict(payload),
        "EMPRESA_FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, payload),
        "LICENSE_INVALID" => StatusCode(StatusCodes.Status402PaymentRequired, payload),
        "VALIDATION" or "PWD_WEAK" => BadRequest(payload),
        "PWD_INVALID" or "AUTH_INVALID_CREDENTIALS" or "AUTH_USER_INACTIVE"
            or "AUTH_USER_LOCKED" or "AUTH_REFRESH_INVALID" => Unauthorized(payload),
        _ => BadRequest(payload),
    };
}
