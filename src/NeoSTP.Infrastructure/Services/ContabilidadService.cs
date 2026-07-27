using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Conta;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Conta;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Shared;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NEOCONTA (V2-D2). Catálogo mínimo por empresa (se siembra al primer uso), asientos
/// automáticos idempotentes (uno por documento origen) y balanza simple. Los asientos
/// solo se anulan con reversa (asiento espejo); la balanza incluye originales y reversas,
/// que se netean — la doble partida siempre cuadra.
/// </summary>
public class ContabilidadService : IContabilidadService
{
    private const string AuditModule = "NEOCONTA";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ContabilidadService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<IReadOnlyList<CuentaContableDto>>> ListCuentasAsync(int empresaId, CancellationToken ct = default)
    {
        await EnsureCatalogoAsync(empresaId, ct);
        var cuentas = await _db.CuentasContables.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId)
            .OrderBy(c => c.Codigo)
            .Select(c => new CuentaContableDto { Id = c.Id, Codigo = c.Codigo, Nombre = c.Nombre, Tipo = c.Tipo, Activa = c.Activa })
            .ToListAsync(ct);
        return Result<IReadOnlyList<CuentaContableDto>>.Ok(cuentas);
    }

    public async Task<Result<int>> GenerarAsientosPeriodoAsync(int empresaId, int anio, int mes, string? actor, CancellationToken ct = default)
    {
        if (anio is < 2020 or > 2100 || mes is < 1 or > 12)
            return Result<int>.Fail("Período inválido.", "VALIDATION");

        await EnsureCatalogoAsync(empresaId, ct);
        var cuentas = await _db.CuentasContables
            .Where(c => c.EmpresaId == empresaId)
            .ToDictionaryAsync(c => c.Codigo, ct);

        var desdeD = new DateOnly(anio, mes, 1);
        var hastaD = desdeD.AddMonths(1);
        var desdeT = desdeD.ToDateTime(TimeOnly.MinValue);
        var hastaT = hastaD.ToDateTime(TimeOnly.MinValue);

        // Idempotencia: documentos ya asentados (Origen:OrigenId) no se repiten.
        var asentados = await _db.AsientosContables.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.OrigenId != null)
            .Select(a => a.Origen + ":" + a.OrigenId)
            .ToListAsync(ct);
        var set = new HashSet<string>(asentados, StringComparer.Ordinal);

        var correlativo = await _db.AsientosContables.CountAsync(a => a.EmpresaId == empresaId, ct);
        var creados = 0;

        void Agregar(string origen, int origenId, DateOnly fecha, string concepto, params (string cuenta, decimal debe, decimal haber, string? detalle)[] lineas)
        {
            if (!set.Add($"{origen}:{origenId}")) return;
            var efectivas = lineas.Where(l => l.debe != 0 || l.haber != 0).ToList();
            if (efectivas.Count == 0) return;
            var asiento = new AsientoContable
            {
                EmpresaId = empresaId,
                Numero = $"ASI-{++correlativo:000000}",
                Fecha = fecha,
                Concepto = concepto,
                Origen = origen,
                OrigenId = origenId,
                EstadoCodigo = AsientoEstados.Activo,
                TotalDebe = Round2(efectivas.Sum(l => l.debe)),
                TotalHaber = Round2(efectivas.Sum(l => l.haber)),
                CreatedBy = actor,
            };
            foreach (var l in efectivas)
                asiento.Lineas.Add(new AsientoContableLinea
                {
                    CuentaContableId = cuentas[l.cuenta].Id,
                    Debe = Round2(l.debe),
                    Haber = Round2(l.haber),
                    Detalle = l.detalle,
                    CreatedBy = actor,
                });
            _db.AsientosContables.Add(asiento);
            creados++;
        }

        // 1) Ventas DTE procesadas (01 FC con IVA incluido, 03 CCF, 05 NC resta, 06 ND suma)
        string[] tiposVenta = ["01", "03", "05", "06"];
        var ventas = await _db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId && d.EstadoCodigo == DteEstadoCodigos.Procesado
                && d.FechaEmision >= desdeT && d.FechaEmision < hastaT && tiposVenta.Contains(d.TipoDteCodigo))
            .Select(d => new { d.Id, d.TipoDteCodigo, d.NumeroControl, d.FechaEmision, d.MontoTotalOperacion, d.IvaTotal })
            .ToListAsync(ct);
        foreach (var v in ventas)
        {
            var neta = Round2(v.MontoTotalOperacion - v.IvaTotal);
            var fecha = DateOnly.FromDateTime(v.FechaEmision);
            if (v.TipoDteCodigo == "05") // Nota de crédito: espejo de la venta
                Agregar(OrigenesAsiento.VentaDte, v.Id, fecha, $"NC {v.NumeroControl}",
                    (CuentasContablesMinimas.Ventas, neta, 0, null),
                    (CuentasContablesMinimas.IvaDebitoFiscal, v.IvaTotal, 0, null),
                    (CuentasContablesMinimas.CuentasPorCobrar, 0, v.MontoTotalOperacion, null));
            else
                Agregar(OrigenesAsiento.VentaDte, v.Id, fecha, $"Venta {v.TipoDteCodigo} {v.NumeroControl}",
                    (CuentasContablesMinimas.CuentasPorCobrar, v.MontoTotalOperacion, 0, null),
                    (CuentasContablesMinimas.Ventas, 0, neta, null),
                    (CuentasContablesMinimas.IvaDebitoFiscal, 0, v.IvaTotal, null));
        }

        // 2) Cobros confirmados
        var cobros = await _db.PagosCliente.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.EstadoCodigo == "CONFIRMADO" && p.Fecha >= desdeD && p.Fecha < hastaD)
            .Select(p => new { p.Id, p.Fecha, p.Monto, p.DteDocumentoId })
            .ToListAsync(ct);
        foreach (var c in cobros)
            Agregar(OrigenesAsiento.Cobro, c.Id, c.Fecha, $"Cobro de cliente (DTE #{c.DteDocumentoId})",
                (CuentasContablesMinimas.Efectivo, c.Monto, 0, null),
                (CuentasContablesMinimas.CuentasPorCobrar, 0, c.Monto, null));

        // 3) Compras (CxP)
        var compras = await _db.FacturasCompra.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId && f.EstadoCodigo != FacturaCompraEstados.Anulada
                && f.FechaEmision >= desdeD && f.FechaEmision < hastaD)
            .Select(f => new { f.Id, f.FechaEmision, f.NumeroDocumento, f.Subtotal, f.Iva, f.IvaDeducible, f.Total })
            .ToListAsync(ct);
        foreach (var f in compras)
            Agregar(OrigenesAsiento.Compra, f.Id, f.FechaEmision, $"Compra {f.NumeroDocumento}",
                (CuentasContablesMinimas.Compras, f.IvaDeducible ? f.Subtotal : f.Total, 0, null),
                (CuentasContablesMinimas.IvaCreditoFiscal, f.IvaDeducible ? f.Iva : 0, 0, null),
                (CuentasContablesMinimas.CuentasPorPagar, 0, f.Total, null));

        // 4) Pagos a proveedor confirmados
        var pagosProv = await _db.PagosProveedor.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.EstadoCodigo == PagoProveedorEstados.Confirmado
                && p.Fecha >= desdeD && p.Fecha < hastaD)
            .Select(p => new { p.Id, p.Fecha, p.Monto, p.FacturaCompraId })
            .ToListAsync(ct);
        foreach (var p in pagosProv)
            Agregar(OrigenesAsiento.PagoProveedor, p.Id, p.Fecha, $"Pago a proveedor (factura #{p.FacturaCompraId})",
                (CuentasContablesMinimas.CuentasPorPagar, p.Monto, 0, null),
                (CuentasContablesMinimas.Efectivo, 0, p.Monto, null));

        // 5) Gastos (incluye PLANILLA al cerrar nómina; excluye COMPRA para no duplicar la factura)
        var gastos = await _db.ProfitGastos.AsNoTracking()
            .Where(g => g.EmpresaId == empresaId && g.EstadoCodigo == "ACTIVO" && g.Categoria != "COMPRA"
                && g.Fecha >= desdeD && g.Fecha < hastaD)
            .Select(g => new { g.Id, g.Fecha, g.Categoria, g.Descripcion, g.Monto, g.IvaMonto, g.IvaDeducible })
            .ToListAsync(ct);
        foreach (var g in gastos)
            Agregar(OrigenesAsiento.Gasto, g.Id, g.Fecha, $"Gasto {g.Categoria}: {g.Descripcion}",
                (CuentasContablesMinimas.GastosOperacion, g.IvaDeducible ? g.Monto : g.Monto + g.IvaMonto, 0, null),
                (CuentasContablesMinimas.IvaCreditoFiscal, g.IvaDeducible ? g.IvaMonto : 0, 0, null),
                (CuentasContablesMinimas.Efectivo, 0, g.Monto + g.IvaMonto, null));

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "GENERAR_ASIENTOS", $"{anio:0000}-{mes:00}: {creados} asiento(s)", 0);
        return Result<int>.Ok(creados);
    }

    public async Task<Result<PagedResult<AsientoDto>>> ListAsientosAsync(int empresaId, int? anio, int? mes, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.AsientosContables.AsNoTracking().Where(a => a.EmpresaId == empresaId);
        if (anio is int a && mes is int m)
        {
            var desde = new DateOnly(a, m, 1);
            var hasta = desde.AddMonths(1);
            q = q.Where(x => x.Fecha >= desde && x.Fecha < hasta);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Numero.Contains(s) || x.Concepto.Contains(s));
        }
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = await q.Include(x => x.Lineas).ThenInclude(l => l.Cuenta)
            .OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = rows.Select(ToDto).ToList();
        return Result<PagedResult<AsientoDto>>.Ok(PagedResult<AsientoDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<AsientoDto>> GetAsientoAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var asiento = await _db.AsientosContables.AsNoTracking()
            .Include(x => x.Lineas).ThenInclude(l => l.Cuenta)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return asiento is null
            ? Result<AsientoDto>.Fail("Asiento no encontrado.", "ASIENTO_NOT_FOUND")
            : Result<AsientoDto>.Ok(ToDto(asiento));
    }

    public async Task<Result<AsientoDto>> ReversarAsientoAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default)
    {
        var original = await _db.AsientosContables
            .Include(x => x.Lineas).ThenInclude(l => l.Cuenta)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (original is null) return Result<AsientoDto>.Fail("Asiento no encontrado.", "ASIENTO_NOT_FOUND");
        if (original.EstadoCodigo == AsientoEstados.Reversado)
            return Result<AsientoDto>.Fail("El asiento ya fue reversado.", "INVALID_STATE");

        var correlativo = await _db.AsientosContables.CountAsync(a => a.EmpresaId == empresaId, ct);
        var reversa = new AsientoContable
        {
            EmpresaId = empresaId,
            Numero = $"ASI-{correlativo + 1:000000}",
            // La reversa se registra en el período del original para que la balanza
            // de ese período siempre netee (no existe cierre de períodos).
            Fecha = original.Fecha,
            Concepto = $"Reversa de {original.Numero}" + (string.IsNullOrWhiteSpace(motivo) ? "" : $": {motivo.Trim()}"),
            Origen = OrigenesAsiento.Reversa,
            OrigenId = original.Id,
            ReversaDeId = original.Id,
            EstadoCodigo = AsientoEstados.Activo,
            TotalDebe = original.TotalHaber,
            TotalHaber = original.TotalDebe,
            CreatedBy = actor,
        };
        foreach (var l in original.Lineas)
            reversa.Lineas.Add(new AsientoContableLinea
            {
                CuentaContableId = l.CuentaContableId,
                Debe = l.Haber,   // espejo
                Haber = l.Debe,
                Detalle = l.Detalle,
                CreatedBy = actor,
            });

        original.EstadoCodigo = AsientoEstados.Reversado;
        original.UpdatedAt = DateTime.UtcNow; original.UpdatedBy = actor;
        _db.AsientosContables.Add(reversa);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "REVERSAR_ASIENTO", $"{original.Numero} → {reversa.Numero}", original.Id);
        return await GetAsientoAsync(empresaId, reversa.Id, ct);
    }

    public async Task<Result<BalanzaDto>> BalanzaAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        if (anio is < 2020 or > 2100 || mes is < 1 or > 12)
            return Result<BalanzaDto>.Fail("Período inválido.", "VALIDATION");

        var desde = new DateOnly(anio, mes, 1);
        var hasta = desde.AddMonths(1);

        // Incluye originales y reversas (se netean); la doble partida siempre cuadra.
        var lineas = await _db.AsientoContableLineas.AsNoTracking()
            .Where(l => l.Asiento.EmpresaId == empresaId && l.Asiento.Fecha >= desde && l.Asiento.Fecha < hasta)
            .GroupBy(l => new { l.Cuenta.Codigo, l.Cuenta.Nombre, l.Cuenta.Tipo })
            .Select(g => new { g.Key.Codigo, g.Key.Nombre, g.Key.Tipo, Debe = g.Sum(x => x.Debe), Haber = g.Sum(x => x.Haber) })
            .OrderBy(x => x.Codigo)
            .ToListAsync(ct);

        var cuentas = lineas.Select(x => new BalanzaCuentaDto
        {
            Codigo = x.Codigo, Nombre = x.Nombre, Tipo = x.Tipo,
            Debe = Round2(x.Debe), Haber = Round2(x.Haber),
            SaldoDeudor = x.Debe > x.Haber ? Round2(x.Debe - x.Haber) : 0m,
            SaldoAcreedor = x.Haber > x.Debe ? Round2(x.Haber - x.Debe) : 0m,
        }).ToList();

        var totalDebe = Round2(cuentas.Sum(c => c.Debe));
        var totalHaber = Round2(cuentas.Sum(c => c.Haber));
        return Result<BalanzaDto>.Ok(new BalanzaDto
        {
            Anio = anio, Mes = mes, Cuentas = cuentas,
            TotalDebe = totalDebe, TotalHaber = totalHaber, Cuadrada = totalDebe == totalHaber,
        });
    }

    public async Task<Result<byte[]>> BalanzaCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        var r = await BalanzaAsync(empresaId, anio, mes, ct);
        if (r.IsFailure) return Result<byte[]>.Fail(r.Error!, r.ErrorCode);
        var csv = new CsvExporter("Código", "Cuenta", "Tipo", "Debe", "Haber", "Saldo deudor", "Saldo acreedor");
        foreach (var c in r.Value!.Cuentas)
            csv.AddRow(c.Codigo, c.Nombre, c.Tipo, F(c.Debe), F(c.Haber), F(c.SaldoDeudor), F(c.SaldoAcreedor));
        csv.AddRow("", "TOTAL", "", F(r.Value.TotalDebe), F(r.Value.TotalHaber), "", "");
        return Result<byte[]>.Ok(csv.ToBytes());
    }

    public async Task<Result<byte[]>> AsientosCsvAsync(int empresaId, int anio, int mes, CancellationToken ct = default)
    {
        if (anio is < 2000 or > 2999 || mes is < 1 or > 12)
            return Result<byte[]>.Fail("Período inválido.", "VALIDATION");

        var desde = new DateOnly(anio, mes, 1);
        var hasta = desde.AddMonths(1);

        var asientos = await _db.AsientosContables.AsNoTracking()
            .Include(x => x.Lineas).ThenInclude(l => l.Cuenta)
            .Where(x => x.EmpresaId == empresaId && x.Fecha >= desde && x.Fecha < hasta)
            .OrderBy(x => x.Fecha).ThenBy(x => x.Id)
            .ToListAsync(ct);

        // Una fila por movimiento: es el formato que aceptan los sistemas contables
        // externos, y deja el archivo listo para tabla dinámica en Excel.
        var csv = new CsvExporter(
            "Fecha", "Asiento", "Concepto", "Origen", "Estado",
            "Cuenta", "Nombre cuenta", "Detalle", "Debe", "Haber");

        decimal totalDebe = 0, totalHaber = 0;
        foreach (var a in asientos)
        {
            foreach (var l in a.Lineas.OrderByDescending(l => l.Debe).ThenBy(l => l.Cuenta!.Codigo))
            {
                csv.AddRow(
                    a.Fecha.ToString("yyyy-MM-dd"), a.Numero, a.Concepto, a.Origen, a.EstadoCodigo,
                    l.Cuenta?.Codigo, l.Cuenta?.Nombre, l.Detalle, F(l.Debe), F(l.Haber));
                totalDebe += l.Debe;
                totalHaber += l.Haber;
            }
        }

        csv.AddRow("", "TOTAL", "", "", "", "", "", "", F(totalDebe), F(totalHaber));
        return Result<byte[]>.Ok(csv.ToBytes());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task EnsureCatalogoAsync(int empresaId, CancellationToken ct)
    {
        if (await _db.CuentasContables.AnyAsync(c => c.EmpresaId == empresaId, ct)) return;
        (string codigo, string nombre, string tipo)[] minimas =
        [
            (CuentasContablesMinimas.Efectivo, "Efectivo y equivalentes", TiposCuentaContable.Activo),
            (CuentasContablesMinimas.CuentasPorCobrar, "Cuentas por cobrar", TiposCuentaContable.Activo),
            (CuentasContablesMinimas.IvaCreditoFiscal, "IVA crédito fiscal", TiposCuentaContable.Activo),
            (CuentasContablesMinimas.CuentasPorPagar, "Cuentas por pagar", TiposCuentaContable.Pasivo),
            (CuentasContablesMinimas.IvaDebitoFiscal, "IVA débito fiscal", TiposCuentaContable.Pasivo),
            (CuentasContablesMinimas.Ventas, "Ventas", TiposCuentaContable.Ingreso),
            (CuentasContablesMinimas.Compras, "Compras y costos", TiposCuentaContable.Costo),
            (CuentasContablesMinimas.GastosOperacion, "Gastos de operación", TiposCuentaContable.Gasto),
        ];
        foreach (var (codigo, nombre, tipo) in minimas)
            _db.CuentasContables.Add(new CuentaContable { EmpresaId = empresaId, Codigo = codigo, Nombre = nombre, Tipo = tipo, CreatedBy = "SYSTEM" });
        await _db.SaveChangesAsync(ct);
    }

    private static AsientoDto ToDto(AsientoContable a) => new()
    {
        Id = a.Id, Numero = a.Numero, Fecha = a.Fecha, Concepto = a.Concepto,
        Origen = a.Origen, OrigenId = a.OrigenId, EstadoCodigo = a.EstadoCodigo, ReversaDeId = a.ReversaDeId,
        TotalDebe = a.TotalDebe, TotalHaber = a.TotalHaber,
        Lineas = a.Lineas.OrderByDescending(l => l.Debe).Select(l => new AsientoLineaDto
        {
            CuentaCodigo = l.Cuenta.Codigo, CuentaNombre = l.Cuenta.Nombre,
            Debe = l.Debe, Haber = l.Haber, Detalle = l.Detalle,
        }).ToList(),
    };

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static string F(decimal v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "AsientoContable", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
