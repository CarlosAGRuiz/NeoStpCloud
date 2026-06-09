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
        "USER_NOT_FOUND" or "ROLE_NOT_FOUND" or "CAT_NOT_FOUND" or "CAT_ITEM_NOT_FOUND"
            or "EMPRESA_NOT_FOUND" or "PLAN_NOT_FOUND" or "MODULO_NOT_FOUND"
            or "SUCURSAL_NOT_FOUND" or "PV_NOT_FOUND"
            or "CLIENTE_NOT_FOUND" or "PRODUCTO_NOT_FOUND"
            or "CONFIG_NOT_FOUND" or "DTE_NOT_FOUND"
            or "CERT_MATRIZ_NOT_FOUND" or "CERT_ESCENARIO_NOT_FOUND" or "CERT_PRUEBA_NOT_FOUND"
            or "EVENTO_NOT_FOUND" or "LOTE_NOT_FOUND"
            or "QUOTA_NOT_FOUND" or "IP_NOT_FOUND"
            or "APIKEY_NOT_FOUND" or "WEBHOOK_NOT_FOUND"
            or "GASTO_NOT_FOUND" or "COMPRA_NOT_FOUND" or "PAGO_NOT_FOUND" or "SCAN_NOT_FOUND"
            or "ALERTA_NOT_FOUND" or "DISPOSITIVO_NOT_FOUND" or "CUENTA_NOT_FOUND"
            or "EMPLEADO_NOT_FOUND" or "PLANILLA_NOT_FOUND" or "DETALLE_NOT_FOUND" or "RECIBIDO_NOT_FOUND"
            or "CUENTA_TES_NOT_FOUND" or "MOVIMIENTO_NOT_FOUND"
            or "PROVEEDOR_NOT_FOUND" or "FACTURA_COMPRA_NOT_FOUND" or "PAGO_PROVEEDOR_NOT_FOUND"
            or "VENTA_POS_NOT_FOUND" or "IMPRESORA_NOT_FOUND" or "SESION_CAJA_NOT_FOUND"
            or "CRM_CONTACTO_NOT_FOUND" or "CRM_ETAPA_NOT_FOUND" or "CRM_OPORTUNIDAD_NOT_FOUND"
            or "CRM_ACTIVIDAD_NOT_FOUND" => NotFound(payload),
        "INVALID_STATE" or "STOCK_INSUFICIENTE" or "CAJA_ABIERTA" => Conflict(payload),
        "IP_DUPLICATE" => Conflict(payload),
        "IP_INVALID" => BadRequest(payload),
        "FIRMA_FAILED" or "HACIENDA_AUTH_FAILED" or "EMAIL_FAILED"
            or "LOTE_ENVIO_FAILED" or "LOTE_CONSULTA_FAILED" or "BACKUP_FAILED"
            or "PRINTER_FAILED" or "PRINTER_TIMEOUT" or "PRINTER_NO_IP" => StatusCode(StatusCodes.Status502BadGateway, payload),
        "DECRYPT_FAILED" => StatusCode(StatusCodes.Status500InternalServerError, payload),
        "USER_DUPLICATE" or "ROLE_DUPLICATE" or "ROLE_SYSTEM" or "EMPRESA_DUPLICATE"
            or "SUCURSAL_DUPLICATE" or "PV_DUPLICATE" or "LIMIT_EXCEEDED"
            or "CLIENTE_DUPLICATE" or "PRODUCTO_DUPLICATE"
            or "CAT_DUPLICATE" or "CAT_ITEM_DUPLICATE"
            or "CAT_SYSTEM_NOT_EDITABLE" or "CAT_ITEM_SYSTEM" or "CAT_ITEM_HAS_CHILDREN"
            or "DUPLICATE" => Conflict(payload),
        "CAT_PARENT_NOT_FOUND" or "CAT_PARENT_SELF" => BadRequest(payload),
        "CERT_TIPO_MISMATCH" or "CERT_NADA_PENDIENTE" or "CERT_NO_ESCENARIOS"
            or "EVENTO_SIN_SELLO" or "SIN_DTE_RELACIONADOS" or "SIN_JWS_DISPONIBLE"
            or "ESTADO_INVALIDO" or "SIN_CODIGO_LOTE" => Conflict(payload),
        "ALREADY_REVOKED" or "WEBHOOK_TEST_FAILED" => Conflict(payload),
        "EMPRESA_FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, payload),
        "LICENSE_INVALID" => StatusCode(StatusCodes.Status402PaymentRequired, payload),
        "VALIDATION" or "PWD_WEAK" => BadRequest(payload),
        "PWD_INVALID" or "AUTH_INVALID_CREDENTIALS" or "AUTH_USER_INACTIVE"
            or "AUTH_USER_LOCKED" or "AUTH_REFRESH_INVALID" => Unauthorized(payload),
        _ => BadRequest(payload),
    };
}
