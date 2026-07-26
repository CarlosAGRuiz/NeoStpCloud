using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Productos.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOPOS â€” punto de venta web. Permisos Pos.Ver / Pos.Vender / Pos.Anular.</summary>
[Authorize]
[RequireModulo("NEOPOS")]
public class PosController : Controller
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IPosService _pos;
    private readonly IPosConfigService _posConfig;
    private readonly ITicketPdfService _ticketPdf;
    private readonly IProductosService _productos;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public PosController(IPosService pos, IPosConfigService posConfig, ITicketPdfService ticketPdf, IProductosService productos, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _pos = pos;
        _posConfig = posConfig;
        _ticketPdf = ticketPdf;
        _productos = productos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Pos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _pos.ListAsync(eid, null, null, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        var resumen = await _pos.ResumenDiaAsync(eid, DateOnly.FromDateTime(DateTime.UtcNow), ct);
        ViewBag.Search = search;
        ViewBag.Resumen = resumen.Value;
        ViewBag.PuedeVender = Has("Pos.Vender");
        ViewBag.PuedeAnular = Has("Pos.Anular");
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Nueva()
    {
        if (!Has("Pos.Vender")) return Forbid();
        if (RequireEmpresa() is null) return RedirectToSoporte();
        ViewBag.FormasPago = NeoSTP.Domain.Core.Pos.FormasPagoPos.All;
        return View();
    }

    /// <summary>BÃºsqueda de productos para el POS (JSON).</summary>
    [HttpGet]
    public async Task<IActionResult> BuscarProductos(string? term, CancellationToken ct)
    {
        if (!Has("Pos.Vender")) return Forbid();
        if (RequireEmpresa() is not int eid) return Json(Array.Empty<object>());

        var result = await _productos.GetListAsync(eid, new PagedQuery { Search = term, Page = 1, PageSize = 15 }, ct: ct);
        var activos = (result.Value?.Items ?? (IReadOnlyList<ProductoDto>)Array.Empty<ProductoDto>())
            .Where(p => p.EstadoCodigo == "ACTIVO")
            .ToList();
        var escalas = await _productos.GetEscalasAsync(eid, activos.Select(p => p.Id).ToList(), ct);
        var items = activos.Select(p =>
        {
            IReadOnlyList<NeoSTP.Application.Productos.Dtos.PrecioEscalaDto> esc =
                escalas.TryGetValue(p.Id, out var e) ? e : Array.Empty<NeoSTP.Application.Productos.Dtos.PrecioEscalaDto>();
            return new
            {
                id = p.Id, codigo = p.CodigoInterno, nombre = p.Nombre, precio = p.PrecioUnitario, iva = p.AplicaIva,
                escalas = esc.Select(x => new { min = x.CantidadMinima, precio = x.PrecioUnitario }).ToArray(),
            };
        });
        return Json(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(string lineasJson, string formaPago, string? clienteNombre, decimal? efectivoRecibido, string? nota, CancellationToken ct)
    {
        if (!Has("Pos.Vender")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        List<CrearVentaLineaRequest>? lineas;
        try { lineas = JsonSerializer.Deserialize<List<CrearVentaLineaRequest>>(lineasJson ?? "[]", JsonOpts); }
        catch { lineas = null; }

        if (lineas is null || lineas.Count == 0)
        {
            TempData["Error"] = "Agrega al menos un producto a la venta.";
            return RedirectToAction(nameof(Nueva));
        }

        var req = new CrearVentaRequest
        {
            FormaPagoCodigo = string.IsNullOrWhiteSpace(formaPago) ? "EFECTIVO" : formaPago,
            ClienteNombre = clienteNombre, EfectivoRecibido = efectivoRecibido, Nota = nota, Lineas = lineas,
        };
        var result = await _pos.CrearVentaAsync(eid, req, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "No se pudo registrar la venta.";
            return RedirectToAction(nameof(Nueva));
        }
        TempData["Success"] = $"Venta {result.Value!.Numero} registrada.";
        return RedirectToAction(nameof(Detalle), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        if (!Has("Pos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _pos.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.PuedeAnular = Has("Pos.Anular");
        ViewBag.PuedeFacturar = Has("Pos.Vender") && Has("DTE.Emitir");
        var impresoras = await _posConfig.ListImpresorasAsync(eid, ct);
        ViewBag.ImpresorasRed = impresoras.Value?
            .Where(i => i.Conexion == NeoSTP.Domain.Core.Pos.ConexionImpresora.Red && i.EstadoCodigo == "ACTIVA")
            .ToList() ?? new List<NeoSTP.Application.Pos.Dtos.ImpresoraPosDto>();
        return View(result.Value);
    }

    /// <summary>Vista imprimible del ticket (HTML tÃ©rmico, auto-print opcional con ?print=1).</summary>
    [HttpGet]
    public async Task<IActionResult> Ticket(int id, bool print = false, CancellationToken ct = default)
    {
        if (!Has("Pos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _pos.GetTicketAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.AutoPrint = print;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> TicketPdf(int id, CancellationToken ct)
    {
        if (!Has("Pos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _pos.GetTicketAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        var bytes = _ticketPdf.GenerarTicket(result.Value!);
        return File(bytes, "application/pdf", $"ticket_{result.Value!.Numero}.pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enviar(int id, string email, CancellationToken ct)
    {
        if (!Has("Pos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _pos.EnviarTicketCorreoAsync(eid, id, email, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? $"Ticket enviado a {email}." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Promover(int id, string tipoDteCodigo = "01", int? clienteId = null, CancellationToken ct = default)
    {
        if (!Has("Pos.Vender") || !Has("DTE.Emitir")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _pos.PromoverADteAsync(eid, id, new PromoverVentaRequest { TipoDteCodigo = tipoDteCodigo, ClienteId = clienteId }, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Venta facturada como DTE (#{result.Value!.DteDocumentoId})."
            : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id, CancellationToken ct)
    {
        if (!Has("Pos.Anular")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _pos.AnularAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Venta anulada." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "El POS opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
