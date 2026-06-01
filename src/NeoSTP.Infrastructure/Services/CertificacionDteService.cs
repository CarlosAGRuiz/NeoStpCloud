using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Certificacion;
using NeoSTP.Application.Dte.Certificacion.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class CertificacionDteService : ICertificacionDteService
{
    private const string AuditModule = "CERTIFICACION";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public CertificacionDteService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // -------------------------------------------------------- Lectura

    public async Task<Result<CertificacionResumenDto>> GetResumenAsync(int empresaId, CancellationToken ct = default)
    {
        var progresos = await BuildMatrizProgresoAsync(empresaId, ct);
        var resumen = new CertificacionResumenDto
        {
            Requeridos = progresos.Sum(p => p.Requeridos),
            Completados = progresos.Sum(p => p.Completados),
            EnProgreso = progresos.Sum(p => p.EnProgreso),
            ConError = progresos.Sum(p => p.ConError),
            TotalTipos = progresos.Count,
            TiposCompletados = progresos.Count(p => p.Completo),
        };
        return Result<CertificacionResumenDto>.Ok(resumen);
    }

    public async Task<Result<IReadOnlyList<CertificacionMatrizProgresoDto>>> GetMatrizAsync(int empresaId, CancellationToken ct = default)
    {
        var progresos = await BuildMatrizProgresoAsync(empresaId, ct);
        return Result<IReadOnlyList<CertificacionMatrizProgresoDto>>.Ok(progresos);
    }

    public async Task<Result<IReadOnlyList<CertificacionEscenarioDto>>> GetEscenariosAsync(string tipoDteCodigo, int empresaId, CancellationToken ct = default)
    {
        var codigo = NormalizeTipo(tipoDteCodigo);
        if (codigo is null) return Result<IReadOnlyList<CertificacionEscenarioDto>>.Fail("Tipo DTE requerido.", "VALIDATION");

        var matriz = await _db.CertificacionMatriz.AsNoTracking()
            .FirstOrDefaultAsync(m => m.TipoDteCodigo == codigo && m.Activo, ct);
        if (matriz is null) return Result<IReadOnlyList<CertificacionEscenarioDto>>.Fail($"Matriz {codigo} no encontrada.", "CERT_MATRIZ_NOT_FOUND");

        var escenarios = await _db.CertificacionEscenarios.AsNoTracking()
            .Where(e => e.MatrizId == matriz.Id && e.Activo)
            .OrderBy(e => e.Orden)
            .ToListAsync(ct);

        // Pruebas más recientes por escenario (mayor IntentoNumero) para esta empresa.
        var escenarioIds = escenarios.Select(e => e.Id).ToList();
        var pruebasRecientes = await GetUltimasPruebasAsync(empresaId, escenarioIds, ct);

        var dtos = escenarios.Select(e =>
        {
            pruebasRecientes.TryGetValue(e.Id, out var p);
            return new CertificacionEscenarioDto
            {
                Id = e.Id,
                MatrizId = e.MatrizId,
                Codigo = e.Codigo,
                Nombre = e.Nombre,
                Descripcion = e.Descripcion,
                Orden = e.Orden,
                EstadoActual = p?.EstadoCodigo ?? CertificacionEstadoCodigos.Pendiente,
                PruebaId = p?.Id,
                DteDocumentoId = p?.DteDocumentoId,
                NumeroControl = p?.DteDocumento?.NumeroControl,
                SelloRecibido = p?.SelloRecibido,
                IntentoNumero = p?.IntentoNumero ?? 0,
                UltimoIntentoAt = p?.CreatedAt,
            };
        }).ToList();

        return Result<IReadOnlyList<CertificacionEscenarioDto>>.Ok(dtos);
    }

    public async Task<Result<IReadOnlyList<CertificacionErrorDto>>> GetErroresAsync(int empresaId, string? codigoMh = null, CancellationToken ct = default)
    {
        var q = _db.CertificacionErrores.AsNoTracking()
            .Include(e => e.Prueba).ThenInclude(p => p.Escenario)
            .Include(e => e.Prueba).ThenInclude(p => p.DteDocumento)
            .Where(e => e.Prueba.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(codigoMh))
        {
            var c = codigoMh.Trim();
            q = q.Where(e => e.CodigoMh == c);
        }

        var errores = await q
            .OrderByDescending(e => e.OcurrioAt)
            .Take(500)
            .Select(e => new CertificacionErrorDto
            {
                Id = e.Id,
                PruebaId = e.PruebaId,
                CodigoMh = e.CodigoMh,
                Descripcion = e.Descripcion,
                RespuestaMhJson = e.RespuestaMhJson,
                OcurrioAt = e.OcurrioAt,
                EscenarioCodigo = e.Prueba.Escenario.Codigo,
                DteDocumentoId = e.Prueba.DteDocumentoId,
                NumeroControl = e.Prueba.DteDocumento != null ? e.Prueba.DteDocumento.NumeroControl : null,
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<CertificacionErrorDto>>.Ok(errores);
    }

    // -------------------------------------------------------- Mutación

    public async Task<Result<CertificacionPruebaDto>> GenerarPruebaAsync(string tipoDteCodigo, int empresaId, string? actor, CancellationToken ct = default)
    {
        var codigo = NormalizeTipo(tipoDteCodigo);
        if (codigo is null) return Result<CertificacionPruebaDto>.Fail("Tipo DTE requerido.", "VALIDATION");

        var matriz = await _db.CertificacionMatriz.FirstOrDefaultAsync(m => m.TipoDteCodigo == codigo && m.Activo, ct);
        if (matriz is null) return Result<CertificacionPruebaDto>.Fail($"Matriz {codigo} no encontrada.", "CERT_MATRIZ_NOT_FOUND");

        var escenariosDeTipo = await _db.CertificacionEscenarios.AsNoTracking()
            .Where(e => e.MatrizId == matriz.Id && e.Activo)
            .OrderBy(e => e.Orden)
            .Select(e => new { e.Id, e.Codigo, e.Nombre })
            .ToListAsync(ct);
        if (escenariosDeTipo.Count == 0) return Result<CertificacionPruebaDto>.Fail("La matriz no tiene escenarios activos.", "CERT_NO_ESCENARIOS");

        var ids = escenariosDeTipo.Select(e => e.Id).ToList();
        var pruebasRecientes = await GetUltimasPruebasAsync(empresaId, ids, ct);

        // Primer escenario sin prueba o cuya última prueba esté ERROR/PENDIENTE.
        var siguiente = escenariosDeTipo.FirstOrDefault(e =>
        {
            if (!pruebasRecientes.TryGetValue(e.Id, out var p)) return true;
            return p.EstadoCodigo == CertificacionEstadoCodigos.Error
                   || p.EstadoCodigo == CertificacionEstadoCodigos.Pendiente;
        });

        if (siguiente is null)
        {
            return Result<CertificacionPruebaDto>.Fail("Todos los escenarios del tipo ya están EN_PROGRESO o COMPLETADOS.", "CERT_NADA_PENDIENTE");
        }

        // Si hay una prueba ERROR previa, abrimos un nuevo intento; si no, intento 1.
        var intento = 1;
        if (pruebasRecientes.TryGetValue(siguiente.Id, out var ultima))
        {
            intento = ultima.IntentoNumero + 1;
        }

        var prueba = new CertificacionPrueba
        {
            EmpresaId = empresaId,
            EscenarioId = siguiente.Id,
            EstadoCodigo = CertificacionEstadoCodigos.EnProgreso,
            IntentoNumero = intento,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        _db.CertificacionPruebas.Add(prueba);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "GENERAR_PRUEBA",
            $"Prueba abierta para {siguiente.Codigo} (intento {intento})", "CertificacionPrueba", prueba.Id);

        return Result<CertificacionPruebaDto>.Ok(MapPrueba(prueba, siguiente.Codigo, siguiente.Nombre, null));
    }

    public async Task<Result<CertificacionPruebaDto>> MarcarCompletadoAsync(int documentoId, MarcarCompletadoRequest request, int empresaId, string? actor, CancellationToken ct = default)
    {
        if (request.EscenarioId <= 0)
        {
            return Result<CertificacionPruebaDto>.Fail("EscenarioId es obligatorio.", "VALIDATION");
        }

        var doc = await _db.DteDocumentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentoId && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<CertificacionPruebaDto>.Fail("Documento DTE no encontrado.", "DTE_NOT_FOUND");

        var escenario = await _db.CertificacionEscenarios
            .Include(e => e.Matriz)
            .FirstOrDefaultAsync(e => e.Id == request.EscenarioId && e.Activo, ct);
        if (escenario is null) return Result<CertificacionPruebaDto>.Fail("Escenario no encontrado.", "CERT_ESCENARIO_NOT_FOUND");

        // Validar consistencia tipo DTE ↔ matriz (excepto eventos, que no tienen TipoDteCodigo de DTE)
        if (IsDteTipo(escenario.Matriz.TipoDteCodigo) && doc.TipoDteCodigo != escenario.Matriz.TipoDteCodigo)
        {
            return Result<CertificacionPruebaDto>.Fail(
                $"El DTE tipo {doc.TipoDteCodigo} no corresponde a la matriz {escenario.Matriz.TipoDteCodigo}.",
                "CERT_TIPO_MISMATCH");
        }

        // Reutiliza la última prueba abierta del escenario si existe; si no, crea una nueva.
        var ultima = await _db.CertificacionPruebas
            .Where(p => p.EmpresaId == empresaId && p.EscenarioId == escenario.Id)
            .OrderByDescending(p => p.IntentoNumero)
            .FirstOrDefaultAsync(ct);

        CertificacionPrueba prueba;
        if (ultima is not null && ultima.EstadoCodigo != CertificacionEstadoCodigos.Completado)
        {
            prueba = ultima;
            prueba.DteDocumentoId = doc.Id;
            prueba.Notas = request.Notas ?? prueba.Notas;
        }
        else
        {
            var nuevoIntento = (ultima?.IntentoNumero ?? 0) + 1;
            prueba = new CertificacionPrueba
            {
                EmpresaId = empresaId,
                EscenarioId = escenario.Id,
                DteDocumentoId = doc.Id,
                IntentoNumero = nuevoIntento,
                Notas = request.Notas,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actor,
            };
            _db.CertificacionPruebas.Add(prueba);
        }

        // Si el DTE ya tiene sello (PROCESADO), marcamos completado; si no, EN_PROGRESO.
        var doctorState = doc.EstadoCodigo;
        if (!string.IsNullOrWhiteSpace(doc.SelloRecibido) && doctorState == DteEstadoCodigos.Procesado)
        {
            prueba.EstadoCodigo = CertificacionEstadoCodigos.Completado;
            prueba.SelloRecibido = doc.SelloRecibido;
            prueba.ProcesadoAt = doc.ProcesadoAt ?? DateTime.UtcNow;
        }
        else if (doctorState == DteEstadoCodigos.Rechazado || doctorState == DteEstadoCodigos.Error)
        {
            prueba.EstadoCodigo = CertificacionEstadoCodigos.Error;
        }
        else
        {
            prueba.EstadoCodigo = CertificacionEstadoCodigos.EnProgreso;
        }
        prueba.UpdatedAt = DateTime.UtcNow;
        prueba.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "MARCAR_COMPLETADO",
            $"DTE #{doc.Id} ({doc.NumeroControl}) → escenario {escenario.Codigo}; estado {prueba.EstadoCodigo}",
            "CertificacionPrueba", prueba.Id);

        return Result<CertificacionPruebaDto>.Ok(MapPrueba(prueba, escenario.Codigo, escenario.Nombre, doc.NumeroControl));
    }

    public async Task<Result<CertificacionPruebaDto>> MarcarCompletadoPorEventoAsync(int eventoId, MarcarCompletadoRequest request, int empresaId, string? actor, CancellationToken ct = default)
    {
        if (request.EscenarioId <= 0)
        {
            return Result<CertificacionPruebaDto>.Fail("EscenarioId es obligatorio.", "VALIDATION");
        }

        var evento = await _db.DteEventos.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventoId && e.EmpresaId == empresaId, ct);
        if (evento is null) return Result<CertificacionPruebaDto>.Fail("Evento no encontrado.", "EVENTO_NOT_FOUND");

        var escenario = await _db.CertificacionEscenarios
            .Include(e => e.Matriz)
            .FirstOrDefaultAsync(e => e.Id == request.EscenarioId && e.Activo, ct);
        if (escenario is null) return Result<CertificacionPruebaDto>.Fail("Escenario no encontrado.", "CERT_ESCENARIO_NOT_FOUND");

        // El tipo de la matriz debe ser un evento (no un código DTE 01/03/...).
        if (IsDteTipo(escenario.Matriz.TipoDteCodigo) || escenario.Matriz.TipoDteCodigo != evento.TipoEventoCodigo)
        {
            return Result<CertificacionPruebaDto>.Fail(
                $"El evento {evento.TipoEventoCodigo} no corresponde a la matriz {escenario.Matriz.TipoDteCodigo}.",
                "CERT_TIPO_MISMATCH");
        }

        var ultima = await _db.CertificacionPruebas
            .Where(p => p.EmpresaId == empresaId && p.EscenarioId == escenario.Id)
            .OrderByDescending(p => p.IntentoNumero)
            .FirstOrDefaultAsync(ct);

        CertificacionPrueba prueba;
        if (ultima is not null && ultima.EstadoCodigo != CertificacionEstadoCodigos.Completado)
        {
            prueba = ultima;
            prueba.EventoId = evento.Id;
            prueba.DteDocumentoId = null;
            prueba.Notas = request.Notas ?? prueba.Notas;
        }
        else
        {
            var nuevoIntento = (ultima?.IntentoNumero ?? 0) + 1;
            prueba = new CertificacionPrueba
            {
                EmpresaId = empresaId,
                EscenarioId = escenario.Id,
                EventoId = evento.Id,
                IntentoNumero = nuevoIntento,
                Notas = request.Notas,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actor,
            };
            _db.CertificacionPruebas.Add(prueba);
        }

        if (!string.IsNullOrWhiteSpace(evento.SelloRecibido) && evento.EstadoCodigo == DteEventoEstadoCodigos.Procesado)
        {
            prueba.EstadoCodigo = CertificacionEstadoCodigos.Completado;
            prueba.SelloRecibido = evento.SelloRecibido;
            prueba.ProcesadoAt = evento.FinalizadoAt ?? DateTime.UtcNow;
        }
        else if (evento.EstadoCodigo is DteEventoEstadoCodigos.Rechazado or DteEventoEstadoCodigos.Error)
        {
            prueba.EstadoCodigo = CertificacionEstadoCodigos.Error;
        }
        else
        {
            prueba.EstadoCodigo = CertificacionEstadoCodigos.EnProgreso;
        }
        prueba.UpdatedAt = DateTime.UtcNow;
        prueba.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "MARCAR_COMPLETADO_EVENTO",
            $"Evento #{evento.Id} ({evento.TipoEventoCodigo}) → escenario {escenario.Codigo}; estado {prueba.EstadoCodigo}",
            "CertificacionPrueba", prueba.Id);

        return Result<CertificacionPruebaDto>.Ok(MapPrueba(prueba, escenario.Codigo, escenario.Nombre, evento.CodigoGeneracion));
    }

    public async Task<Result<CertificacionPruebaDto>> ReintentarAsync(int documentoId, int empresaId, string? actor, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentoId && d.EmpresaId == empresaId, ct);
        if (doc is null) return Result<CertificacionPruebaDto>.Fail("Documento DTE no encontrado.", "DTE_NOT_FOUND");

        var prueba = await _db.CertificacionPruebas
            .Include(p => p.Escenario)
            .Where(p => p.EmpresaId == empresaId && p.DteDocumentoId == documentoId)
            .OrderByDescending(p => p.IntentoNumero)
            .FirstOrDefaultAsync(ct);
        if (prueba is null)
        {
            return Result<CertificacionPruebaDto>.Fail("El DTE no está asociado a ninguna prueba.", "CERT_PRUEBA_NOT_FOUND");
        }

        // La prueba pasa a ERROR si no estaba ya completada.
        if (prueba.EstadoCodigo != CertificacionEstadoCodigos.Completado)
        {
            prueba.EstadoCodigo = CertificacionEstadoCodigos.Error;
            prueba.UpdatedAt = DateTime.UtcNow;
            prueba.UpdatedBy = actor;
        }

        // Abrimos un nuevo intento sin DTE asociado, listo para que el usuario emita otro.
        var nuevo = new CertificacionPrueba
        {
            EmpresaId = empresaId,
            EscenarioId = prueba.EscenarioId,
            EstadoCodigo = CertificacionEstadoCodigos.Pendiente,
            IntentoNumero = prueba.IntentoNumero + 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        _db.CertificacionPruebas.Add(nuevo);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "REINTENTAR",
            $"DTE #{doc.Id} → escenario {prueba.Escenario.Codigo}; intento {nuevo.IntentoNumero} abierto",
            "CertificacionPrueba", nuevo.Id);

        return Result<CertificacionPruebaDto>.Ok(MapPrueba(nuevo, prueba.Escenario.Codigo, prueba.Escenario.Nombre, null));
    }

    // -------------------------------------------------------- Helpers

    private async Task<IReadOnlyList<CertificacionMatrizProgresoDto>> BuildMatrizProgresoAsync(int empresaId, CancellationToken ct)
    {
        var matrices = await _db.CertificacionMatriz.AsNoTracking()
            .Where(m => m.Activo)
            .OrderBy(m => m.Orden)
            .ToListAsync(ct);

        // Estado actual por escenario (mayor IntentoNumero) para esta empresa.
        var estadoActualPorEscenario = await _db.CertificacionPruebas.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId)
            .GroupBy(p => p.EscenarioId)
            .Select(g => g
                .OrderByDescending(p => p.IntentoNumero)
                .Select(p => new { p.EscenarioId, p.EstadoCodigo })
                .First())
            .ToListAsync(ct);
        var lookup = estadoActualPorEscenario.ToDictionary(x => x.EscenarioId, x => x.EstadoCodigo);

        // Para cada matriz, contar escenarios por estado actual.
        var escenariosPorMatriz = await _db.CertificacionEscenarios.AsNoTracking()
            .Where(e => e.Activo)
            .Select(e => new { e.Id, e.MatrizId })
            .ToListAsync(ct);

        var dtos = new List<CertificacionMatrizProgresoDto>(matrices.Count);
        foreach (var m in matrices)
        {
            var escenariosTipo = escenariosPorMatriz.Where(x => x.MatrizId == m.Id).ToList();
            var requeridos = m.EscenariosRequeridos;
            var completados = 0;
            var enProgreso = 0;
            var conError = 0;

            foreach (var e in escenariosTipo)
            {
                if (!lookup.TryGetValue(e.Id, out var estado))
                {
                    continue; // pendiente (sin prueba)
                }
                switch (estado)
                {
                    case CertificacionEstadoCodigos.Completado: completados++; break;
                    case CertificacionEstadoCodigos.EnProgreso: enProgreso++; break;
                    case CertificacionEstadoCodigos.Error: conError++; break;
                }
            }

            dtos.Add(new CertificacionMatrizProgresoDto
            {
                MatrizId = m.Id,
                TipoDteCodigo = m.TipoDteCodigo,
                Nombre = m.Nombre,
                Descripcion = m.Descripcion,
                Orden = m.Orden,
                Requeridos = requeridos,
                Completados = completados,
                EnProgreso = enProgreso,
                ConError = conError,
            });
        }

        return dtos;
    }

    private async Task<Dictionary<int, CertificacionPrueba>> GetUltimasPruebasAsync(int empresaId, IReadOnlyList<int> escenarioIds, CancellationToken ct)
    {
        if (escenarioIds.Count == 0) return new Dictionary<int, CertificacionPrueba>();

        var pruebas = await _db.CertificacionPruebas
            .Include(p => p.DteDocumento)
            .Where(p => p.EmpresaId == empresaId && escenarioIds.Contains(p.EscenarioId))
            .AsNoTracking()
            .ToListAsync(ct);

        return pruebas
            .GroupBy(p => p.EscenarioId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.IntentoNumero).First());
    }

    private static string? NormalizeTipo(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToUpperInvariant();

    /// <summary>True si el código corresponde a un DTE numérico (no a un evento).</summary>
    private static bool IsDteTipo(string codigo)
        => codigo is "01" or "03" or "04" or "05" or "06" or "07" or "08" or "09" or "11" or "14" or "15";

    private static CertificacionPruebaDto MapPrueba(CertificacionPrueba p, string escenarioCodigo, string escenarioNombre, string? numeroControl) => new()
    {
        Id = p.Id,
        EmpresaId = p.EmpresaId,
        EscenarioId = p.EscenarioId,
        EscenarioCodigo = escenarioCodigo,
        EscenarioNombre = escenarioNombre,
        DteDocumentoId = p.DteDocumentoId,
        NumeroControl = numeroControl,
        EstadoCodigo = p.EstadoCodigo,
        SelloRecibido = p.SelloRecibido,
        IntentoNumero = p.IntentoNumero,
        ProcesadoAt = p.ProcesadoAt,
        Notas = p.Notas,
        CreatedAt = p.CreatedAt,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, string entidad, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId.ToString(),
            Resultado = "OK",
            Detalle = detalle,
        });
}
