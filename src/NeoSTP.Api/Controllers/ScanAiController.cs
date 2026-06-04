using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NeoScanAI (Sprint 23 / B-3): bandeja de documentos capturados, revisión/corrección y
/// conversión a gasto, compra o DTE recibido. Requiere el módulo NEOSCANAI activo.
/// Lectura/captura: ScanAI.Ver; confirmaciones: ScanAI.Confirmar.
/// </summary>
[Authorize]
[RequireModule("NEOSCANAI")]
[Route("api/scanai/documentos")]
public class ScanAiController : ApiControllerBase
{
    private readonly IScanService _service;
    private readonly ICurrentUser _currentUser;

    public ScanAiController(IScanService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermiso("ScanAI.Ver")]
    public async Task<IActionResult> List([FromQuery] ScanQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ListAsync(eid, query, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermiso("ScanAI.Ver")]
    public async Task<IActionResult> Get(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetAsync(eid, id, ct));
    }

    [HttpGet("{id:int}/archivo")]
    [RequirePermiso("ScanAI.Ver")]
    public async Task<IActionResult> Archivo(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var a = await _service.GetArchivoAsync(eid, id, ct);
        return a is null ? NotFound(ApiResponse.Fail("Archivo no encontrado.", new[] { "SCAN_NOT_FOUND" }, HttpContext.TraceIdentifier))
                         : File(a.Contenido, a.ContentType, a.Nombre);
    }

    /// <summary>Sube un documento capturado (imagen/PDF en base64) y ejecuta la extracción.</summary>
    [HttpPost]
    [RequirePermiso("ScanAI.Ver")]
    public async Task<IActionResult> Subir([FromBody] SubirScanRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.SubirAsync(eid, req, _currentUser.Username, ct));
    }

    /// <summary>Corrige manualmente los campos extraídos.</summary>
    [HttpPut("{id:int}/campos")]
    [RequirePermiso("ScanAI.Ver")]
    public async Task<IActionResult> Corregir(int id, [FromBody] CorregirScanRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CorregirAsync(eid, id, req, _currentUser.Username, ct));
    }

    /// <summary>Recibe un resultado de extracción desde un proveedor externo de NeoScanAI.</summary>
    [HttpPost("{id:int}/resultado")]
    [RequirePermiso("ScanAI.Ver")]
    public async Task<IActionResult> Resultado(int id, [FromBody] ScanResultadoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.SetResultadoAsync(eid, id, req, _currentUser.Username, ct));
    }

    /// <summary>Confirma el escaneo como gasto (alimenta NeoProfit).</summary>
    [HttpPost("{id:int}/registrar-gasto")]
    [RequirePermiso("ScanAI.Confirmar")]
    public async Task<IActionResult> RegistrarGasto(int id, [FromBody] CreateProfitGastoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ConfirmarComoGastoAsync(eid, id, req, _currentUser.Username, ct));
    }

    /// <summary>Confirma el escaneo como compra (alimenta NeoProfit).</summary>
    [HttpPost("{id:int}/registrar-compra")]
    [RequirePermiso("ScanAI.Confirmar")]
    public async Task<IActionResult> RegistrarCompra(int id, [FromBody] CreateProfitCompraRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ConfirmarComoCompraAsync(eid, id, req, _currentUser.Username, ct));
    }

    /// <summary>Confirma el escaneo como DTE recibido de un proveedor.</summary>
    [HttpPost("{id:int}/registrar-dte-recibido")]
    [RequirePermiso("ScanAI.Confirmar")]
    public async Task<IActionResult> RegistrarDteRecibido(int id, [FromBody] RegistrarDteRecibidoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.RegistrarDteRecibidoAsync(eid, id, req, _currentUser.Username, ct));
    }

    /// <summary>Rechaza el escaneo.</summary>
    [HttpPost("{id:int}/rechazar")]
    [RequirePermiso("ScanAI.Confirmar")]
    public async Task<IActionResult> Rechazar(int id, [FromBody] RechazarScanRequest? body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.RechazarAsync(eid, id, body?.Motivo, _currentUser.Username, ct), "Escaneo rechazado.");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);

    public class RechazarScanRequest
    {
        public string? Motivo { get; set; }
    }
}
