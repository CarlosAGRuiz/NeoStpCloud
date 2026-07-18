using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Clientes.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Productos.Dtos;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NeoConnect API pública v1 — consumida por integraciones externas mediante API Key
/// (header <c>X-Api-Key</c>). Cada endpoint exige un scope concreto de la key.
/// El ambiente (sandbox PRUEBAS / PRODUCCION) lo determina la configuración DTE de la empresa.
/// </summary>
[AllowAnonymous]
[Route("api/v1")]
[Produces("application/json")]
public class ConnectApiV1Controller : ConnectApiControllerBase
{
    private readonly IConnectDteService _connectDte;
    private readonly IDteDocumentosService _dte;
    private readonly IClientesService _clientes;
    private readonly IProductosService _productos;

    public ConnectApiV1Controller(
        IConnectDteService connectDte,
        IDteDocumentosService dte,
        IClientesService clientes,
        IProductosService productos)
    {
        _connectDte = connectDte;
        _dte = dte;
        _clientes = clientes;
        _productos = productos;
    }

    // ─── Ping / sandbox ─────────────────────────────────────────────────────────

    /// <summary>Verifica la API Key y devuelve la empresa y los scopes autorizados.</summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        if (ApiKey is not { } ctx)
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail(
                "Envía tu API Key en el header X-Api-Key.", new[] { "APIKEY_REQUIRED" }, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<object>.Ok(new
        {
            empresaId = ctx.EmpresaId,
            scopes = ctx.Scopes,
            ok = true,
        }, traceId: HttpContext.TraceIdentifier));
    }

    // ─── DTE ─────────────────────────────────────────────────────────────────────

    /// <summary>Emite un DTE de extremo a extremo (borrador → generar → validar → firmar → enviar).</summary>
    [HttpPost("dte")]
    public async Task<IActionResult> EmitirDte([FromBody] CreateDteDocumentoRequest req, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.DteWrite, out var eid, out var error)) return error!;
        return Respond(await _connectDte.EmitirAsync(eid, req, Actor, ct));
    }

    /// <summary>Lista los DTE de la empresa.</summary>
    [HttpGet("dte")]
    public async Task<IActionResult> ListarDte([FromQuery] DteListQuery query, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.DteRead, out var eid, out var error)) return error!;
        return Respond(await _dte.GetListAsync(eid, query, ct));
    }

    /// <summary>Consulta el estado y detalle de un DTE.</summary>
    [HttpGet("dte/{id:int}")]
    public async Task<IActionResult> ObtenerDte(int id, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.DteRead, out var eid, out var error)) return error!;
        return Respond(await _dte.GetByIdAsync(eid, id, ct));
    }

    /// <summary>Descarga el PDF del DTE.</summary>
    [HttpGet("dte/{id:int}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DescargarPdf(int id, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.DteRead, out var eid, out var error)) return error!;
        var result = await _dte.ObtenerArchivosAsync(eid, id, ct);
        if (result.IsFailure)
            return NotFound(ApiResponse.Fail(result.Error ?? "No encontrado",
                new[] { result.ErrorCode ?? "DTE_NOT_FOUND" }, HttpContext.TraceIdentifier));
        return File(result.Value!.PdfContent, "application/pdf", result.Value!.PdfFileName);
    }

    /// <summary>Descarga el JSON sellado del DTE.</summary>
    [HttpGet("dte/{id:int}/json")]
    [Produces("application/json")]
    public async Task<IActionResult> DescargarJson(int id, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.DteRead, out var eid, out var error)) return error!;
        var result = await _dte.ObtenerArchivosAsync(eid, id, ct);
        if (result.IsFailure)
            return NotFound(ApiResponse.Fail(result.Error ?? "No encontrado",
                new[] { result.ErrorCode ?? "DTE_NOT_FOUND" }, HttpContext.TraceIdentifier));
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Value!.JsonContent ?? string.Empty);
        return File(bytes, "application/json", result.Value!.JsonFileName);
    }

    // ─── Clientes ─────────────────────────────────────────────────────────────────

    /// <summary>Lista los clientes de la empresa.</summary>
    [HttpGet("clientes")]
    public async Task<IActionResult> ListarClientes([FromQuery] PagedQuery query, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.ClientesRead, out var eid, out var error)) return error!;
        return Respond(await _clientes.GetListAsync(eid, query, ct));
    }

    /// <summary>Da de alta un cliente.</summary>
    [HttpPost("clientes")]
    public async Task<IActionResult> CrearCliente([FromBody] CreateClienteRequest req, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.ClientesWrite, out var eid, out var error)) return error!;
        return Respond(await _clientes.CreateAsync(eid, req, Actor, ct));
    }

    // ─── Productos ──────────────────────────────────────────────────────────────

    /// <summary>Lista los productos de la empresa.</summary>
    [HttpGet("productos")]
    public async Task<IActionResult> ListarProductos([FromQuery] PagedQuery query, [FromQuery] string? categoria, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.ProductosRead, out var eid, out var error)) return error!;
        return Respond(await _productos.GetListAsync(eid, query, categoria, ct));
    }

    /// <summary>Da de alta un producto.</summary>
    [HttpPost("productos")]
    public async Task<IActionResult> CrearProducto([FromBody] CreateProductoRequest req, CancellationToken ct)
    {
        if (!TryAuthorize(ConnectScopes.ProductosWrite, out var eid, out var error)) return error!;
        return Respond(await _productos.CreateAsync(eid, req, Actor, ct));
    }
}
