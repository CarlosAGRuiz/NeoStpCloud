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

    [HttpPost("documentos/{id:int}/firmar")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> Firmar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.FirmarAsync(eid, id, _currentUser.Username, ct));
    }

    [HttpPost("documentos/{id:int}/enviar")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> Enviar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.EnviarAsync(eid, id, _currentUser.Username, ct));
    }

    [HttpPost("documentos/{id:int}/invalidar")]
    [RequirePermiso("DTE.Invalidar")]
    public async Task<IActionResult> Invalidar(int id, [FromQuery] int? empresaId, [FromBody] InvalidarRequest body, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.InvalidarAsync(eid, id, body?.Motivo, _currentUser.Username, ct),
            "Documento invalidado.");
    }

    [HttpPost("evento/contingencia")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> EventoContingencia([FromBody] EventoContingenciaRequest body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.TransmitirEventoContingenciaAsync(
            eid, body.DocumentoIds ?? new List<int>(), body.TipoContingencia, body.Motivo,
            body.NombreResponsable, body.TipoDocResponsable, body.NumeroDocResponsable,
            _currentUser.Username, ct));
    }

    [HttpPost("evento/invalidacion")]
    [RequirePermiso("DTE.Invalidar")]
    public async Task<IActionResult> EventoInvalidacion([FromBody] EventoInvalidacionRequest body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.TransmitirInvalidacionEventoAsync(
            eid, body.DocumentoId, body.TipoAnulacion, body.MotivoAnulacion, body.CodigoGeneracionReemplazo,
            body.NombreResponsable, body.TipoDocResponsable, body.NumDocResponsable, _currentUser.Username, ct));
    }

    [HttpPost("evento/operaciones-especiales")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> EventoOperacionesEspeciales([FromBody] EventoOpEspecialesRequest body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.TransmitirEventoOperacionesEspecialesAsync(
            eid, body.CodigoGeneracionRef, body.Descripcion, body.Monto, _currentUser.Username, ct));
    }

    // ---- descarga y reenvío (Sprint 7) ----

    [HttpGet("documentos/{id:int}/pdf")]
    [RequirePermiso("DTE.Consultar")]
    public async Task<IActionResult> DescargarPdf(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var result = await _service.ObtenerArchivosAsync(eid, id, ct);
        if (result.IsFailure)
            return NotFound(Shared.ApiResponse.Fail(result.Error ?? "No encontrado", new[] { result.ErrorCode ?? "DTE_NOT_FOUND" }, HttpContext.TraceIdentifier));
        return File(result.Value!.PdfContent, "application/pdf", result.Value!.PdfFileName);
    }

    [HttpGet("documentos/{id:int}/json")]
    [RequirePermiso("DTE.Consultar")]
    public async Task<IActionResult> DescargarJson(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var result = await _service.ObtenerArchivosAsync(eid, id, ct);
        if (result.IsFailure)
            return NotFound(Shared.ApiResponse.Fail(result.Error ?? "No encontrado", new[] { result.ErrorCode ?? "DTE_NOT_FOUND" }, HttpContext.TraceIdentifier));
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Value!.JsonContent ?? string.Empty);
        return File(bytes, "application/json", result.Value!.JsonFileName);
    }

    [HttpPost("documentos/{id:int}/reenviar")]
    [RequirePermiso("DTE.Reenviar")]
    public async Task<IActionResult> Reenviar(int id, [FromQuery] int? empresaId, [FromBody] ReenviarRequest body, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ReenviarPorCorreoAsync(eid, id, body?.Destinatario, _currentUser.Username, ct));
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

    public class ReenviarRequest
    {
        public string? Destinatario { get; set; }
    }

    public class EventoContingenciaRequest
    {
        public List<int>? DocumentoIds { get; set; }
        public int TipoContingencia { get; set; } = 1;
        public string? Motivo { get; set; }
        public string NombreResponsable { get; set; } = null!;
        public string TipoDocResponsable { get; set; } = "36";
        public string NumeroDocResponsable { get; set; } = null!;
    }

    public class EventoInvalidacionRequest
    {
        public int DocumentoId { get; set; }
        public int TipoAnulacion { get; set; } = 2;
        public string? MotivoAnulacion { get; set; }
        public string? CodigoGeneracionReemplazo { get; set; }
        public string NombreResponsable { get; set; } = null!;
        public string TipoDocResponsable { get; set; } = "36";
        public string NumDocResponsable { get; set; } = null!;
    }

    public class EventoOpEspecialesRequest
    {
        public string? CodigoGeneracionRef { get; set; }
        public string Descripcion { get; set; } = "Operación especial";
        public decimal Monto { get; set; } = 10m;
    }
}
