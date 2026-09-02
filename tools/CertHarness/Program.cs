using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Application.Dte.Certificacion;
using NeoSTP.Application.Dte.Certificacion.Dtos;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Persistence;
using NJsonSchema;

// ============================================================================
// CertHarness — arnés de consola en proceso para certificación DTE de NEO
// contra apitest de Hacienda. NO forma parte de la solución ni se despliega.
//
// Comparte el llavero de DataProtection (SetApplicationName "NeoSTP.Cloud",
// por-usuario) con la API; así descifra la clave MH cifrada en Dte_Configuracion.
// Debe correr como el MISMO usuario Windows que cargó el certificado.
//
// Uso:  dotnet run --project tools/CertHarness -- <comando>
//   diag                 Estado real: empresa 2, cert, DTE por tipo/estado, matriz.
//   emit <tipo>          Emite UN DTE del tipo (clonando el último PROCESADO) a apitest.
//   verify-all           Emite uno de cada tipo + invalidación + contingencia; reporta.
//   fill <tipo> <n>      Emite y liga a la matriz hasta n escenarios pendientes del tipo.
// ============================================================================

const int EmpresaId = 2;
const string Actor = "cert-harness";

var comando = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "diag";

var apiDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "NeoSTP.Api"));
if (!File.Exists(Path.Combine(apiDir, "appsettings.json")))
    apiDir = Environment.GetEnvironmentVariable("NEOSTP_API_DIR") ?? apiDir;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = apiDir,
    EnvironmentName = "Development",
    Args = args,
});
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: false, reloadOnChange: false);
// Los comandos de validación generan con el esquema NUEVO (v2/v4) para contrastar contra los
// esquemas oficiales; emit/verify-all quedan en el esquema vigente que apitest acepta hoy.
if (comando.StartsWith("validate"))
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Dte:EsquemaNuevo"] = "true" });
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var sp = scope.ServiceProvider;

Console.WriteLine($"== CertHarness :: '{comando}' :: Hacienda:Client={builder.Configuration["Hacienda:Client"]} Signer={builder.Configuration["Dte:Signer"]}");

// Tipos de DTE en orden de la matriz.
string[] tiposDoc = ["01", "03", "04", "05", "06", "07", "08", "09", "11", "14", "15"];

switch (comando)
{
    case "diag": await DiagAsync(sp); break;
    case "emit":
        {
            var tipo = args.Length > 1 ? args[1] : "01";
            var (ok, msg, _) = await EmitirTipoAsync(sp, tipo);
            Console.WriteLine($"   {tipo}: {(ok ? "OK" : "FAIL")} — {msg}");
            break;
        }
    case "verify-all": await VerifyAllAsync(sp, tiposDoc); break;
    case "retorno":
        {
            var dbRetorno = sp.GetRequiredService<NeoStpDbContext>();
            var documentoId = args.Length > 1 && int.TryParse(args[1], out var explicitId)
                ? explicitId
                : await dbRetorno.DteDocumentos.AsNoTracking()
                    .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "01"
                             && d.EstadoCodigo == DteEstadoCodigos.Procesado)
                    .OrderByDescending(d => d.Id)
                    .Select(d => d.Id)
                    .FirstOrDefaultAsync();
            var servicioRetorno = sp.GetRequiredService<IDteDocumentosService>();
            var resultado = await servicioRetorno.TransmitirEventoRetornoAsync(EmpresaId, documentoId, Actor);
            Console.WriteLine($"   retorno DTE #{documentoId}: {(resultado.IsSuccess ? $"OK — {resultado.Value!.SelloOEstado}" : $"FALLO [{resultado.ErrorCode}] {resultado.Error}")}");
            break;
        }
    case "validate":
        {
            var tipo = args.Length > 1 ? args[1] : "01";
            await ValidateTipoAsync(sp, apiDir, tipo);
            break;
        }
    case "validate-all":
        foreach (var t in tiposDoc) await ValidateTipoAsync(sp, apiDir, t);
        break;
    case "contingencia-e2e": await ContingenciaE2eAsync(sp); break;
    case "fill-eventos": await FillEventosAsync(sp); break;
    case "fill-retornos": await FillRetornosAsync(sp); break;
    case "verify-fix-nc":
        {
            var db3 = sp.GetRequiredService<NeoStpDbContext>();
            var em3 = sp.GetRequiredService<IConnectDteService>();
            // CCF limpio MÁS VIEJO (de un día anterior) para probar el fix de fecha del relacionado.
            var oldCcf = await db3.DteDocumentos.AsNoTracking()
                .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "03"
                         && d.EstadoCodigo == DteEstadoCodigos.Procesado && d.VentaTerceroNit == null)
                .OrderBy(d => d.Id).FirstOrDefaultAsync();
            var ncTpl = await db3.DteDocumentos.AsNoTracking().Include(d => d.Detalles)
                .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "05" && d.EstadoCodigo == DteEstadoCodigos.Procesado)
                .OrderByDescending(d => d.Id).FirstOrDefaultAsync();
            if (oldCcf is null || ncTpl is null) { Console.WriteLine("   faltan plantillas"); break; }
            var req3 = ClonarRequest(ncTpl);
            req3.VentaTerceroNit = null; req3.VentaTerceroNombre = null;
            req3.DocumentoRelacionadoId = oldCcf.Id;
            req3.NumeroDocumentoRelacionado = oldCcf.CodigoGeneracion;
            req3.TipoDteRelacionado = "03"; req3.TipoGeneracionRelacionado = "2";
            Console.WriteLine($"   CCF relacionado #{oldCcf.Id} fecha={oldCcf.FechaEmision:yyyy-MM-dd} (hoy={DateTime.Now:yyyy-MM-dd})");
            var r3 = await em3.EmitirAsync(EmpresaId, req3, Actor);
            Console.WriteLine($"   NC vs CCF viejo: {(r3.Value?.EstadoCodigo == DteEstadoCodigos.Procesado ? "✔ PROCESADO (fix OK)" : $"✘ {r3.Value?.EstadoCodigo} {Short(r3.Value?.RespuestaHacienda, 200)}")}");
            break;
        }
    case "dump-dte":
        {
            var td = args.Length > 1 ? args[1] : "11";
            var db4 = sp.GetRequiredService<NeoStpDbContext>();
            var gen4 = sp.GetRequiredService<IDteGeneratorService>();
            var doc4 = await db4.DteDocumentos.AsNoTracking().Include(d => d.Empresa).Include(d => d.Detalles)
                .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == td && d.EstadoCodigo == DteEstadoCodigos.Procesado)
                .OrderByDescending(d => d.Id).FirstOrDefaultAsync();
            var cfg4 = await db4.DteConfiguracion.AsNoTracking().FirstOrDefaultAsync(c => c.EmpresaId == EmpresaId);
            var g4 = gen4.Generar(doc4!, cfg4);
            var f4 = Path.Combine(Path.GetTempPath(), $"dte-{td}.json");
            await File.WriteAllTextAsync(f4, g4.Value ?? g4.Error);
            Console.WriteLine($"   {td}: receptorPaisCodigo(db)='{doc4?.ReceptorPaisCodigo}' → json {f4}");
            break;
        }
    case "dump-evento":
        {
            var te = args.Length > 1 ? args[1] : "RETORNO";
            var db2 = sp.GetRequiredService<NeoStpDbContext>();
            var ev = await db2.DteEventos.AsNoTracking().Include(e => e.Json)
                .Where(e => e.EmpresaId == EmpresaId && e.TipoEventoCodigo == te)
                .OrderByDescending(e => e.Id).FirstOrDefaultAsync();
            var outFile = Path.Combine(Path.GetTempPath(), $"evento-{te}.json");
            await File.WriteAllTextAsync(outFile, ev?.Json?.JsonSinFirmar ?? "(sin json)");
            Console.WriteLine($"   {te}: json → {outFile}");
            break;
        }
    case "set-plan":
        {
            var planId = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 204;
            var emp = sp.GetRequiredService<IEmpresasService>();
            var r = await emp.AsignarPlanAsync(EmpresaId, new NeoSTP.Application.Empresas.Dtos.AsignarPlanRequest { PlanId = planId }, Actor);
            Console.WriteLine($"   set-plan {planId}: {(r.IsSuccess ? "OK" : $"FALLO [{r.ErrorCode}] {r.Error}")}");
            break;
        }
    case "fill":
        {
            var tipo = args.Length > 1 ? args[1] : "01";
            var n = args.Length > 2 && int.TryParse(args[2], out var x) ? x : 5;
            await FillAsync(sp, tipo, n);
            break;
        }
    default: Console.WriteLine($"Comando desconocido: {comando}"); break;
}

return;

// ---------------------------------------------------------------------------

static async Task DiagAsync(IServiceProvider sp)
{
    var db = sp.GetRequiredService<NeoStpDbContext>();
    var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == EmpresaId);
    Console.WriteLine($"\n-- Empresa {EmpresaId}: {(empresa is null ? "NO ENCONTRADA" : $"{empresa.Nit} | {empresa.RazonSocial} | {empresa.EstadoCodigo}")}");
    var cfg = await db.DteConfiguracion.AsNoTracking().FirstOrDefaultAsync(c => c.EmpresaId == EmpresaId);
    Console.WriteLine($"-- Config DTE: ambiente={cfg?.AmbienteCodigo ?? "n/a"}  certBytes={cfg?.CertificadoBlob?.Length ?? 0}");

    var cert = sp.GetRequiredService<ICertificacionDteService>();
    var matriz = await cert.GetMatrizAsync(EmpresaId);
    Console.WriteLine("\n-- Matriz de certificación:");
    if (matriz.IsSuccess && matriz.Value is not null)
    {
        foreach (var m in matriz.Value.OrderBy(x => x.Orden))
            Console.WriteLine($"   [{m.TipoDteCodigo,-14}] {m.Nombre,-36} req={m.Requeridos,3} ok={m.Completados,3} prog={m.EnProgreso,3} err={m.ConError,3}");
        var r = (await cert.GetResumenAsync(EmpresaId)).Value!;
        Console.WriteLine($"\n   TOTAL: req={r.Requeridos} ok={r.Completados} tiposCompletos={r.TiposCompletados}/{r.TotalTipos}");
    }
}

static async Task VerifyAllAsync(IServiceProvider sp, string[] tipos)
{
    Console.WriteLine("\n== VERIFY-ALL: emite uno de cada tipo con el código actual ==");
    foreach (var tipo in tipos)
    {
        var (ok, msg, _) = await EmitirTipoAsync(sp, tipo);
        Console.WriteLine($"   {tipo}: {(ok ? "✔ PROCESADO" : "✘ FAIL")} — {msg}");
    }

    Console.WriteLine("\n== EVENTOS requeridos por MH ==");
    await VerifyInvalidacionAsync(sp);
    await VerifyContingenciaAsync(sp);
}

/// <summary>Clona el último DTE PROCESADO del tipo y lo re-emite a apitest.</summary>
static async Task<(bool ok, string msg, int? docId)> EmitirTipoAsync(IServiceProvider sp, string tipo)
{
    var db = sp.GetRequiredService<NeoStpDbContext>();
    var emisor = sp.GetRequiredService<IConnectDteService>();

    var plantilla = await db.DteDocumentos.AsNoTracking().Include(d => d.Detalles)
        .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == tipo && d.EstadoCodigo == DteEstadoCodigos.Procesado)
        .OrderByDescending(d => d.Id)
        .FirstOrDefaultAsync();
    if (plantilla is null)
        return (false, "sin plantilla PROCESADO para clonar", null);

    var req = ClonarRequest(plantilla);
    var unico = DateTime.UtcNow.Ticks.ToString()[^10..];

    // NC/ND: emiten un CCF limpio (sin ventaTercero) FRESCO y lo referencian. Así el CCF es de
    // HOY y su fecha coincide con la que el generador pone en documentoRelacionado (usa la fecha
    // de la nota); un CCF viejo daría "[documentoRelacionado.fechaEmision] FECHA NO ES CORRECTA".
    if (tipo is "05" or "06")
    {
        req.VentaTerceroNit = null;
        req.VentaTerceroNombre = null;
        var ccfPlantilla = await db.DteDocumentos.AsNoTracking().Include(d => d.Detalles)
            .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "03" && d.EstadoCodigo == DteEstadoCodigos.Procesado)
            .OrderByDescending(d => d.Id).FirstOrDefaultAsync();
        if (ccfPlantilla is not null)
        {
            var ccfReq = ClonarRequest(ccfPlantilla);
            ccfReq.VentaTerceroNit = null;
            ccfReq.VentaTerceroNombre = null;
            var ccfRes = await emisor.EmitirAsync(EmpresaId, ccfReq, Actor);
            if (ccfRes.IsSuccess && ccfRes.Value is { } ccf && ccf.EstadoCodigo == DteEstadoCodigos.Procesado)
            {
                req.DocumentoRelacionadoId = ccf.Id;
                req.NumeroDocumentoRelacionado = ccf.CodigoGeneracion;
                req.TipoDteRelacionado = "03";
                req.TipoGeneracionRelacionado = "2";
            }
        }
    }

    // 07/08: el número del documento relacionado (físico) debe ser único por documento.
    if (tipo is "07" or "08")
    {
        foreach (var l in req.Lineas)
        {
            l.DocRelacionadoNumero = $"CERT{unico}";
            l.Codigo = $"CERT{unico}";
        }
    }

    var res = await emisor.EmitirAsync(EmpresaId, req, Actor);
    if (res.IsSuccess && res.Value is { } d)
    {
        var procesado = d.EstadoCodigo == DteEstadoCodigos.Procesado;
        var extra = procesado ? "" : " " + Short(d.RespuestaHacienda, 500);
        return (procesado, $"{d.EstadoCodigo} sello={Short(d.SelloRecibido)} nc={d.NumeroControl}{extra}", d.Id);
    }
    return (false, $"[{res.ErrorCode}] {Short(res.Error, 300)}", null);
}

static CreateDteDocumentoRequest ClonarRequest(DteDocumento p)
{
    var req = new CreateDteDocumentoRequest
    {
        TipoDteCodigo = p.TipoDteCodigo,
        CondicionOperacionCodigo = p.CondicionOperacionCodigo,
        FormaPagoCodigo = p.FormaPagoCodigo,
        PlazoDias = p.PlazoDias,
        TipoMonedaCodigo = p.TipoMonedaCodigo,
        Observaciones = p.Observaciones,
        VentaTerceroNit = p.VentaTerceroNit,
        VentaTerceroNombre = p.VentaTerceroNombre,
        ReceptorPaisCodigo = p.ReceptorPaisCodigo,
        ReceptorPaisNombre = p.ReceptorPaisNombre,
        ReceptorTipoPersona = p.ReceptorTipoPersona,
        ReceptorManual = new ReceptorDto
        {
            TipoDocumento = p.ReceptorTipoDocumento,
            NumeroDocumento = p.ReceptorNumeroDocumento,
            Nrc = p.ReceptorNrc,
            Nombre = p.ReceptorNombre,
            TipoContribuyente = p.ReceptorTipoContribuyente,
            CodigoActividad = p.ReceptorCodigoActividad,
            ActividadEconomica = p.ReceptorActividadEconomica,
            DepartamentoCodigo = p.ReceptorDepartamentoCodigo,
            MunicipioCodigo = p.ReceptorMunicipioCodigo,
            DistritoCodigo = p.ReceptorDistritoCodigo,
            Direccion = p.ReceptorDireccion,
            Correo = p.ReceptorCorreo,
            Telefono = p.ReceptorTelefono,
            PaisCodigo = p.ReceptorPaisCodigo,
            PaisNombre = p.ReceptorPaisNombre,
            TipoPersona = p.ReceptorTipoPersona,
        },
    };

    // 09 DCL: clonar el bloque de liquidación del corte.
    if (p.TipoDteCodigo == "09")
    {
        req.Liquidacion = new LiquidacionDto
        {
            PeriodoInicio = p.LiquidacionPeriodoInicio,
            PeriodoFin = p.LiquidacionPeriodoFin,
            Codigo = p.LiquidacionCodigo,
            CantidadDocumentos = p.LiquidacionCantidadDocumentos,
            MontoSinPercepcion = p.LiquidacionMontoSinPercepcion,
            DescripcionSinPercepcion = p.LiquidacionDescripcionSinPercepcion,
            PorcentajeComision = p.LiquidacionPorcentajeComision,
            NombreEntrega = p.LiquidacionNombreEntrega,
            DocumentoEntrega = p.LiquidacionDocumentoEntrega,
            CodigoEmpleado = p.LiquidacionCodigoEmpleado,
        };
    }

    foreach (var det in p.Detalles.OrderBy(x => x.NumeroLinea))
    {
        var clasif = det.VentaExenta > 0 ? "EXENTA"
                   : det.VentaNoSujeta > 0 ? "NO_SUJETA"
                   : "GRAVADA";
        req.Lineas.Add(new CreateDteDocumentoLineaRequest
        {
            Codigo = det.Codigo,
            Descripcion = det.Descripcion,
            UnidadMedidaCodigo = det.UnidadMedidaCodigo,
            TipoItem = det.TipoItem,
            Cantidad = det.Cantidad <= 0 ? 1 : det.Cantidad,
            PrecioUnitario = det.PrecioUnitario,
            MontoDescuento = det.MontoDescuento,
            Clasificacion = clasif,
            NoGravado = det.NoGravado,
            Observaciones = det.Observaciones,
            DocRelacionadoTipoDte = det.DocRelacionadoTipoDte,
            DocRelacionadoNumero = det.Codigo, // en 07/08 el número va en Codigo
            DocRelacionadoFecha = det.DocRelacionadoFecha,
            RetencionCodigoMH = det.RetencionCodigoMH,
        });
    }
    return req;
}

static async Task VerifyInvalidacionAsync(IServiceProvider sp)
{
    var db = sp.GetRequiredService<NeoStpDbContext>();
    var svc = sp.GetRequiredService<IDteDocumentosService>();

    // Necesita un DTE PROCESADO con sello para anular. Usamos el último 01.
    var doc = await db.DteDocumentos.AsNoTracking()
        .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "01"
                 && d.EstadoCodigo == DteEstadoCodigos.Procesado && d.SelloRecibido != null)
        .OrderByDescending(d => d.Id).FirstOrDefaultAsync();
    if (doc is null) { Console.WriteLine("   INVALIDACION: sin DTE PROCESADO para anular"); return; }

    // tipoAnulacion 2 = rescindir (no requiere documento de reemplazo).
    var res = await svc.TransmitirInvalidacionEventoAsync(
        EmpresaId, doc.Id, tipoAnulacion: 2, motivoAnulacion: "Prueba de certificación de invalidación",
        codigoGeneracionReemplazo: null,
        nombreResponsable: "Carlos Antonio Garcia", tipoDocResponsable: "13", numDocResponsable: "000000000",
        actor: Actor);
    Console.WriteLine($"   INVALIDACION (DTE #{doc.Id}): {(res.IsSuccess ? $"✔ {res.Value!.SelloOEstado}" : $"✘ [{res.ErrorCode}] {Short(res.Error, 300)}")}");
}

static async Task VerifyContingenciaAsync(IServiceProvider sp)
{
    var db = sp.GetRequiredService<NeoStpDbContext>();
    var svc = sp.GetRequiredService<IDteDocumentosService>();

    var doc = await db.DteDocumentos.AsNoTracking()
        .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "01" && d.EstadoCodigo == DteEstadoCodigos.Procesado)
        .OrderByDescending(d => d.Id).FirstOrDefaultAsync();
    if (doc is null) { Console.WriteLine("   CONTINGENCIA: sin DTE para referenciar"); return; }

    // tipoContingencia 5 = otro motivo (requiere motivo libre).
    var res = await svc.TransmitirEventoContingenciaAsync(
        EmpresaId, new[] { doc.Id }, tipoContingencia: 5, motivo: "Prueba de certificación de contingencia",
        nombreResponsable: "Carlos Antonio Garcia", tipoDocResponsable: "13", numeroDocResponsable: "000000000",
        actor: Actor);
    Console.WriteLine($"   CONTINGENCIA (DTE #{doc.Id}): {(res.IsSuccess ? $"✔ {res.Value!.SelloOEstado}" : $"✘ [{res.ErrorCode}] {Short(res.Error, 300)}")}");
}

static async Task FillAsync(IServiceProvider sp, string tipo, int n)
{
    var cert = sp.GetRequiredService<ICertificacionDteService>();
    var esc = await cert.GetEscenariosAsync(tipo, EmpresaId);
    if (!esc.IsSuccess || esc.Value is null) { Console.WriteLine($"   sin escenarios para {tipo}: {esc.Error}"); return; }

    var pendientes = esc.Value.Where(e => e.EstadoActual != "COMPLETADO").Take(n).ToList();
    Console.WriteLine($"\n== FILL {tipo}: {pendientes.Count} escenarios pendientes a cubrir ==");
    var hechos = 0;
    var fallosSeguidos = 0;
    foreach (var e in pendientes)
    {
        // Reintenta ante "YA EXISTE" (correlativo local por detrás del histórico de MH tras la
        // limpieza): cada emisión consume un correlativo, así que reintentar avanza hasta superarlo.
        var linked = false;
        for (var intento = 0; intento < 12 && !linked; intento++)
        {
            var (ok, msg, docId) = await EmitirTipoAsync(sp, tipo);
            if (ok && docId is int id)
            {
                var link = await cert.MarcarCompletadoAsync(id, new MarcarCompletadoRequest { EscenarioId = e.Id }, EmpresaId, Actor);
                linked = link.IsSuccess && link.Value?.EstadoCodigo == "COMPLETADO";
                if (linked) { hechos++; fallosSeguidos = 0; }
            }
            else if (msg.Contains("YA EXISTE", StringComparison.OrdinalIgnoreCase)
                  || msg.Contains("\"codigoMsg\":\"004\"", StringComparison.OrdinalIgnoreCase))
            {
                continue; // correlativo colisiona con MH; el siguiente intento usa el siguiente número
            }
            else
            {
                Console.WriteLine($"   {e.Codigo}: emisión FALLÓ — {msg}");
                break; // error estructural, no de correlativo: no insistir con este escenario
            }
        }
        if (!linked)
        {
            fallosSeguidos++;
            if (fallosSeguidos >= 3) { Console.WriteLine($"   {tipo}: 3 escenarios fallidos seguidos, abortando el tipo"); break; }
        }
    }
    Console.WriteLine($"   {tipo}: completados en esta corrida = {hechos}/{pendientes.Count}");
}

/// <summary>Completa RETORNO usando eventos procesados sin asociar y FE distintas para no exceder el valor retornable.</summary>
static async Task FillRetornosAsync(IServiceProvider sp)
{
    var db = sp.GetRequiredService<NeoStpDbContext>();
    var svc = sp.GetRequiredService<IDteDocumentosService>();
    var cert = sp.GetRequiredService<ICertificacionDteService>();

    var escenariosResult = await cert.GetEscenariosAsync("RETORNO", EmpresaId);
    if (!escenariosResult.IsSuccess || escenariosResult.Value is null)
    {
        Console.WriteLine($"   RETORNO: no se pudieron leer escenarios — {escenariosResult.Error}");
        return;
    }

    var pendientes = escenariosResult.Value.Where(e => e.EstadoActual != "COMPLETADO").ToList();
    if (pendientes.Count == 0)
    {
        Console.WriteLine("   RETORNO: matriz ya completada.");
        return;
    }

    var eventosDisponibles = new Queue<int>(await db.DteEventos.AsNoTracking()
        .Where(e => e.EmpresaId == EmpresaId && e.TipoEventoCodigo == "RETORNO"
                 && e.EstadoCodigo == "PROCESADO"
                 && !db.CertificacionPruebas.Any(p => p.EventoId == e.Id))
        .OrderBy(e => e.Id)
        .Select(e => e.Id)
        .ToListAsync());

    var docsNoElegibles = await db.DteEventoDocumentosRelacionados.AsNoTracking()
        .Where(r => r.Evento.EmpresaId == EmpresaId && r.Evento.EstadoCodigo == "PROCESADO"
                 && (r.Evento.TipoEventoCodigo == "RETORNO"
                     || r.Evento.TipoEventoCodigo == "INVALIDACION"))
        .Select(r => r.DocumentoId)
        .Distinct()
        .ToListAsync();
    var documentos = new Queue<int>(await db.DteDocumentos.AsNoTracking()
        .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "01"
                 && d.EstadoCodigo == DteEstadoCodigos.Procesado && d.SelloRecibido != null
                 && d.TotalPagar > 0 && d.FechaEmision >= DateTime.Today.AddMonths(-3)
                 && !docsNoElegibles.Contains(d.Id))
        .OrderByDescending(d => d.Id)
        .Select(d => d.Id)
        .Take(pendientes.Count)
        .ToListAsync());

    Console.WriteLine($"\n== FILL RETORNOS: {pendientes.Count} escenarios pendientes ==");
    foreach (var escenario in pendientes)
    {
        int eventoId;
        if (eventosDisponibles.Count > 0)
        {
            eventoId = eventosDisponibles.Dequeue();
            Console.WriteLine($"   {escenario.Codigo}: reutilizando evento PROCESADO #{eventoId}");
        }
        else
        {
            if (documentos.Count == 0) { Console.WriteLine("   Sin FE elegibles distintas."); break; }
            var documentoId = documentos.Dequeue();
            var resultado = await svc.TransmitirEventoRetornoAsync(EmpresaId, documentoId, Actor);
            if (!resultado.IsSuccess || resultado.Value?.EventoId is not int nuevoEventoId)
            {
                Console.WriteLine($"   {escenario.Codigo}: FALLÓ DTE #{documentoId} — [{resultado.ErrorCode}] {resultado.Error}");
                break;
            }
            eventoId = nuevoEventoId;
        }

        var link = await cert.MarcarCompletadoPorEventoAsync(
            eventoId, new MarcarCompletadoRequest { EscenarioId = escenario.Id }, EmpresaId, Actor);
        if (!link.IsSuccess || link.Value?.EstadoCodigo != "COMPLETADO")
        {
            Console.WriteLine($"   {escenario.Codigo}: no se pudo asociar evento #{eventoId} — {link.Error}");
            break;
        }
        Console.WriteLine($"   {escenario.Codigo}: ✔ evento #{eventoId} COMPLETADO");
    }
}

/// <summary>Llena la matriz de eventos ligando eventos PROCESADO a sus escenarios.</summary>
static async Task FillEventosAsync(IServiceProvider sp)
{
    var db = sp.GetRequiredService<NeoStpDbContext>();
    var svc = sp.GetRequiredService<IDteDocumentosService>();
    var cert = sp.GetRequiredService<ICertificacionDteService>();

    // Docs PROCESADO para invalidar (uno distinto por escenario de invalidación).
    var docsParaInvalidar = new Queue<int>(await db.DteDocumentos.AsNoTracking()
        .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "01"
                 && d.EstadoCodigo == DteEstadoCodigos.Procesado && d.SelloRecibido != null)
        .OrderByDescending(d => d.Id).Select(d => d.Id).Take(30).ToListAsync());
    // Retorno (ERET) aplica a exportaciones (el emisor del esquema lleva campos de exportación):
    // preferir una Factura de Exportación (11); si no hay, caer a Factura (01).
    var docOrigenRetorno = await db.DteDocumentos.AsNoTracking()
        .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "11" && d.EstadoCodigo == DteEstadoCodigos.Procesado)
        .OrderByDescending(d => d.Id).Select(d => d.Id).FirstOrDefaultAsync();
    if (docOrigenRetorno == 0)
        docOrigenRetorno = await db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "01" && d.EstadoCodigo == DteEstadoCodigos.Procesado)
            .OrderByDescending(d => d.Id).Select(d => d.Id).FirstOrDefaultAsync();

    Console.WriteLine("\n== FILL EVENTOS ==");
    await FillEventoTipoAsync(cert, "INVALIDACION", () => svc.TransmitirInvalidacionEventoAsync(
        EmpresaId, docsParaInvalidar.Count > 0 ? docsParaInvalidar.Dequeue() : 0, 2,
        "Prueba certificación invalidación", null,
        "Carlos Antonio Garcia", "13", "000000000", Actor));
    await FillEventoTipoAsync(cert, "CONTINGENCIA", async () =>
    {
        var pl = await db.DteDocumentos.AsNoTracking().Include(d => d.Detalles)
            .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "01" && d.EstadoCodigo == DteEstadoCodigos.Procesado)
            .OrderByDescending(d => d.Id).FirstOrDefaultAsync();
        var req = ClonarRequest(pl!);
        req.TipoTransmision = 2; req.ModeloFacturacion = 2; req.TipoContingenciaCodigo = "1"; req.MotivoContingencia = null;
        // Modo contingencia: borrador+generar+firmar SIN enviar (el DTE se retiene para el lote),
        // así su codigoGeneracion no existe aún en MH y el evento no choca con "ya existe".
        var b = await svc.CreateBorradorAsync(EmpresaId, req, Actor);
        var id = b.Value?.Id ?? 0;
        if (id > 0) { await svc.GenerarAsync(EmpresaId, id, Actor); await svc.FirmarAsync(EmpresaId, id, Actor); }
        return await svc.TransmitirEventoContingenciaAsync(EmpresaId, new[] { id }, 1, null,
            "Carlos Antonio Garcia", "13", "000000000", Actor);
    });
    await FillEventoTipoAsync(cert, "RETORNO", () => svc.TransmitirEventoRetornoAsync(EmpresaId, docOrigenRetorno, Actor));
    await FillEventoTipoAsync(cert, "OPERACIONES_ESPECIALES", () => svc.TransmitirEventoOperacionesEspecialesAsync(
        EmpresaId, null, "Prueba certificación operaciones especiales", 100m, Actor));
}

static async Task FillEventoTipoAsync(ICertificacionDteService cert, string tipoEvento, Func<Task<Result<CrearEventoResultadoDto>>> emitir)
{
    var esc = await cert.GetEscenariosAsync(tipoEvento, EmpresaId);
    if (!esc.IsSuccess || esc.Value is null) { Console.WriteLine($"   {tipoEvento}: sin escenarios"); return; }
    var pend = esc.Value.Where(e => e.EstadoActual != "COMPLETADO").ToList();
    var hechos = 0;
    foreach (var e in pend)
    {
        var r = await emitir();
        var evId = r.Value?.EventoId ?? 0;
        if (r.IsSuccess && evId > 0)
        {
            var link = await cert.MarcarCompletadoPorEventoAsync(evId, new MarcarCompletadoRequest { EscenarioId = e.Id }, EmpresaId, Actor);
            if (link.IsSuccess && link.Value?.EstadoCodigo == "COMPLETADO") hechos++;
        }
        else { Console.WriteLine($"   {e.Codigo}: FALLÓ — [{r.ErrorCode}] {Short(r.Error, 600)}"); break; }
    }
    Console.WriteLine($"   {tipoEvento}: completados {hechos}/{pend.Count}");
}

/// <summary>
/// Flujo real de contingencia: emite una Factura en MODO CONTINGENCIA (retenida, no enviada
/// individualmente) → borrador+generar+firmar → luego el evento de contingencia que la referencia.
/// El DTE queda con codigoGeneracion pero SIN sello individual, así el evento no choca con
/// "codigo generacion ya existe".
/// </summary>
static async Task ContingenciaE2eAsync(IServiceProvider sp)
{
    var db = sp.GetRequiredService<NeoStpDbContext>();
    var svc = sp.GetRequiredService<IDteDocumentosService>();

    var plantilla = await db.DteDocumentos.AsNoTracking().Include(d => d.Detalles)
        .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == "01" && d.EstadoCodigo == DteEstadoCodigos.Procesado)
        .OrderByDescending(d => d.Id).FirstOrDefaultAsync();
    if (plantilla is null) { Console.WriteLine("   sin factura plantilla"); return; }

    var req = ClonarRequest(plantilla);
    req.TipoTransmision = 2;               // contingencia
    req.ModeloFacturacion = 2;             // diferido
    req.TipoContingenciaCodigo = "1";      // CAT-005: 1 = no disponibilidad del sistema MH
    req.MotivoContingencia = null;

    Console.WriteLine("\n== CONTINGENCIA E2E ==");
    var borr = await svc.CreateBorradorAsync(EmpresaId, req, Actor);
    if (!borr.IsSuccess || borr.Value is null) { Console.WriteLine($"   borrador FALLÓ: [{borr.ErrorCode}] {Short(borr.Error, 200)}"); return; }
    var id = borr.Value.Id;
    Console.WriteLine($"   borrador #{id} nc={borr.Value.NumeroControl} (contingencia, retenido)");

    var gen = await svc.GenerarAsync(EmpresaId, id, Actor);
    if (!gen.IsSuccess) { Console.WriteLine($"   generar FALLÓ: {Short(gen.Error, 200)}"); return; }
    var fir = await svc.FirmarAsync(EmpresaId, id, Actor);
    if (!fir.IsSuccess) { Console.WriteLine($"   firmar FALLÓ: {Short(fir.Error, 200)}"); return; }
    Console.WriteLine("   generado + firmado (NO enviado individualmente)");

    var ev = await svc.TransmitirEventoContingenciaAsync(
        EmpresaId, new[] { id }, tipoContingencia: 1, motivo: null,
        nombreResponsable: "Carlos Antonio Garcia", tipoDocResponsable: "13", numeroDocResponsable: "000000000",
        actor: Actor);
    Console.WriteLine($"   EVENTO CONTINGENCIA: {(ev.IsSuccess ? $"✔ {ev.Value!.SelloOEstado}" : $"✘ [{ev.ErrorCode}] {Short(ev.Error, 300)}")}");
}

// Mapa tipo DTE → esquema oficial vigente (MH 2026-08-11).
static string? SchemaRel(string tipo) => tipo switch
{
    "01" => "v2/fe-f-v2.json",
    "03" => "v4/fe-ccf-v4.json",
    "04" => "v4/fe-nr-v4.json",
    "05" => "v4/fe-nc-v4.json",
    "06" => "v4/fe-nd-v4.json",
    "07" => "v2/fe-cr-v2.json",
    "08" => "v2/fe-cl-v2.json",
    "09" => "v2/fe-dcl-v2.json",
    "11" => "v3/fe-fex-v3.json",
    "14" => "v2/fe-fse-v2.json",
    "15" => "v2/fe-cd-v2.json",
    _ => null,
};

/// <summary>Genera el DTE (código actual) y lo valida contra el esquema oficial vigente. Las
/// violaciones son el delta de migración a la versión nueva.</summary>
static async Task ValidateTipoAsync(IServiceProvider sp, string apiDir, string tipo)
{
    var db = sp.GetRequiredService<NeoStpDbContext>();
    var gen = sp.GetRequiredService<IDteGeneratorService>();

    var rel = SchemaRel(tipo);
    if (rel is null) { Console.WriteLine($"   {tipo}: sin esquema mapeado"); return; }
    var schemaPath = Path.GetFullPath(Path.Combine(apiDir, "..", "..", "tools", "CertHarness", "schemas", "svfe-json-schemas", rel));
    if (!File.Exists(schemaPath)) { Console.WriteLine($"   {tipo}: esquema no encontrado en {schemaPath}"); return; }

    var doc = await db.DteDocumentos.AsNoTracking().Include(d => d.Empresa).Include(d => d.Detalles)
        .Where(d => d.EmpresaId == EmpresaId && d.TipoDteCodigo == tipo && d.EstadoCodigo == DteEstadoCodigos.Procesado)
        .OrderByDescending(d => d.Id).FirstOrDefaultAsync();
    if (doc is null) { Console.WriteLine($"   {tipo}: sin doc PROCESADO para generar"); return; }

    var cfg = await db.DteConfiguracion.AsNoTracking().FirstOrDefaultAsync(c => c.EmpresaId == EmpresaId);
    var g = gen.Generar(doc, cfg);
    if (!g.IsSuccess || g.Value is null) { Console.WriteLine($"   {tipo}: generación falló — {g.Error}"); return; }

    var schema = await JsonSchema.FromFileAsync(schemaPath);
    var errors = schema.Validate(g.Value);
    if (errors.Count == 0)
    {
        Console.WriteLine($"   {tipo} vs {rel}: ✔ VÁLIDO (0 violaciones)");
    }
    else
    {
        Console.WriteLine($"   {tipo} vs {rel}: ✘ {errors.Count} violaciones:");
        foreach (var e in errors.Take(25))
            Console.WriteLine($"      - {e.Path}: {e.Kind}");
    }
}

static string Short(string? s, int max = 40)
    => string.IsNullOrEmpty(s) ? "(vacío)" : s.Length <= max ? s : s[..max] + "…";
