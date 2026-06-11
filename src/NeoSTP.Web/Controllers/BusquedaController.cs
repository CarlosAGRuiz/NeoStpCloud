using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Lookups;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Buscador global (Ctrl+K): clientes, productos y DTE en una sola consulta.
/// Cada grupo se incluye solo si el usuario tiene el permiso correspondiente.
/// </summary>
[Authorize]
[Route("busqueda")]
public class BusquedaController : Controller
{
    private readonly ILookupService _lookups;
    private readonly IDteDocumentosService _dte;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public BusquedaController(ILookupService lookups, IDteDocumentosService dte, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _lookups = lookups;
        _dte = dte;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("global")]
    public async Task<IActionResult> Global([FromQuery] string? q, CancellationToken ct)
    {
        if (_empresaContext.CurrentEmpresaId is not int eid) return Json(Array.Empty<object>());
        q = q?.Trim();
        if (string.IsNullOrEmpty(q) || q.Length < 2) return Json(Array.Empty<object>());

        var resultados = new List<object>();

        if (Has("Clientes.Ver"))
        {
            var clientes = await _lookups.BuscarClientesAsync(eid, q, 5, ct);
            resultados.AddRange(clientes.Select(c => new
            {
                grupo = "Clientes",
                icono = "person",
                titulo = c.Label,
                detalle = $"{c.Parent} {c.Meta}",
                url = Url.Action("Edit", "Clientes", new { id = c.Value }),
            }));
        }

        if (Has("Productos.Ver"))
        {
            var productos = await _lookups.BuscarProductosAsync(eid, q, 5, ct);
            resultados.AddRange(productos.Select(p => new
            {
                grupo = "Productos",
                icono = "inventory_2",
                titulo = p.Label,
                detalle = $"{p.Parent} · ${p.Meta}",
                url = Url.Action("Edit", "Productos", new { id = p.Value }),
            }));
        }

        if (Has("DTE.Consultar"))
        {
            var dtes = await _dte.GetListAsync(eid, new DteListQuery { Search = q, PageSize = 5 }, ct);
            if (dtes.IsSuccess)
            {
                resultados.AddRange(dtes.Value!.Items.Select(d => new
                {
                    grupo = "DTE",
                    icono = "receipt_long",
                    titulo = d.NumeroControl,
                    detalle = $"{d.ReceptorNombre} · ${d.TotalPagar:N2} · {d.EstadoCodigo}",
                    url = Url.Action("Details", "DteDocumentos", new { id = d.Id }),
                }));
            }
        }

        return Json(resultados);
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
}
