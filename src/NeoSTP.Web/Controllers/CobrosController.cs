using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Cobros / cuentas por cobrar. UI web sobre los servicios ya expuestos para NeoCloud Mobile.
/// Opera dentro de una empresa; SuperAdmin debe seleccionar empresa en modo soporte.
/// </summary>
[Authorize]
[Route("[controller]")]
public class CobrosController : Controller
{
    public static readonly string[] TiposCuenta = ["TRANSFERENCIA", "WOMPI", "PAGADITO", "ACH", "OTRO"];
    public static readonly string[] FormasPago = ["EFECTIVO", "TRANSFERENCIA", "TARJETA", "CHEQUE", "OTRO"];

    private readonly ICobranzaService _cobranza;
    private readonly ICobroQrService _qr;
    private readonly IRecordatorioCobroService _recordatorios;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public CobrosController(
        ICobranzaService cobranza,
        ICobroQrService qr,
        IRecordatorioCobroService recordatorios,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _cobranza = cobranza;
        _qr = qr;
        _recordatorios = recordatorios;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    // ── Recordatorios automáticos (V2-D3) ────────────────────────────────────

    [HttpGet("recordatorios")]
    public async Task<IActionResult> Recordatorios(CancellationToken ct)
    {
        if (!Has("Cobros.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var config = await _recordatorios.GetConfiguracionAsync(eid, ct);
        ViewBag.PuedeGestionar = Has("Cobros.Gestionar");
        ViewBag.Historial = await _recordatorios.GetHistorialAsync(eid, 50, ct);
        return View(config.Value);
    }

    [HttpPost("recordatorios")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recordatorios(GuardarConfigRecordatorioRequest model, CancellationToken ct)
    {
        if (!Has("Cobros.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _recordatorios.GuardarConfiguracionAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Configuración guardada." : r.Error;
        return RedirectToAction(nameof(Recordatorios));
    }

    [HttpPost("recordatorios/ejecutar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordatoriosEjecutar(CancellationToken ct)
    {
        if (!Has("Cobros.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _recordatorios.EjecutarSegunConfiguracionAsync(eid, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess
            ? $"Recordatorios ejecutados: {r.Value!.EnviadosEmail} email, {r.Value.EnviadosWhatsApp} WhatsApp, {r.Value.Omitidos} omitidos, {r.Value.Fallidos} fallidos."
            : r.Error;
        return RedirectToAction(nameof(Recordatorios));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, bool soloVencidas = false, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Cobros.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var vm = await BuildIndexModelAsync(eid, search, soloVencidas, page, ct);
        return View(vm);
    }

    [HttpGet("Cliente/{clienteId:int}")]
    public async Task<IActionResult> Cliente(int clienteId, CancellationToken ct)
    {
        if (!Has("Cobros.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var saldo = await _cobranza.GetSaldoClienteAsync(eid, clienteId, ct);
        if (saldo.IsFailure) return NotFound();

        var pagosPorDte = new Dictionary<int, IReadOnlyList<PagoClienteDto>>();
        foreach (var factura in saldo.Value!.Facturas)
        {
            var pagos = await _cobranza.GetPagosAsync(eid, factura.DteDocumentoId, ct);
            pagosPorDte[factura.DteDocumentoId] = pagos.Value ?? Array.Empty<PagoClienteDto>();
        }

        return View(new CobrosClienteViewModel
        {
            Saldo = saldo.Value,
            PagosPorDte = pagosPorDte,
            PuedeGestionar = Has("Cobros.Gestionar"),
            CuentasCobro = await _qr.ListarCuentasAsync(eid, ct),
        });
    }

    [HttpPost("RegistrarPago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPago(int dteDocumentoId, RegistrarPagoRequest request, string? returnUrl, CancellationToken ct)
    {
        if (!Has("Cobros.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _cobranza.RegistrarPagoAsync(eid, dteDocumentoId, request, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Pago registrado." : result.Error;
        return RedirectBack(returnUrl);
    }

    [HttpPost("ConfirmarPago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarPago(int pagoId, string? returnUrl, CancellationToken ct)
    {
        if (!Has("Cobros.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _cobranza.ConfirmarPagoAsync(eid, pagoId, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Pago confirmado." : result.Error;
        return RedirectBack(returnUrl);
    }

    [HttpPost("AnularPago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnularPago(int pagoId, string? returnUrl, CancellationToken ct)
    {
        if (!Has("Cobros.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _cobranza.AnularPagoAsync(eid, pagoId, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Pago anulado." : result.Error;
        return RedirectBack(returnUrl);
    }

    [HttpPost("GenerarQr")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerarQr(GenerarQrCobroRequest request, string? search, bool soloVencidas = false, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Cobros.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _qr.GenerarQrAsync(eid, request, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index), new { search, soloVencidas, page });
        }

        var vm = await BuildIndexModelAsync(eid, search, soloVencidas, page, ct);
        vm.Qr = result.Value;
        TempData["Success"] = "QR de cobro generado.";
        return View(nameof(Index), vm);
    }

    /// <summary>Genera el cobro de una factura con la cuenta predeterminada, desde el botón "Cobrar".</summary>
    [HttpGet("Cobrar")]
    public async Task<IActionResult> Cobrar(int dteDocumentoId, CancellationToken ct)
    {
        if (!Has("Cobros.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _qr.GenerarQrAsync(eid, new GenerarQrCobroRequest { DteDocumentoId = dteDocumentoId }, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        var vm = await BuildIndexModelAsync(eid, null, false, 1, ct);
        vm.Qr = result.Value;
        return View(nameof(Index), vm);
    }

    [HttpGet("CobrarPdf")]
    public async Task<IActionResult> CobrarPdf(int? dteDocumentoId, int? cuentaCobroId, decimal? monto, string? referencia, CancellationToken ct)
    {
        if (!Has("Cobros.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _qr.GenerarPdfAsync(eid, new GenerarQrCobroRequest
        {
            DteDocumentoId = dteDocumentoId,
            CuentaCobroId = cuentaCobroId,
            Monto = monto,
            Referencia = referencia,
        }, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        return File(result.Value!.Pdf, "application/pdf", result.Value.FileName);
    }

    [HttpGet("Cuentas")]
    public async Task<IActionResult> Cuentas(CancellationToken ct)
    {
        if (!Has("Cobros.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        return View(new CobrosCuentasViewModel
        {
            Cuentas = await _qr.ListarCuentasAsync(eid, ct),
            PuedeGestionar = Has("Cobros.Gestionar"),
            Tipos = TiposCuenta,
        });
    }

    [HttpPost("Cuentas/Crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCuenta(CrearCuentaCobroRequest request, CancellationToken ct)
    {
        if (!Has("Cobros.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _qr.CrearCuentaAsync(eid, request, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Cuenta de cobro creada." : result.Error;
        return RedirectToAction(nameof(Cuentas));
    }

    [HttpPost("Cuentas/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarCuenta(int id, CrearCuentaCobroRequest request, CancellationToken ct)
    {
        if (!Has("Cobros.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _qr.ActualizarCuentaAsync(eid, id, request, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Cuenta de cobro actualizada." : result.Error;
        return RedirectToAction(nameof(Cuentas));
    }

    [HttpPost("Cuentas/Inactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InactivarCuenta(int id, CancellationToken ct)
    {
        if (!Has("Cobros.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _qr.InactivarCuentaAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Cuenta de cobro inactivada." : result.Error;
        return RedirectToAction(nameof(Cuentas));
    }

    private async Task<CobrosIndexViewModel> BuildIndexModelAsync(int empresaId, string? search, bool soloVencidas, int page, CancellationToken ct)
    {
        var query = new CobranzaQuery
        {
            Search = search,
            SoloVencidas = soloVencidas,
            Page = page <= 0 ? 1 : page,
            PageSize = 20,
        };

        var pendientes = await _cobranza.GetPendientesAsync(empresaId, query, ct);
        if (pendientes.IsFailure)
        {
            TempData["Error"] = pendientes.Error;
        }

        return new CobrosIndexViewModel
        {
            Resumen = await _cobranza.GetResumenAsync(empresaId, ct),
            Pendientes = pendientes.Value ?? PagedResult<CobroPendienteDto>.Create(Array.Empty<CobroPendienteDto>(), 0, query.Page, query.PageSize),
            CuentasCobro = await _qr.ListarCuentasAsync(empresaId, ct),
            PuedeGestionar = Has("Cobros.Gestionar"),
            Search = search,
            SoloVencidas = soloVencidas,
            Page = query.Page,
            FormasPago = FormasPago,
        };
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Cobros opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }

        return RedirectToAction("Index", "Home");
    }

    private IActionResult RedirectBack(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
}

public class CobrosIndexViewModel
{
    public CobranzaResumenDto Resumen { get; set; } = new();
    public PagedResult<CobroPendienteDto> Pendientes { get; set; } = PagedResult<CobroPendienteDto>.Create(Array.Empty<CobroPendienteDto>(), 0, 1, 20);
    public IReadOnlyList<CuentaCobroDto> CuentasCobro { get; set; } = Array.Empty<CuentaCobroDto>();
    public CobroQrDto? Qr { get; set; }
    public bool PuedeGestionar { get; set; }
    public string? Search { get; set; }
    public bool SoloVencidas { get; set; }
    public int Page { get; set; } = 1;
    public string[] FormasPago { get; set; } = Array.Empty<string>();
}

public class CobrosClienteViewModel
{
    public SaldoClienteDto Saldo { get; set; } = new();
    public Dictionary<int, IReadOnlyList<PagoClienteDto>> PagosPorDte { get; set; } = new();
    public IReadOnlyList<CuentaCobroDto> CuentasCobro { get; set; } = Array.Empty<CuentaCobroDto>();
    public bool PuedeGestionar { get; set; }
}

public class CobrosCuentasViewModel
{
    public IReadOnlyList<CuentaCobroDto> Cuentas { get; set; } = Array.Empty<CuentaCobroDto>();
    public bool PuedeGestionar { get; set; }
    public string[] Tipos { get; set; } = Array.Empty<string>();
}
