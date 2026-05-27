using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/dte")]
public class DteController : ApiControllerBase
{
    private readonly IDteDocumentosService _service;
    private readonly ICurrentUser _currentUser;

    public DteController(IDteDocumentosService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // ---- listado y detalle ----

    [HttpGet("documentos")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> List([FromQuery] DteListQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetListAsync(eid, query, ct));
    }

    [HttpGet("documentos/{id:int}")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> GetById(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetByIdAsync(eid, id, ct));
    }

    // ---- creación por tipo ----

    [HttpPost("factura")]
    [RequirePermiso("DTE.Emitir")]
    public Task<IActionResult> CrearFactura([FromBody] CreateDteDocumentoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
        => CrearConTipo(req, TipoDteCodigos.FacturaConsumidorFinal, empresaId, ct);

    [HttpPost("credito-fiscal")]
    [RequirePermiso("DTE.Emitir")]
    public Task<IActionResult> CrearCcf([FromBody] CreateDteDocumentoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
        => CrearConTipo(req, TipoDteCodigos.ComprobanteCreditoFiscal, empresaId, ct);

    [HttpPost("nota-credito")]
    [RequirePermiso("DTE.Emitir")]
    public Task<IActionResult> CrearNotaCredito([FromBody] CreateDteDocumentoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
        => CrearConTipo(req, TipoDteCodigos.NotaCredito, empresaId, ct);

    [HttpPost("nota-debito")]
    [RequirePermiso("DTE.Emitir")]
    public Task<IActionResult> CrearNotaDebito([FromBody] CreateDteDocumentoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
        => CrearConTipo(req, TipoDteCodigos.NotaDebito, empresaId, ct);

    [HttpPost("sujeto-excluido")]
    [RequirePermiso("DTE.Emitir")]
    public Task<IActionResult> CrearSujetoExcluido([FromBody] CreateDteDocumentoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
        => CrearConTipo(req, TipoDteCodigos.FacturaSujetoExcluido, empresaId, ct);

    [HttpPost("documentos")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> CrearGenerico([FromBody] CreateDteDocumentoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CreateBorradorAsync(eid, req, _currentUser.Username, ct));
    }

    // ---- transiciones de estado ----

    [HttpPost("documentos/{id:int}/generar")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> Generar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GenerarAsync(eid, id, _currentUser.Username, ct));
    }

    [HttpPost("documentos/{id:int}/validar")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> Validar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ValidarAsync(eid, id, _currentUser.Username, ct));
    }

    [HttpPost("documentos/{id:int}/invalidar")]
    [RequirePermiso("DTE.Invalidar")]
    public async Task<IActionResult> Invalidar(int id, [FromQuery] int? empresaId, [FromBody] InvalidarRequest body, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.InvalidarAsync(eid, id, body?.Motivo, _currentUser.Username, ct),
            "Documento invalidado.");
    }

    // ---- helpers ----

    private async Task<IActionResult> CrearConTipo(CreateDteDocumentoRequest req, string tipoForzado, int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        req.TipoDteCodigo = tipoForzado;
        return Respond(await _service.CreateBorradorAsync(eid, req, _currentUser.Username, ct));
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);

    public class InvalidarRequest
    {
        public string? Motivo { get; set; }
    }
}
