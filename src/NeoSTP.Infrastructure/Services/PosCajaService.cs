using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Domain.Core.Pos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NEOPOS (Sprint 4) — sesiones / corte de caja. Abre con un fondo, acumula las ventas del turno
/// (ligadas por <c>VentaPos.SesionCajaId</c>) y cierra comparando efectivo esperado vs contado
/// (<see cref="CorteCajaCalculator"/>). Aislada por EmpresaId.
/// </summary>
public class PosCajaService : IPosCajaService
{
    private const string AuditModule = "NEOPOS_CAJA";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public PosCajaService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<SesionCajaDto?>> GetEstadoAsync(int empresaId, CancellationToken ct = default)
    {
        var s = await _db.SesionesCaja.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.EstadoCodigo == SesionCajaEstados.Abierta, ct);
        return Result<SesionCajaDto?>.Ok(s is null ? null : await ToDtoConTotalesAsync(s, ct));
    }

    public async Task<Result<SesionCajaDto>> AbrirAsync(int empresaId, AbrirCajaRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.MontoInicial < 0) return Result<SesionCajaDto>.Fail("El fondo inicial no puede ser negativo.", "VALIDATION");
        var yaAbierta = await _db.SesionesCaja.AnyAsync(x => x.EmpresaId == empresaId && x.EstadoCodigo == SesionCajaEstados.Abierta, ct);
        if (yaAbierta) return Result<SesionCajaDto>.Fail("Ya hay una caja abierta. Ciérrala antes de abrir otra.", "CAJA_ABIERTA");

        var s = new SesionCaja
        {
            EmpresaId = empresaId, SucursalId = request.SucursalId, PuntoVentaId = request.PuntoVentaId,
            Numero = await SiguienteNumeroAsync(empresaId, ct), EstadoCodigo = SesionCajaEstados.Abierta,
            AbiertaAt = DateTime.UtcNow, MontoInicial = decimal.Round(request.MontoInicial, 2, MidpointRounding.AwayFromZero),
            AbiertaPor = actor, Nota = request.Nota?.Trim(), CreatedBy = actor,
        };
        _db.SesionesCaja.Add(s);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ABRIR_CAJA", $"{s.Numero} · fondo {s.MontoInicial:N2}", s.Id);
        return Result<SesionCajaDto>.Ok(await ToDtoConTotalesAsync(s, ct));
    }

    public async Task<Result<SesionCajaDto>> CerrarAsync(int empresaId, int sesionId, CerrarCajaRequest request, string? actor, CancellationToken ct = default)
    {
        var s = await _db.SesionesCaja.FirstOrDefaultAsync(x => x.Id == sesionId && x.EmpresaId == empresaId, ct);
        if (s is null) return Result<SesionCajaDto>.Fail("Sesión de caja no encontrada.", "SESION_CAJA_NOT_FOUND");
        if (s.EstadoCodigo == SesionCajaEstados.Cerrada) return Result<SesionCajaDto>.Fail("La caja ya está cerrada.", "INVALID_STATE");
        if (request.MontoContado < 0) return Result<SesionCajaDto>.Fail("El efectivo contado no puede ser negativo.", "VALIDATION");

        var t = await TotalesAsync(s.Id, ct);
        var esperado = CorteCajaCalculator.Esperado(s.MontoInicial, t.efectivo);
        var contado = decimal.Round(request.MontoContado, 2, MidpointRounding.AwayFromZero);
        s.MontoEsperado = esperado;
        s.MontoContado = contado;
        s.Diferencia = CorteCajaCalculator.Diferencia(contado, esperado);
        s.CerradaAt = DateTime.UtcNow;
        s.CerradaPor = actor;
        s.EstadoCodigo = SesionCajaEstados.Cerrada;
        if (!string.IsNullOrWhiteSpace(request.Nota)) s.Nota = request.Nota.Trim();
        s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CERRAR_CAJA", $"{s.Numero} · esperado {esperado:N2} · contado {contado:N2} · dif {s.Diferencia:N2}", s.Id);
        return Result<SesionCajaDto>.Ok(ToDto(s, t));
    }

    public async Task<Result<SesionCajaDto>> GetAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var s = await _db.SesionesCaja.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return s is null
            ? Result<SesionCajaDto>.Fail("Sesión de caja no encontrada.", "SESION_CAJA_NOT_FOUND")
            : Result<SesionCajaDto>.Ok(await ToDtoConTotalesAsync(s, ct));
    }

    public async Task<Result<PagedResult<SesionCajaDto>>> ListAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.SesionesCaja.AsNoTracking().Where(s => s.EmpresaId == empresaId);
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = await q.OrderByDescending(s => s.AbiertaAt).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var ids = rows.Select(s => s.Id).ToList();
        var ventas = await _db.VentasPos.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.EstadoCodigo == VentaPosEstados.Completada && v.SesionCajaId != null && ids.Contains(v.SesionCajaId!.Value))
            .Select(v => new { SesionId = v.SesionCajaId!.Value, v.Total, v.FormaPagoCodigo })
            .ToListAsync(ct);

        var items = rows.Select(s =>
        {
            var vs = ventas.Where(a => a.SesionId == s.Id).ToList();
            (int count, decimal total, decimal efectivo, decimal tarjeta, decimal otros) t = (
                vs.Count, vs.Sum(x => x.Total),
                vs.Where(x => x.FormaPagoCodigo == FormasPagoPos.Efectivo).Sum(x => x.Total),
                vs.Where(x => x.FormaPagoCodigo == FormasPagoPos.Tarjeta).Sum(x => x.Total),
                vs.Where(x => x.FormaPagoCodigo != FormasPagoPos.Efectivo && x.FormaPagoCodigo != FormasPagoPos.Tarjeta).Sum(x => x.Total));
            return ToDto(s, t);
        }).ToList();
        return Result<PagedResult<SesionCajaDto>>.Ok(PagedResult<SesionCajaDto>.Create(items, total, page, pageSize));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(int count, decimal total, decimal efectivo, decimal tarjeta, decimal otros)> TotalesAsync(int sesionId, CancellationToken ct)
    {
        var v = await _db.VentasPos.AsNoTracking()
            .Where(x => x.SesionCajaId == sesionId && x.EstadoCodigo == VentaPosEstados.Completada)
            .Select(x => new { x.Total, x.FormaPagoCodigo }).ToListAsync(ct);
        return (v.Count, v.Sum(x => x.Total),
            v.Where(x => x.FormaPagoCodigo == FormasPagoPos.Efectivo).Sum(x => x.Total),
            v.Where(x => x.FormaPagoCodigo == FormasPagoPos.Tarjeta).Sum(x => x.Total),
            v.Where(x => x.FormaPagoCodigo != FormasPagoPos.Efectivo && x.FormaPagoCodigo != FormasPagoPos.Tarjeta).Sum(x => x.Total));
    }

    private async Task<SesionCajaDto> ToDtoConTotalesAsync(SesionCaja s, CancellationToken ct)
        => ToDto(s, await TotalesAsync(s.Id, ct));

    private static SesionCajaDto ToDto(SesionCaja s, (int count, decimal total, decimal efectivo, decimal tarjeta, decimal otros) t) => new()
    {
        Id = s.Id, Numero = s.Numero, EstadoCodigo = s.EstadoCodigo, SucursalId = s.SucursalId, PuntoVentaId = s.PuntoVentaId,
        AbiertaAt = s.AbiertaAt, MontoInicial = s.MontoInicial, AbiertaPor = s.AbiertaPor,
        CerradaAt = s.CerradaAt, MontoEsperado = s.MontoEsperado, MontoContado = s.MontoContado, Diferencia = s.Diferencia,
        CerradaPor = s.CerradaPor, Nota = s.Nota,
        Ventas = t.count, TotalVentas = t.total, TotalEfectivo = t.efectivo, TotalTarjeta = t.tarjeta, TotalOtros = t.otros,
        EfectivoEsperado = CorteCajaCalculator.Esperado(s.MontoInicial, t.efectivo),
    };

    private async Task<string> SiguienteNumeroAsync(int empresaId, CancellationToken ct)
    {
        var count = await _db.SesionesCaja.CountAsync(s => s.EmpresaId == empresaId, ct);
        return $"CAJA-{count + 1:000000}";
    }

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "SesionCaja", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
