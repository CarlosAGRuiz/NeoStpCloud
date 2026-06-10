using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Application.Tesoreria.Dtos;
using NeoSTP.Domain.Core.Tesoreria;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// V2-D4 — Conciliación bancaria básica. Importa estados de cuenta (CSV/XLSX, reusa
/// <see cref="TabularParser"/>), sugiere matches con <see cref="ConciliacionCalculator"/>
/// (monto exacto + signo + ventana de fecha + referencia) y concilia/desconcilia manualmente.
/// La importación deduplica por (cuenta, fecha, monto, referencia, descripción).
/// </summary>
public class ConciliacionBancariaService : IConciliacionBancariaService
{
    private const string AuditModule = "NEOTESORERIA";
    private static readonly string[] FormatosFecha = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "MM/dd/yyyy"];

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ConciliacionBancariaService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // ── Importación ──────────────────────────────────────────────────────────

    public async Task<Result<BulkImportResult>> ImportarAsync(int empresaId, int cuentaId, BulkImportRequest request, string? actor, CancellationToken ct = default)
    {
        var cuenta = await _db.CuentasTesoreria.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cuentaId && c.EmpresaId == empresaId, ct);
        if (cuenta is null) return Result<BulkImportResult>.Fail("Cuenta no encontrada.", "CUENTA_TES_NOT_FOUND");

        IReadOnlyList<TabularRow> rows;
        try
        {
            rows = TabularParser.Parse(request.Content, request.Format);
        }
        catch (Exception ex)
        {
            return Result<BulkImportResult>.Fail($"No se pudo leer el archivo: {ex.Message}", "VALIDATION");
        }
        if (rows.Count == 0) return Result<BulkImportResult>.Fail("El archivo no tiene filas de datos.", "VALIDATION");

        // Claves existentes para deduplicar reimportes del mismo estado de cuenta.
        var existentes = (await _db.MovimientosBancarios.AsNoTracking()
                .Where(m => m.EmpresaId == empresaId && m.CuentaTesoreriaId == cuentaId)
                .Select(m => new { m.Fecha, m.Monto, m.Referencia, m.Descripcion })
                .ToListAsync(ct))
            .Select(m => ClaveDedupe(m.Fecha, m.Monto, m.Referencia, m.Descripcion))
            .ToHashSet();

        var result = new BulkImportResult { DryRun = request.DryRun, Total = rows.Count };
        var nuevos = new List<MovimientoBancario>();
        foreach (var row in rows)
        {
            var (mov, error) = ParseFila(row);
            if (error is not null)
            {
                result.Errors.Add(new BulkImportError { Row = row.RowNumber, Message = error });
                continue;
            }

            var clave = ClaveDedupe(mov!.Fecha, mov.Monto, mov.Referencia, mov.Descripcion);
            if (!existentes.Add(clave))
            {
                result.Skipped++;
                continue;
            }

            mov.EmpresaId = empresaId;
            mov.CuentaTesoreriaId = cuentaId;
            mov.CreatedBy = actor;
            nuevos.Add(mov);
            result.Inserted++;
        }

        if (!request.DryRun && nuevos.Count > 0)
        {
            _db.MovimientosBancarios.AddRange(nuevos);
            await _db.SaveChangesAsync(ct);
            await Audit(empresaId, actor, "CONCILIACION_IMPORTAR",
                $"Importadas {nuevos.Count} línea(s) bancarias a la cuenta {cuenta.Codigo}.", "CuentaTesoreria", cuentaId);
        }
        return Result<BulkImportResult>.Ok(result);
    }

    /// <summary>
    /// Columnas aceptadas: fecha (requerida); monto con signo, o cargo/abono (o debito/credito)
    /// por separado; descripcion/concepto/detalle; referencia/documento.
    /// </summary>
    internal static (MovimientoBancario? Mov, string? Error) ParseFila(TabularRow row)
    {
        var fechaTexto = row.Get("fecha");
        if (fechaTexto is null) return (null, "Falta la columna 'fecha'.");
        if (!TryParseFecha(fechaTexto, out var fecha)) return (null, $"Fecha inválida: '{fechaTexto}'.");

        decimal monto;
        var montoTexto = row.Get("monto");
        if (montoTexto is not null)
        {
            if (!TryParseMonto(montoTexto, out monto)) return (null, $"Monto inválido: '{montoTexto}'.");
        }
        else
        {
            var cargoTexto = row.Get("cargo") ?? row.Get("debito") ?? row.Get("débito");
            var abonoTexto = row.Get("abono") ?? row.Get("credito") ?? row.Get("crédito");
            decimal cargo = 0, abono = 0;
            if (cargoTexto is not null && !TryParseMonto(cargoTexto, out cargo)) return (null, $"Cargo inválido: '{cargoTexto}'.");
            if (abonoTexto is not null && !TryParseMonto(abonoTexto, out abono)) return (null, $"Abono inválido: '{abonoTexto}'.");
            monto = abono - Math.Abs(cargo);
        }
        if (monto == 0) return (null, "El monto no puede ser cero (usa 'monto' con signo, o 'cargo'/'abono').");

        var descripcion = row.Get("descripcion") ?? row.Get("descripción") ?? row.Get("concepto") ?? row.Get("detalle");
        var referencia = row.Get("referencia") ?? row.Get("documento") ?? row.Get("ref");
        if (descripcion is null && referencia is null) return (null, "Falta 'descripcion' o 'referencia'.");

        return (new MovimientoBancario
        {
            Fecha = fecha,
            Monto = decimal.Round(monto, 2),
            Descripcion = Truncar(descripcion ?? referencia!, 200),
            Referencia = referencia is null ? null : Truncar(referencia, 80),
        }, null);
    }

    internal static bool TryParseFecha(string texto, out DateOnly fecha)
    {
        if (DateOnly.TryParseExact(texto, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)) return true;
        if (DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            fecha = DateOnly.FromDateTime(dt);
            return true;
        }
        return false;
    }

    internal static bool TryParseMonto(string texto, out decimal monto)
    {
        var limpio = texto.Replace("$", string.Empty).Replace(" ", string.Empty);
        // Formato contable: (123.45) = negativo.
        var negativo = limpio.StartsWith('(') && limpio.EndsWith(')');
        if (negativo) limpio = limpio[1..^1];
        if (!decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out monto)) return false;
        if (negativo) monto = -monto;
        return true;
    }

    private static string ClaveDedupe(DateOnly fecha, decimal monto, string? referencia, string descripcion)
        => $"{fecha:yyyyMMdd}|{monto.ToString("F2", CultureInfo.InvariantCulture)}|{referencia?.Trim().ToUpperInvariant()}|{descripcion.Trim().ToUpperInvariant()}";

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..max];

    // ── Consulta y sugerencias ───────────────────────────────────────────────

    public async Task<Result<PagedResult<MovimientoBancarioDto>>> ListAsync(int empresaId, int cuentaId, string? estado, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.MovimientosBancarios.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.CuentaTesoreriaId == cuentaId);
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(m => m.EstadoCodigo == estado);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(m => m.Descripcion.Contains(s) || (m.Referencia != null && m.Referencia.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MovimientoBancarioDto
            {
                Id = m.Id, CuentaTesoreriaId = m.CuentaTesoreriaId, Fecha = m.Fecha,
                Referencia = m.Referencia, Descripcion = m.Descripcion, Monto = m.Monto,
                EstadoCodigo = m.EstadoCodigo, MovimientoTesoreriaId = m.MovimientoTesoreriaId,
                MovimientoTesoreriaConcepto = m.MovimientoTesoreria != null ? m.MovimientoTesoreria.Concepto : null,
                ConciliadoAt = m.ConciliadoAt, ConciliadoPor = m.ConciliadoPor,
            })
            .ToListAsync(ct);
        return Result<PagedResult<MovimientoBancarioDto>>.Ok(PagedResult<MovimientoBancarioDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<IReadOnlyList<SugerenciaConciliacionDto>>> SugerenciasAsync(int empresaId, int cuentaId, int toleranciaDias = 3, CancellationToken ct = default)
    {
        var (banco, internos) = await CandidatosAsync(empresaId, cuentaId, ct);
        var sugerencias = ConciliacionCalculator.Sugerir(banco, internos, toleranciaDias);
        return Result<IReadOnlyList<SugerenciaConciliacionDto>>.Ok(sugerencias);
    }

    /// <summary>Líneas del banco sin conciliar + movimientos internos confirmados aún no vinculados.</summary>
    private async Task<(IReadOnlyList<BancoMatchRow> Banco, IReadOnlyList<InternoMatchRow> Internos)> CandidatosAsync(int empresaId, int cuentaId, CancellationToken ct)
    {
        var banco = await _db.MovimientosBancarios.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.CuentaTesoreriaId == cuentaId && m.EstadoCodigo == EstadosConciliacion.NoConciliado)
            .Select(m => new BancoMatchRow(m.Id, m.Fecha, m.Monto, m.Referencia))
            .ToListAsync(ct);

        var internos = await _db.MovimientosTesoreria.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.CuentaId == cuentaId
                && m.EstadoCodigo == EstadosMovimientoTesoreria.Confirmado
                && !_db.MovimientosBancarios.Any(b => b.MovimientoTesoreriaId == m.Id && b.EstadoCodigo == EstadosConciliacion.Conciliado))
            .Select(m => new InternoMatchRow(m.Id, m.Fecha, m.Monto, m.Tipo, m.Referencia, m.Concepto))
            .ToListAsync(ct);

        return (banco, internos);
    }

    // ── Conciliar / desconciliar ─────────────────────────────────────────────

    public async Task<Result> ConciliarAsync(int empresaId, int movimientoBancoId, int movimientoTesoreriaId, string? actor, CancellationToken ct = default)
    {
        var banco = await _db.MovimientosBancarios
            .FirstOrDefaultAsync(m => m.Id == movimientoBancoId && m.EmpresaId == empresaId, ct);
        if (banco is null) return Result.Fail("Línea bancaria no encontrada.", "MOV_BANCO_NOT_FOUND");
        if (banco.EstadoCodigo == EstadosConciliacion.Conciliado)
            return Result.Fail("La línea ya está conciliada.", "INVALID_STATE");

        var interno = await _db.MovimientosTesoreria.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == movimientoTesoreriaId && m.EmpresaId == empresaId, ct);
        if (interno is null) return Result.Fail("Movimiento de tesorería no encontrado.", "MOVIMIENTO_NOT_FOUND");
        if (interno.CuentaId != banco.CuentaTesoreriaId)
            return Result.Fail("El movimiento pertenece a otra cuenta de tesorería.", "VALIDATION");
        if (interno.EstadoCodigo != EstadosMovimientoTesoreria.Confirmado)
            return Result.Fail("Solo se concilian movimientos confirmados.", "INVALID_STATE");

        var yaUsado = await _db.MovimientosBancarios.AsNoTracking()
            .AnyAsync(m => m.MovimientoTesoreriaId == movimientoTesoreriaId && m.EstadoCodigo == EstadosConciliacion.Conciliado, ct);
        if (yaUsado) return Result.Fail("Ese movimiento de tesorería ya está conciliado con otra línea del banco.", "INVALID_STATE");

        var compatible = ConciliacionCalculator.MontoCompatible(
            new BancoMatchRow(banco.Id, banco.Fecha, banco.Monto, banco.Referencia),
            new InternoMatchRow(interno.Id, interno.Fecha, interno.Monto, interno.Tipo, interno.Referencia, interno.Concepto));
        if (!compatible)
            return Result.Fail("El monto/tipo no coincide: abonos concilian con INGRESOS y cargos con EGRESOS por el mismo monto.", "VALIDATION");

        banco.EstadoCodigo = EstadosConciliacion.Conciliado;
        banco.MovimientoTesoreriaId = movimientoTesoreriaId;
        banco.ConciliadoAt = DateTime.UtcNow;
        banco.ConciliadoPor = actor;
        banco.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CONCILIAR",
            $"Línea bancaria {banco.Id} conciliada con movimiento {movimientoTesoreriaId}.", "MovimientoBancario", banco.Id);
        return Result.Ok();
    }

    public async Task<Result<int>> ConciliarSugeridosAsync(int empresaId, int cuentaId, int toleranciaDias = 3, string? actor = null, CancellationToken ct = default)
    {
        var (banco, internos) = await CandidatosAsync(empresaId, cuentaId, ct);
        var altas = ConciliacionCalculator.Sugerir(banco, internos, toleranciaDias)
            .Where(s => s.Confianza == ConfianzasConciliacion.Alta)
            .ToList();

        var conciliadas = 0;
        foreach (var s in altas)
        {
            var r = await ConciliarAsync(empresaId, s.MovimientoBancoId, s.MovimientoTesoreriaId, actor, ct);
            if (r.IsSuccess) conciliadas++;
        }
        return Result<int>.Ok(conciliadas);
    }

    public async Task<Result> DesconciliarAsync(int empresaId, int movimientoBancoId, string? actor, CancellationToken ct = default)
    {
        var banco = await _db.MovimientosBancarios
            .FirstOrDefaultAsync(m => m.Id == movimientoBancoId && m.EmpresaId == empresaId, ct);
        if (banco is null) return Result.Fail("Línea bancaria no encontrada.", "MOV_BANCO_NOT_FOUND");
        if (banco.EstadoCodigo != EstadosConciliacion.Conciliado)
            return Result.Fail("La línea no está conciliada.", "INVALID_STATE");

        var movimientoAnterior = banco.MovimientoTesoreriaId;
        banco.EstadoCodigo = EstadosConciliacion.NoConciliado;
        banco.MovimientoTesoreriaId = null;
        banco.ConciliadoAt = null;
        banco.ConciliadoPor = null;
        banco.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "DESCONCILIAR",
            $"Línea bancaria {banco.Id} desconciliada (movimiento {movimientoAnterior}).", "MovimientoBancario", banco.Id);
        return Result.Ok();
    }

    public async Task<Result<ConciliacionResumenDto>> ResumenAsync(int empresaId, int cuentaId, CancellationToken ct = default)
    {
        var banco = await _db.MovimientosBancarios.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.CuentaTesoreriaId == cuentaId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Conciliados = g.Count(m => m.EstadoCodigo == EstadosConciliacion.Conciliado),
                MontoPendiente = g.Where(m => m.EstadoCodigo == EstadosConciliacion.NoConciliado)
                    .Sum(m => (decimal?)(m.Monto < 0 ? -m.Monto : m.Monto)) ?? 0,
            })
            .FirstOrDefaultAsync(ct);

        var internosSinConciliar = await _db.MovimientosTesoreria.AsNoTracking()
            .CountAsync(m => m.EmpresaId == empresaId && m.CuentaId == cuentaId
                && m.EstadoCodigo == EstadosMovimientoTesoreria.Confirmado
                && !_db.MovimientosBancarios.Any(b => b.MovimientoTesoreriaId == m.Id && b.EstadoCodigo == EstadosConciliacion.Conciliado), ct);

        return Result<ConciliacionResumenDto>.Ok(new ConciliacionResumenDto
        {
            TotalBanco = banco?.Total ?? 0,
            Conciliados = banco?.Conciliados ?? 0,
            NoConciliados = (banco?.Total ?? 0) - (banco?.Conciliados ?? 0),
            MontoNoConciliado = banco?.MontoPendiente ?? 0,
            InternosSinConciliar = internosSinConciliar,
        });
    }

    private Task Audit(int empresaId, string? actor, string accion, string detalle, string entidad, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = entidad, EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
