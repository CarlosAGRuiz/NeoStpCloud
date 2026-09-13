using NeoSTP.Application.Auth.Abstractions;

namespace NeoSTP.Api.Auth;

internal readonly record struct ApiTenantResolution(
    bool Success,
    int? EmpresaId,
    int StatusCode,
    string ErrorCode,
    string Message);

internal static class ApiTenantResolver
{
    public static ApiTenantResolution Resolve(ICurrentUser currentUser, int? requestedEmpresaId)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        if (currentUser.EmpresaId is int ownEmpresaId)
        {
            if (requestedEmpresaId is int requested && requested != ownEmpresaId)
                return Fail(403, "TENANT_OVERRIDE_FORBIDDEN",
                    "No puedes operar sobre una empresa diferente a la de tu sesión.");
            return Ok(ownEmpresaId);
        }

        if (string.Equals(currentUser.TipoUsuarioCodigo, "SUPERADMIN", StringComparison.Ordinal))
        {
            return requestedEmpresaId is > 0
                ? Ok(requestedEmpresaId.Value)
                : Fail(400, "AUTH_NO_TENANT",
                    "Selecciona explícitamente una empresa para operar en modo soporte.");
        }

        return Fail(403, "AUTH_NO_TENANT",
            "La sesión no tiene una empresa autorizada.");
    }

    private static ApiTenantResolution Ok(int empresaId)
        => new(true, empresaId, 200, string.Empty, string.Empty);

    private static ApiTenantResolution Fail(int status, string code, string message)
        => new(false, null, status, code, message);
}
