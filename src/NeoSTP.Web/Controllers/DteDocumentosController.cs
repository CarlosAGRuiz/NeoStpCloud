using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Certificacion;
using NeoSTP.Application.Dte.Certificacion.Dtos;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Productos;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class DteDocumentosController : Controller
{
    private readonly IDteDocumentosService _service;
    private readonly IClientesService _clientes;
    private readonly IProductosService _productos;
    private readonly ICatalogosService _catalogos;
    private readonly ICertificacionDteService _certificacion;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Empresas.IEmpresaContext _empresaContext;

    public DteDocumentosController(
        IDteDocumentosService service,
        IClientesService clientes,
        IProductosService productos,
        ICatalogosService catalogos,
        ICertificacionDteService certificacion,
        ICurrentUser currentUser,
        NeoSTP.Application.Empresas.IEmpresaContext empresaContext)
    {
        _service = service;
        _clientes = clientes;
        _productos = productos;
        _catalogos = catalogos;
        _certificacion = certificacion;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DteListQuery query, CancellationToken ct)
    {
        if (!Has("DTE.Emitir") && !Has("DTE.Consultar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        query.PageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var result = await _service.GetListAsync(eid, query, ct);
        ViewBag.Query = query;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        if (!Has("DTE.Emitir") && !Has("DTE.Consultar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _service.GetByIdAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] string tipo = "01", [FromQuery] int? certificacionEscenarioId = null, CancellationToken ct = default)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        await LoadFormDataAsync(eid, ct);
        var vm = await BuildCreateModelAsync(eid, tipo, certificacionEscenarioId, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDteDocumentoViewModel model, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        // Filtrar líneas vacías
        model.Lineas = (model.Lineas ?? new())
            .Where(l => l.Cantidad > 0 && (l.PrecioUnitario > 0 || l.ProductoId.HasValue))
            .ToList();

        if (model.Lineas.Count == 0)
            ModelState.AddModelError(string.Empty, "Agregue al menos una línea con cantidad y precio.");

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(eid, ct);
            return View(model);
        }

        var request = new CreateDteDocumentoRequest
        {
            TipoDteCodigo = model.TipoDteCodigo,
            ClienteId = model.ClienteId,
            ReceptorManual = model.ClienteId is null && !string.IsNullOrEmpty(model.ReceptorNombre) ? new ReceptorDto
            {
                TipoDocumento = model.ReceptorTipoDocumento,
                NumeroDocumento = model.ReceptorNumeroDocumento,
                Nrc = model.ReceptorNrc,
                Nombre = model.ReceptorNombre,
                TipoContribuyente = model.ReceptorTipoContribuyente,
                CodigoActividad = model.ReceptorCodigoActividad,
                ActividadEconomica = model.ReceptorActividadEconomica,
                DepartamentoCodigo = model.ReceptorDepartamentoCodigo,
                MunicipioCodigo = model.ReceptorMunicipioCodigo,
                Direccion = model.ReceptorDireccion,
                Correo = model.ReceptorCorreo,
                Telefono = model.ReceptorTelefono,
            } : null,
            CondicionOperacionCodigo = model.CondicionOperacionCodigo,
            FormaPagoCodigo = model.FormaPagoCodigo,
            PlazoDias = model.PlazoDias,
            NumeroDocumentoRelacionado = model.NumeroDocumentoRelacionado,
            TipoDteRelacionado = model.TipoDteRelacionado,
            TipoGeneracionRelacionado = model.TipoGeneracionRelacionado,
            Observaciones = model.Observaciones,
            Lineas = model.Lineas.Select(l => new CreateDteDocumentoLineaRequest
            {
                ProductoId = l.ProductoId,
                Codigo = l.Codigo ?? string.Empty,
                Descripcion = l.Descripcion ?? string.Empty,
                UnidadMedidaCodigo = l.UnidadMedidaCodigo ?? "59",
                TipoItem = l.TipoItem == 0 ? 1 : l.TipoItem,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario,
                MontoDescuento = l.MontoDescuento,
                Clasificacion = l.Clasificacion ?? "GRAVADA",
                NoGravado = l.NoGravado,
            }).ToList(),
        };

        var result = await _service.CreateBorradorAsync(eid, request, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            await LoadFormDataAsync(eid, ct);
            return View(model);
        }

        if (model.CertificacionEscenarioId.HasValue)
        {
            var sync = await _certificacion.MarcarCompletadoAsync(result.Value!.Id, new MarcarCompletadoRequest
            {
                EscenarioId = model.CertificacionEscenarioId.Value,
                Notas = $"DTE creado desde certificacion: {model.CertificacionEscenarioCodigo}",
            }, eid, _currentUser.Username, ct);

            if (sync.IsFailure)
            {
                TempData["Error"] = $"DTE creado, pero no se pudo asociar a certificacion: {sync.Error}";
                return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
            }
        }

        if (model.GenerarInmediato)
        {
            var gen = await _service.GenerarAsync(eid, result.Value!.Id, _currentUser.Username, ct);
            if (gen.IsFailure)
            {
                TempData["Error"] = $"Documento creado pero no se pudo generar JSON: {gen.Error}";
                return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
            }

            await SincronizarCertificacionAsync(eid, result.Value!.Id, ct);
        }

        TempData["Success"] = $"DTE {result.Value!.NumeroControl} creado.";
        return model.CertificacionEscenarioId.HasValue && !string.IsNullOrWhiteSpace(model.CertificacionTipoCodigo)
            ? RedirectToAction("Tipo", "Certificacion", new { codigo = model.CertificacionTipoCodigo })
            : RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(int id, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.GenerarAsync(eid, id, _currentUser.Username, ct);
        await SincronizarCertificacionAsync(eid, id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "JSON DTE generado." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Validar(int id, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.ValidarAsync(eid, id, _currentUser.Username, ct);
        await SincronizarCertificacionAsync(eid, id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "DTE validado." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Firmar(int id, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.FirmarAsync(eid, id, _currentUser.Username, ct);
        await SincronizarCertificacionAsync(eid, id, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "DTE firmado." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enviar(int id, CancellationToken ct)
    {
        if (!Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.EnviarAsync(eid, id, _currentUser.Username, ct);
        if (result.IsSuccess)
        {
            var estado = result.Value!.EstadoCodigo;
            TempData[estado == "PROCESADO" ? "Success" : "Error"] =
                estado == "PROCESADO" ? $"DTE procesado por Hacienda. Sello: {result.Value!.SelloRecibido}"
                                      : $"Hacienda devolvió estado {estado}.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }
        await SincronizarCertificacionAsync(eid, id, ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invalidar(int id, string? motivo, CancellationToken ct)
    {
        if (!Has("DTE.Invalidar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.InvalidarAsync(eid, id, motivo, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "DTE invalidado." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        if (!Has("DTE.Consultar") && !Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.ObtenerArchivosAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        return File(result.Value!.PdfContent, "application/pdf", result.Value!.PdfFileName);
    }

    [HttpGet]
    public async Task<IActionResult> Json(int id, CancellationToken ct)
    {
        if (!Has("DTE.Consultar") && !Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.ObtenerArchivosAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Value!.JsonContent ?? string.Empty);
        return File(bytes, "application/json", result.Value!.JsonFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reenviar(int id, string? destinatario, CancellationToken ct)
    {
        if (!Has("DTE.Reenviar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _service.ReenviarPorCorreoAsync(eid, id, destinatario, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"DTE reenviado a {result.Value!.Destinatario}."
            : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Esta pantalla opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }

    private async Task LoadFormDataAsync(int empresaId, CancellationToken ct)
    {
        var clientes = await _clientes.GetListAsync(empresaId, new PagedQuery { PageSize = 200 }, ct);
        ViewBag.Clientes = clientes.Value?.Items ?? new List<NeoSTP.Application.Clientes.Dtos.ClienteDto>();

        var productos = await _productos.GetListAsync(empresaId, new PagedQuery { PageSize = 500 }, ct);
        ViewBag.Productos = productos.Value?.Items ?? new List<NeoSTP.Application.Productos.Dtos.ProductoDto>();

        async Task<IReadOnlyList<NeoSTP.Application.Catalogos.Dtos.CatalogoItemDto>> Items(string c)
            => (await _catalogos.GetItemsAsync(c, empresaId, ct: ct)).Value
               ?? new List<NeoSTP.Application.Catalogos.Dtos.CatalogoItemDto>();

        ViewBag.UnidadesMedida = await Items("UNIDAD_MEDIDA");
        ViewBag.FormasPago = await Items("FORMA_PAGO");
        ViewBag.CondicionesOperacion = await Items("CONDICION_OPERACION");
        ViewBag.Departamentos = await Items("DEPARTAMENTO_ES");
        ViewBag.TiposDoc = await Items("TIPO_DOC_IDENTIDAD");
        ViewBag.TiposContrib = await Items("TIPO_CONTRIBUYENTE");
    }

    private async Task<CreateDteDocumentoViewModel> BuildCreateModelAsync(int empresaId, string tipo, int? certificacionEscenarioId, CancellationToken ct)
    {
        var vm = new CreateDteDocumentoViewModel
        {
            TipoDteCodigo = tipo,
            Lineas = new List<DteLineaViewModel> { new() { Cantidad = 1 } },
        };

        if (!certificacionEscenarioId.HasValue)
        {
            return vm;
        }

        var escenarios = await _certificacion.GetEscenariosAsync(tipo, empresaId, ct);
        var escenario = escenarios.Value?.FirstOrDefault(e => e.Id == certificacionEscenarioId.Value);
        if (escenario is null)
        {
            return vm;
        }

        vm.CertificacionEscenarioId = escenario.Id;
        vm.CertificacionTipoCodigo = tipo;
        vm.CertificacionEscenarioCodigo = escenario.Codigo;
        vm.CertificacionEscenarioNombre = escenario.Nombre;
        vm.Observaciones = $"Prueba de certificacion {escenario.Codigo}: {escenario.Nombre}";
        vm.Lineas = new List<DteLineaViewModel> { BuildCertificacionLinea(tipo, escenario.Codigo) };
        ApplyReceptorCertificacion(vm);
        return vm;
    }

    private static DteLineaViewModel BuildCertificacionLinea(string tipo, string? escenarioCodigo)
        => new()
        {
            Codigo = "CERT-001",
            Descripcion = $"Servicio de prueba certificacion {(!string.IsNullOrWhiteSpace(escenarioCodigo) ? escenarioCodigo : tipo)}",
            UnidadMedidaCodigo = "59",
            TipoItem = 1,
            Cantidad = 1,
            PrecioUnitario = 10,
            Clasificacion = "GRAVADA",
        };

    private static void ApplyReceptorCertificacion(CreateDteDocumentoViewModel vm)
    {
        if (vm.TipoDteCodigo == "01")
        {
            vm.ReceptorNombre = "Consumidor final pruebas";
            vm.ReceptorTipoDocumento = "13";
            vm.ReceptorNumeroDocumento = "00000000-0";
            vm.ReceptorCorreo = "certificacion@neostp.local";
            return;
        }

        vm.ReceptorNombre = "Cliente certificacion DTE";
        vm.ReceptorTipoDocumento = "36";
        vm.ReceptorNumeroDocumento = "06142803901121";
        vm.ReceptorNrc = vm.TipoDteCodigo is "03" or "05" or "06" ? "123456-7" : null;
        vm.ReceptorTipoContribuyente = "GRANDE";
        vm.ReceptorCodigoActividad = "62010";
        vm.ReceptorActividadEconomica = "Programacion informatica";
        vm.ReceptorDepartamentoCodigo = "06";
        vm.ReceptorMunicipioCodigo = "14";
        vm.ReceptorDireccion = "Direccion de prueba para certificacion";
        vm.ReceptorCorreo = "certificacion@neostp.local";
        vm.ReceptorTelefono = "22222222";

        if (vm.TipoDteCodigo is "05" or "06")
        {
            vm.TipoDteRelacionado = "03";
            vm.NumeroDocumentoRelacionado = "DTE-03-REFERENCIA-CERTIFICACION";
            vm.TipoGeneracionRelacionado = "2";
        }
    }

    private async Task SincronizarCertificacionAsync(int empresaId, int documentoId, CancellationToken ct)
    {
        var result = await _certificacion.SincronizarDocumentoAsync(documentoId, empresaId, _currentUser.Username, ct);
        if (result.IsFailure && result.ErrorCode != "CERT_PRUEBA_NOT_FOUND")
        {
            TempData["Error"] = result.Error;
        }
    }
}
