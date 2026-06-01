using NeoSTP.Application.Common;

namespace NeoSTP.Application.Ops;

public sealed class AgregarIpRequest
{
    public string IpCidr { get; set; } = null!;
    public string? Descripcion { get; set; }
}

public sealed class ToggleIpRequest
{
    public bool Activo { get; set; }
}

public sealed record AdminIpAllowlistDto
{
    public int Id { get; init; }
    public string IpCidr { get; init; } = null!;
    public string? Descripcion { get; init; }
    public bool Activo { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Lista blanca de IP/CIDR para el acceso al panel administrativo (SuperAdmin).
/// Si no hay entradas activas, el acceso no se restringe (fail-open por seguridad
/// operativa: evita auto-bloqueo). Soporta IP exacta y rangos CIDR (IPv4/IPv6).
/// </summary>
public interface IAdminIpAllowlistService
{
    /// <summary>True si la IP está permitida o si la lista está vacía.</summary>
    Task<bool> EstaPermitidaAsync(string? ip, CancellationToken ct = default);

    Task<IReadOnlyList<AdminIpAllowlistDto>> ListarAsync(CancellationToken ct = default);
    Task<Result<AdminIpAllowlistDto>> AgregarAsync(string ipCidr, string? descripcion, string? actor, CancellationToken ct = default);
    Task<Result> ToggleAsync(int id, bool activo, string? actor, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, string? actor, CancellationToken ct = default);
}
