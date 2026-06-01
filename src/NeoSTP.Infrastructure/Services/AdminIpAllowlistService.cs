using System.Net;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Core.Ops;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>Implementación de la lista blanca de IP del panel admin. Ver <see cref="IAdminIpAllowlistService"/>.</summary>
public class AdminIpAllowlistService : IAdminIpAllowlistService
{
    private const string AuditModule = "HARDENING";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public AdminIpAllowlistService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<bool> EstaPermitidaAsync(string? ip, CancellationToken ct = default)
    {
        var activas = await _db.AdminIpAllowlist.AsNoTracking()
            .Where(e => e.Activo)
            .Select(e => e.IpCidr)
            .ToListAsync(ct);

        if (activas.Count == 0)
            return true; // fail-open: lista vacía = sin restricción

        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var addr))
            return false;

        return activas.Any(entry => Coincide(entry, addr));
    }

    public async Task<IReadOnlyList<AdminIpAllowlistDto>> ListarAsync(CancellationToken ct = default)
        => await _db.AdminIpAllowlist.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new AdminIpAllowlistDto
            {
                Id = e.Id, IpCidr = e.IpCidr, Descripcion = e.Descripcion, Activo = e.Activo, CreatedAt = e.CreatedAt,
            })
            .ToListAsync(ct);

    public async Task<Result<AdminIpAllowlistDto>> AgregarAsync(string ipCidr, string? descripcion, string? actor, CancellationToken ct = default)
    {
        ipCidr = ipCidr?.Trim() ?? string.Empty;
        if (!EsValida(ipCidr))
            return Result<AdminIpAllowlistDto>.Fail("IP o CIDR inválido.", "IP_INVALID");

        if (await _db.AdminIpAllowlist.AnyAsync(e => e.IpCidr == ipCidr, ct))
            return Result<AdminIpAllowlistDto>.Fail("La entrada ya existe.", "IP_DUPLICATE");

        var entry = new AdminIpAllowlistEntry
        {
            IpCidr = ipCidr,
            Descripcion = descripcion,
            Activo = true,
            CreatedBy = actor,
        };
        _db.AdminIpAllowlist.Add(entry);
        await _db.SaveChangesAsync(ct);

        await Audit(actor, "IP_ALLOWLIST_ADD", entry.Id.ToString(), ipCidr);
        return Result<AdminIpAllowlistDto>.Ok(new AdminIpAllowlistDto
        {
            Id = entry.Id, IpCidr = entry.IpCidr, Descripcion = entry.Descripcion, Activo = entry.Activo, CreatedAt = entry.CreatedAt,
        });
    }

    public async Task<Result> ToggleAsync(int id, bool activo, string? actor, CancellationToken ct = default)
    {
        var entry = await _db.AdminIpAllowlist.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null)
            return Result.Fail("Entrada no encontrada.", "IP_NOT_FOUND");

        entry.Activo = activo;
        entry.UpdatedBy = actor;
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await Audit(actor, "IP_ALLOWLIST_TOGGLE", id.ToString(), $"{entry.IpCidr} => {(activo ? "ACTIVO" : "INACTIVO")}");
        return Result.Ok();
    }

    public async Task<Result> EliminarAsync(int id, string? actor, CancellationToken ct = default)
    {
        var entry = await _db.AdminIpAllowlist.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null)
            return Result.Fail("Entrada no encontrada.", "IP_NOT_FOUND");

        _db.AdminIpAllowlist.Remove(entry);
        await _db.SaveChangesAsync(ct);

        await Audit(actor, "IP_ALLOWLIST_REMOVE", id.ToString(), entry.IpCidr);
        return Result.Ok();
    }

    // -- matching ---------------------------------------------------------

    private static bool Coincide(string entry, IPAddress addr)
    {
        if (entry.Contains('/'))
            return IPNetwork.TryParse(entry, out var net) && net.Contains(addr);
        return IPAddress.TryParse(entry, out var single) && single.Equals(addr);
    }

    private static bool EsValida(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return false;
        return entry.Contains('/')
            ? IPNetwork.TryParse(entry, out _)
            : IPAddress.TryParse(entry, out _);
    }

    private Task Audit(string? actor, string accion, string entidadId, string detalle)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = "AdminIpAllowlist",
            EntidadId = entidadId,
            Resultado = "OK",
            Detalle = detalle,
        });
}
