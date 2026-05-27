using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Clientes.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class ClientesService : IClientesService
{
    private const string AuditModule = "CLIENTES";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ClientesService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<ClienteDto>>> GetListAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.Clientes.AsNoTracking().Where(c => c.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(c => EF.Functions.Like(c.Nombre, $"%{s}%")
                          || EF.Functions.Like(c.NumeroDocumento, $"%{s}%")
                          || EF.Functions.Like(c.Nrc ?? string.Empty, $"%{s}%")
                          || EF.Functions.Like(c.NombreComercial ?? string.Empty, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync(ct);

        return Result<PagedResult<ClienteDto>>.Ok(PagedResult<ClienteDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ClienteDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var c = await _db.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return c is null
            ? Result<ClienteDto>.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND")
            : Result<ClienteDto>.Ok(MapToDto(c));
    }

    public async Task<Result<ClienteDto>> CreateAsync(int empresaId, CreateClienteRequest request, string? actor, CancellationToken ct = default)
    {
        var errors = ClienteValidator.Validate(request);
        if (errors.Count > 0)
            return Result<ClienteDto>.Fail("Datos del cliente inválidos.", "VALIDATION", errors);

        var tipoDoc = request.TipoDocumentoCodigo.Trim().ToUpperInvariant();
        var numero = tipoDoc == "NIT"
            ? ClienteValidator.NormalizeNit(request.NumeroDocumento)
            : request.NumeroDocumento.Trim();

        var dup = await _db.Clientes.AnyAsync(c =>
            c.EmpresaId == empresaId &&
            c.TipoDocumentoCodigo == tipoDoc &&
            c.NumeroDocumento == numero, ct);
        if (dup)
            return Result<ClienteDto>.Fail($"Ya existe un cliente con {tipoDoc} {numero}.", "CLIENTE_DUPLICATE");

        var cliente = new Cliente
        {
            EmpresaId = empresaId,
            TipoDocumentoCodigo = tipoDoc,
            NumeroDocumento = numero,
            Nrc = string.IsNullOrWhiteSpace(request.Nrc) ? null : request.Nrc.Trim(),
            Nombre = request.Nombre.Trim(),
            NombreComercial = request.NombreComercial?.Trim(),
            TipoContribuyenteCodigo = request.TipoContribuyenteCodigo.Trim().ToUpperInvariant(),
            CodigoActividad = request.CodigoActividad?.Trim(),
            ActividadEconomica = request.ActividadEconomica?.Trim(),
            DepartamentoCodigo = request.DepartamentoCodigo,
            MunicipioCodigo = request.MunicipioCodigo,
            Direccion = request.Direccion,
            Correo = request.Correo?.Trim(),
            Telefono = request.Telefono,
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = DateTime.UtcNow, CreatedBy = actor,
        };
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREATE", "OK", $"Cliente {cliente.Nombre} creado", cliente.Id);

        return Result<ClienteDto>.Ok(MapToDto(cliente));
    }

    public async Task<Result<ClienteDto>> UpdateAsync(int empresaId, int id, UpdateClienteRequest request, string? actor, CancellationToken ct = default)
    {
        var errors = ClienteValidator.Validate(request);
        if (errors.Count > 0)
            return Result<ClienteDto>.Fail("Datos del cliente inválidos.", "VALIDATION", errors);

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId, ct);
        if (cliente is null) return Result<ClienteDto>.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");

        cliente.Nombre = request.Nombre.Trim();
        cliente.NombreComercial = request.NombreComercial?.Trim();
        cliente.TipoContribuyenteCodigo = request.TipoContribuyenteCodigo.Trim().ToUpperInvariant();
        cliente.Nrc = string.IsNullOrWhiteSpace(request.Nrc) ? null : request.Nrc.Trim();
        cliente.CodigoActividad = request.CodigoActividad?.Trim();
        cliente.ActividadEconomica = request.ActividadEconomica?.Trim();
        cliente.DepartamentoCodigo = request.DepartamentoCodigo;
        cliente.MunicipioCodigo = request.MunicipioCodigo;
        cliente.Direccion = request.Direccion;
        cliente.Correo = request.Correo?.Trim();
        cliente.Telefono = request.Telefono;
        if (!string.IsNullOrWhiteSpace(request.EstadoCodigo)) cliente.EstadoCodigo = request.EstadoCodigo;
        cliente.UpdatedAt = DateTime.UtcNow;
        cliente.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPDATE", "OK", $"Cliente {cliente.Nombre} actualizado", cliente.Id);

        return Result<ClienteDto>.Ok(MapToDto(cliente));
    }

    public async Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId, ct);
        if (cliente is null) return Result.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");

        cliente.EstadoCodigo = EstadoCodes.Inactivo;
        cliente.UpdatedAt = DateTime.UtcNow;
        cliente.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INACTIVAR", "OK", $"Cliente {cliente.Nombre} inactivado", cliente.Id);
        return Result.Ok();
    }

    private static ClienteDto MapToDto(Cliente c) => new()
    {
        Id = c.Id, EmpresaId = c.EmpresaId,
        TipoDocumentoCodigo = c.TipoDocumentoCodigo,
        NumeroDocumento = c.NumeroDocumento,
        Nrc = c.Nrc,
        Nombre = c.Nombre, NombreComercial = c.NombreComercial,
        TipoContribuyenteCodigo = c.TipoContribuyenteCodigo,
        EsContribuyente = c.EsContribuyente,
        CodigoActividad = c.CodigoActividad,
        ActividadEconomica = c.ActividadEconomica,
        DepartamentoCodigo = c.DepartamentoCodigo,
        MunicipioCodigo = c.MunicipioCodigo,
        Direccion = c.Direccion,
        Correo = c.Correo, Telefono = c.Telefono,
        EstadoCodigo = c.EstadoCodigo,
        CreatedAt = c.CreatedAt,
    };

    private Task Audit(int empresaId, string? actor, string accion, string resultado, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "Cliente", EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });
}
