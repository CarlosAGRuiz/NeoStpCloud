using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Bitácora de auditoría consultable (M3.4). Filtros + paginación + export CSV.
/// Permiso Core.Auditoria.Ver. Empresa: cada usuario ve la auditoría de su empresa;
/// SuperAdmin ve todas (o la empresa seleccionada en modo soporte).
/// </summary>
[Authorize]
[Route("[controller]")]
public class AuditoriaController : Controller
{
    public static readonly string[] Resultados = ["OK", "ERROR", "DENEGADO"];

    private readonly IAuditoriaQueryService _auditoria;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public AuditoriaController(IAuditoriaQueryService auditoria, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _auditoria = auditoria;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AuditoriaFiltro filtro, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Core.Auditoria.Ver")) return Forbid();
        if (!ResolverEmpresa(out var empresaId)) return RedirectToAction("Index", "Home");

        var query = Construir(filtro, empresaId, page);
        var resultado = await _auditoria.ListAsync(query, ct);

        return View(new AuditoriaIndexViewModel
        {
            Registros = resultado,
            Modulos = await _auditoria.GetModulosAsync(empresaId, ct),
            Resultados = Resultados,
            Filtro = filtro,
            Page = query.Page,
            EsSuperAdmin = EsSuperAdmin,
        });
    }

    [HttpGet("Export")]
    public async Task<IActionResult> Export([FromQuery] AuditoriaFiltro filtro, CancellationToken ct = default)
    {
        if (!Has("Core.Auditoria.Ver")) return Forbid();
        if (!ResolverEmpresa(out var empresaId)) return RedirectToAction("Index", "Home");

        var query = Construir(filtro, empresaId, 1);
        var filas = await _auditoria.ExportAsync(query, ct: ct);

        var sb = new StringBuilder();
        sb.AppendLine("Fecha,Empresa,Usuario,Modulo,Accion,Entidad,EntidadId,Resultado,IP,Detalle");
        foreach (var a in filas)
        {
            sb.Append(Csv(a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',')
              .Append(Csv(a.EmpresaId?.ToString())).Append(',')
              .Append(Csv(a.Username)).Append(',')
              .Append(Csv(a.Modulo)).Append(',')
              .Append(Csv(a.Accion)).Append(',')
              .Append(Csv(a.Entidad)).Append(',')
              .Append(Csv(a.EntidadId)).Append(',')
              .Append(Csv(a.Resultado)).Append(',')
              .Append(Csv(a.IpAddress)).Append(',')
              .Append(Csv(a.Detalle)).AppendLine();
        }

        var bytes = new UTF8Encoding(true).GetBytes(sb.ToString()); // BOM para Excel
        return File(bytes, "text/csv", $"auditoria_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
    }

    private AuditoriaQuery Construir(AuditoriaFiltro f, int? empresaId, int page) => new()
    {
        EmpresaId = empresaId,
        Search = f.Search,
        Modulo = f.Modulo,
        Accion = f.Accion,
        Username = f.Username,
        Resultado = f.Resultado,
        Desde = f.Desde,
        Hasta = f.Hasta,
        Page = page <= 0 ? 1 : page,
        PageSize = 30,
    };

    /// <summary>
    /// Determina el filtro de empresa según rol. SuperAdmin: empresa de soporte o todas (null).
    /// Otros: su empresa obligatoriamente (false si no se puede determinar).
    /// </summary>
    private bool ResolverEmpresa(out int? empresaId)
    {
        empresaId = _empresaContext.CurrentEmpresaId;
        if (EsSuperAdmin) return true;          // null = todas no aplica; será su empresa o nada
        return empresaId is not null;            // empresa user sin empresa => bloquear
    }

    private bool EsSuperAdmin => _currentUser.TipoUsuarioCodigo == "SUPERADMIN";

    private bool Has(string codigo) => EsSuperAdmin || _currentUser.HasPermiso(codigo);

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var v = value.Replace("\"", "\"\"");
        return $"\"{v}\"";
    }
}

/// <summary>Filtros enviados por querystring (bind plano para reusarlos en Index y Export).</summary>
public sealed class AuditoriaFiltro
{
    public string? Search { get; set; }
    public string? Modulo { get; set; }
    public string? Accion { get; set; }
    public string? Username { get; set; }
    public string? Resultado { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

public class AuditoriaIndexViewModel
{
    public PagedResult<AuditoriaDto> Registros { get; set; }
        = PagedResult<AuditoriaDto>.Create(Array.Empty<AuditoriaDto>(), 0, 1, 30);
    public IReadOnlyList<string> Modulos { get; set; } = Array.Empty<string>();
    public string[] Resultados { get; set; } = Array.Empty<string>();
    public AuditoriaFiltro Filtro { get; set; } = new();
    public int Page { get; set; } = 1;
    public bool EsSuperAdmin { get; set; }
}
