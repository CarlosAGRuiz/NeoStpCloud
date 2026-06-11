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

        // Normaliza códigos MH del CAT-022 (13/36/…) al código interno (DUI/NIT/…).
        var tipoDoc = ClienteValidator.NormalizarTipoDocumento(request.TipoDocumentoCodigo);
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

    public async Task<Result> RestaurarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId, ct);
        if (cliente is null) return Result.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");
        if (cliente.EstadoCodigo == EstadoCodes.Activo)
            return Result.Fail("El cliente ya está activo.", "INVALID_STATE");

        cliente.EstadoCodigo = EstadoCodes.Activo;
        cliente.UpdatedAt = DateTime.UtcNow;
        cliente.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RESTAURAR", "OK", $"Cliente {cliente.Nombre} restaurado", cliente.Id);
        return Result.Ok();
    }

    private static readonly string[] EtiquetasValidas = ["VIP", "FRECUENTE"];

    public async Task<Result> SetEtiquetaAsync(int empresaId, int id, string? etiqueta, string? actor, CancellationToken ct = default)
    {
        var limpia = string.IsNullOrWhiteSpace(etiqueta) ? null : etiqueta.Trim().ToUpperInvariant();
        if (limpia is not null && !EtiquetasValidas.Contains(limpia))
            return Result.Fail("Etiqueta inválida. Usa VIP, FRECUENTE o vacío.", "VALIDATION");

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId, ct);
        if (cliente is null) return Result.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");
        cliente.Etiqueta = limpia;
        cliente.UpdatedAt = DateTime.UtcNow;
        cliente.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ETIQUETA", "OK", $"Cliente {cliente.Nombre}: etiqueta {limpia ?? "(ninguna)"}", cliente.Id);
        return Result.Ok();
    }

    public async Task<Result<BulkImportResult>> ImportAsync(int empresaId, BulkImportRequest request, string? actor, CancellationToken ct = default)
    {
        IReadOnlyList<TabularRow> rows;
        try
        {
            rows = TabularParser.Parse(request.Content, request.Format);
        }
        catch (Exception ex)
        {
            return Result<BulkImportResult>.Fail($"No se pudo leer el archivo: {ex.Message}", "IMPORT_PARSE_ERROR");
        }

        var result = new BulkImportResult { DryRun = request.DryRun, Total = rows.Count };
        var existentes = await _db.Clientes
            .Where(c => c.EmpresaId == empresaId)
            .ToDictionaryAsync(c => $"{c.TipoDocumentoCodigo}|{c.NumeroDocumento}", c => c, ct);

        foreach (var row in rows)
        {
            var req = new CreateClienteRequest
            {
                TipoDocumentoCodigo = row.Get("tipodocumento") ?? row.Get("tipodocumentocodigo") ?? "DUI",
                NumeroDocumento = row.Get("numerodocumento") ?? string.Empty,
                Nrc = row.Get("nrc"),
                Nombre = row.Get("nombre") ?? string.Empty,
                NombreComercial = row.Get("nombrecomercial"),
                TipoContribuyenteCodigo = row.Get("tipocontribuyente") ?? row.Get("tipocontribuyentecodigo") ?? "CONSUMIDOR_FINAL",
                CodigoActividad = row.Get("codigoactividad"),
                ActividadEconomica = row.Get("actividadeconomica"),
                DepartamentoCodigo = row.Get("departamento") ?? row.Get("departamentocodigo"),
                MunicipioCodigo = row.Get("municipio") ?? row.Get("municipiocodigo"),
                Direccion = row.Get("direccion"),
                Correo = row.Get("correo"),
                Telefono = row.Get("telefono"),
            };

            var errors = ClienteValidator.Validate(req);
            if (errors.Count > 0)
            {
                result.Errors.Add(new BulkImportError { Row = row.RowNumber, Key = req.NumeroDocumento, Message = string.Join("; ", errors) });
                continue;
            }

            var tipoDoc = req.TipoDocumentoCodigo.Trim().ToUpperInvariant();
            var numero = tipoDoc == "NIT" ? ClienteValidator.NormalizeNit(req.NumeroDocumento) : req.NumeroDocumento.Trim();
            var key = $"{tipoDoc}|{numero}";

            if (existentes.TryGetValue(key, out var existing))
            {
                if (!request.DryRun) ApplyUpdate(existing, req, actor);
                result.Updated++;
            }
            else
            {
                var nuevo = BuildCliente(empresaId, req, tipoDoc, numero, actor);
                if (!request.DryRun) _db.Clientes.Add(nuevo);
                existentes[key] = nuevo; // evita duplicados dentro del mismo archivo
                result.Inserted++;
            }
        }

        if (!request.DryRun && result.ErrorCount < result.Total)
        {
            await _db.SaveChangesAsync(ct);
            await Audit(empresaId, actor, "IMPORT", "OK", $"Carga masiva clientes: {result.Inserted} nuevos, {result.Updated} actualizados, {result.ErrorCount} errores", 0);
        }

        return Result<BulkImportResult>.Ok(result);
    }

    private static Cliente BuildCliente(int empresaId, CreateClienteRequest req, string tipoDoc, string numero, string? actor) => new()
    {
        EmpresaId = empresaId,
        TipoDocumentoCodigo = tipoDoc,
        NumeroDocumento = numero,
        Nrc = string.IsNullOrWhiteSpace(req.Nrc) ? null : req.Nrc.Trim(),
        Nombre = req.Nombre.Trim(),
        NombreComercial = req.NombreComercial?.Trim(),
        TipoContribuyenteCodigo = req.TipoContribuyenteCodigo.Trim().ToUpperInvariant(),
        CodigoActividad = req.CodigoActividad?.Trim(),
        ActividadEconomica = req.ActividadEconomica?.Trim(),
        DepartamentoCodigo = req.DepartamentoCodigo,
        MunicipioCodigo = req.MunicipioCodigo,
        Direccion = req.Direccion,
        Correo = req.Correo?.Trim(),
        Telefono = req.Telefono,
        EstadoCodigo = EstadoCodes.Activo,
        CreatedAt = DateTime.UtcNow, CreatedBy = actor,
    };

    private static void ApplyUpdate(Cliente c, CreateClienteRequest req, string? actor)
    {
        c.Nombre = req.Nombre.Trim();
        c.NombreComercial = req.NombreComercial?.Trim();
        c.TipoContribuyenteCodigo = req.TipoContribuyenteCodigo.Trim().ToUpperInvariant();
        c.Nrc = string.IsNullOrWhiteSpace(req.Nrc) ? null : req.Nrc.Trim();
        c.CodigoActividad = req.CodigoActividad?.Trim();
        c.ActividadEconomica = req.ActividadEconomica?.Trim();
        c.DepartamentoCodigo = req.DepartamentoCodigo;
        c.MunicipioCodigo = req.MunicipioCodigo;
        c.Direccion = req.Direccion;
        c.Correo = req.Correo?.Trim();
        c.Telefono = req.Telefono;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = actor;
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
        Etiqueta = c.Etiqueta,
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
