using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Domain.Core.Pos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NEOPOS — administración de impresoras e impresión por red (ESC/POS). Aislado por EmpresaId.
/// </summary>
public class PosConfigService : IPosConfigService
{
    private const string AuditModule = "NEOPOS";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IPosService _pos;
    private readonly INetworkPrinter _printer;

    public PosConfigService(NeoStpDbContext db, IAuditoriaService auditoria, IPosService pos, INetworkPrinter printer)
    {
        _db = db;
        _auditoria = auditoria;
        _pos = pos;
        _printer = printer;
    }

    public async Task<Result<List<ImpresoraPosDto>>> ListImpresorasAsync(int empresaId, CancellationToken ct = default)
    {
        var items = await _db.ImpresorasPos.AsNoTracking()
            .Where(i => i.EmpresaId == empresaId)
            .OrderByDescending(i => i.EsPredeterminada).ThenBy(i => i.Nombre)
            .Select(i => ToDto(i)).ToListAsync(ct);
        return Result<List<ImpresoraPosDto>>.Ok(items);
    }

    public async Task<Result<ImpresoraPosDto>> GetImpresoraAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var i = await _db.ImpresorasPos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return i is null ? Result<ImpresoraPosDto>.Fail("Impresora no encontrada.", "IMPRESORA_NOT_FOUND") : Result<ImpresoraPosDto>.Ok(ToDto(i));
    }

    public async Task<Result<ImpresoraPosDto>> GuardarImpresoraAsync(int empresaId, int? id, GuardarImpresoraRequest request, string? actor, CancellationToken ct = default)
    {
        if (!ConexionImpresora.All.Contains(request.Conexion)) return Result<ImpresoraPosDto>.Fail("Tipo de conexión inválido.", "VALIDATION");
        if (request.Conexion == ConexionImpresora.Red && string.IsNullOrWhiteSpace(request.Ip))
            return Result<ImpresoraPosDto>.Fail("La conexión de red requiere IP.", "VALIDATION");
        var ancho = request.AnchoMm <= 58 ? 58 : 80;

        ImpresoraPos imp;
        if (id is int existingId)
        {
            var found = await _db.ImpresorasPos.FirstOrDefaultAsync(x => x.Id == existingId && x.EmpresaId == empresaId, ct);
            if (found is null) return Result<ImpresoraPosDto>.Fail("Impresora no encontrada.", "IMPRESORA_NOT_FOUND");
            imp = found;
            imp.UpdatedAt = DateTime.UtcNow; imp.UpdatedBy = actor;
        }
        else
        {
            imp = new ImpresoraPos { EmpresaId = empresaId, CreatedBy = actor };
            _db.ImpresorasPos.Add(imp);
        }

        imp.Nombre = request.Nombre.Trim();
        imp.Conexion = request.Conexion;
        imp.AnchoMm = ancho;
        imp.Ip = request.Ip?.Trim();
        imp.Puerto = request.Puerto <= 0 ? 9100 : request.Puerto;
        imp.CorteAutomatico = request.CorteAutomatico;
        imp.EsPredeterminada = request.EsPredeterminada;
        imp.Notas = request.Notas?.Trim();
        imp.EstadoCodigo = EstadosImpresora.Activa;

        await _db.SaveChangesAsync(ct);

        // Sólo una predeterminada por empresa.
        if (imp.EsPredeterminada)
        {
            var otras = await _db.ImpresorasPos
                .Where(x => x.EmpresaId == empresaId && x.Id != imp.Id && x.EsPredeterminada)
                .ToListAsync(ct);
            if (otras.Count > 0)
            {
                foreach (var o in otras) o.EsPredeterminada = false;
                await _db.SaveChangesAsync(ct);
            }
        }

        await Audit(empresaId, actor, id is null ? "CREAR_IMPRESORA" : "EDITAR_IMPRESORA", imp.Nombre, imp.Id);
        return Result<ImpresoraPosDto>.Ok(ToDto(imp));
    }

    public async Task<Result> EliminarImpresoraAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var imp = await _db.ImpresorasPos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (imp is null) return Result.Fail("Impresora no encontrada.", "IMPRESORA_NOT_FOUND");
        _db.ImpresorasPos.Remove(imp);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ELIMINAR_IMPRESORA", imp.Nombre, id);
        return Result.Ok();
    }

    public async Task<Result> ImprimirVentaEnRedAsync(int empresaId, int ventaId, int impresoraId, string? actor, CancellationToken ct = default)
    {
        var imp = await _db.ImpresorasPos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == impresoraId && x.EmpresaId == empresaId, ct);
        if (imp is null) return Result.Fail("Impresora no encontrada.", "IMPRESORA_NOT_FOUND");
        if (imp.Conexion != ConexionImpresora.Red) return Result.Fail("La impresora no es de red.", "VALIDATION");

        var ticket = await _pos.GetTicketAsync(empresaId, ventaId, ct);
        if (ticket.IsFailure) return Result.Fail(ticket.Error ?? "Venta no encontrada.", ticket.ErrorCode);

        ticket.Value!.AnchoMm = imp.AnchoMm;
        var datos = EscPosTicketBuilder.Build(ticket.Value);
        var res = await _printer.EnviarAsync(imp.Ip!, imp.Puerto, datos, ct);
        if (res.IsSuccess) await Audit(empresaId, actor, "IMPRIMIR_RED", $"{ticket.Value.Numero} → {imp.Nombre}", ventaId);
        return res;
    }

    public async Task<Result> ProbarImpresoraAsync(int empresaId, int impresoraId, string? actor, CancellationToken ct = default)
    {
        var imp = await _db.ImpresorasPos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == impresoraId && x.EmpresaId == empresaId, ct);
        if (imp is null) return Result.Fail("Impresora no encontrada.", "IMPRESORA_NOT_FOUND");
        if (imp.Conexion != ConexionImpresora.Red) return Result.Fail("La impresora no es de red.", "VALIDATION");

        var empresaNombre = await _db.Empresas.AsNoTracking().Where(e => e.Id == empresaId).Select(e => e.RazonSocial).FirstOrDefaultAsync(ct) ?? "NeoSTP";
        var demo = new TicketModel
        {
            EmpresaNombre = empresaNombre, Numero = "PRUEBA", Fecha = DateTime.UtcNow, AnchoMm = imp.AnchoMm,
            ClienteNombre = "—", FormaPago = "—", Total = 0m, PieTicket = "Impresora configurada correctamente",
            Lineas = [new TicketLinea { Descripcion = "Ticket de prueba", Cantidad = 1, PrecioUnitario = 0m, Total = 0m }],
        };
        return await _printer.EnviarAsync(imp.Ip!, imp.Puerto, EscPosTicketBuilder.Build(demo), ct);
    }

    private static ImpresoraPosDto ToDto(ImpresoraPos i) => new()
    {
        Id = i.Id, Nombre = i.Nombre, Conexion = i.Conexion, AnchoMm = i.AnchoMm, Ip = i.Ip, Puerto = i.Puerto,
        CorteAutomatico = i.CorteAutomatico, EsPredeterminada = i.EsPredeterminada, EstadoCodigo = i.EstadoCodigo, Notas = i.Notas,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "ImpresoraPos", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
