using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Connect;

public class ConnectApiKey : AuditableEntity
{
    public int EmpresaId { get; set; }

    /// <summary>Nombre descriptivo para identificar la key en la UI.</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>Prefijo de los primeros 8 caracteres de la key en texto plano (para mostrar en UI).</summary>
    public string Prefix { get; set; } = null!;

    /// <summary>SHA-256 de la key completa. Nunca se devuelve al cliente tras la creación.</summary>
    public string KeyHash { get; set; } = null!;

    /// <summary>Scopes autorizados separados por coma: DTE:Read, DTE:Write, Clientes:Read, Webhooks:Manage.</summary>
    public string Scopes { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTime? ExpiresAt { get; set; }
    public DateTime? UltimoUsoAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }

    public ICollection<ConnectWebhook> Webhooks { get; set; } = [];
}

public static class ConnectScopes
{
    public const string DteRead = "DTE:Read";
    public const string DteWrite = "DTE:Write";
    public const string ClientesRead = "Clientes:Read";
    public const string ProductosRead = "Productos:Read";
    public const string WebhooksManage = "Webhooks:Manage";

    public static readonly string[] All = [DteRead, DteWrite, ClientesRead, ProductosRead, WebhooksManage];
}
