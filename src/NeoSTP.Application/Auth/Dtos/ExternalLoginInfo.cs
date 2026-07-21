namespace NeoSTP.Application.Auth.Dtos;

/// <summary>
/// Claims normalizados que llegan del proveedor OIDC tras un login federado (E3).
/// La capa Web/API los extrae del id_token y los entrega a
/// <see cref="Abstractions.IAuthService.LoginExternoAsync"/>.
/// </summary>
public sealed class ExternalLoginInfo
{
    /// <summary>Proveedor: ENTRA | GOOGLE (SsoProveedores).</summary>
    public string Proveedor { get; set; } = null!;

    /// <summary>Identificador estable del sujeto (claim "sub"/"oid"). Ancla la vinculación.</summary>
    public string Subject { get; set; } = null!;

    public string? Email { get; set; }
    public string? NombreCompleto { get; set; }

    /// <summary>Tenant id del directorio de origen (Entra "tid"), si el proveedor lo entrega.</summary>
    public string? TenantIdExterno { get; set; }
}
