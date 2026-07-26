using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Auth;

/// <summary>
/// Exige que el plan de la empresa incluya el módulo indicado para entrar a la pantalla.
/// El menú ya oculta lo que no se contrató, pero ocultar no es bloquear: sin esto, escribir
/// la URL a mano daba acceso completo a módulos no comprados.
///
/// En vez de un 403 seco, muestra una pantalla que explica qué hace el módulo y en qué plan
/// viene — el bloqueo se convierte en una oportunidad de venta.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireModuloAttribute : Attribute, IAsyncAuthorizationFilter
{
    public RequireModuloAttribute(string codigo) => Codigo = codigo;

    public string Codigo { get; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var servicios = context.HttpContext.RequestServices;
        var currentUser = servicios.GetRequiredService<ICurrentUser>();

        // Sin sesión, deja actuar al filtro de autenticación (redirige al login).
        if (!currentUser.IsAuthenticated) return;
        if (currentUser.TipoUsuarioCodigo == "SUPERADMIN") return;

        var empresaContext = servicios.GetRequiredService<IEmpresaContext>();
        if (empresaContext.CurrentEmpresaId is not int empresaId)
        {
            context.Result = Bloquear(context, null);
            return;
        }

        var licencias = servicios.GetRequiredService<ILicenciaResolver>();
        var licencia = await licencias.ResolveAsync(empresaId, context.HttpContext.RequestAborted);

        var incluido = licencia is not null
            && licencia.Vigente
            && licencia.Modulos.Any(m => m.Activo && string.Equals(m.Codigo, Codigo, StringComparison.OrdinalIgnoreCase));

        if (!incluido) context.Result = Bloquear(context, licencia?.PlanNombre);
    }

    private ViewResult Bloquear(AuthorizationFilterContext context, string? planActual)
    {
        var info = ModuloCatalogo.Describir(Codigo);
        var viewData = new ViewDataDictionary(
            new EmptyModelMetadataProvider(), context.ModelState)
        {
            ["ModuloCodigo"] = Codigo,
            ["ModuloNombre"] = info.Nombre,
            ["ModuloDescripcion"] = info.Descripcion,
            ["ModuloIcono"] = info.Icono,
            ["PlanesQueLoIncluyen"] = info.Planes,
            ["PlanActual"] = planActual,
        };

        return new ViewResult
        {
            ViewName = "ModuloNoIncluido",
            ViewData = viewData,
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}

/// <summary>
/// Texto comercial de cada módulo para la pantalla de bloqueo: qué resuelve y desde qué
/// plan viene. Se mantiene aquí (y no en BD) porque es copy de venta, no configuración.
/// </summary>
public static class ModuloCatalogo
{
    public sealed record Info(string Nombre, string Descripcion, string Icono, string Planes);

    private static readonly Dictionary<string, Info> Mapa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NEOPOS"] = new("Punto de venta",
            "Cobra en mostrador con lector de código de barras, control de caja e impresión de tickets, y convierte la venta en factura electrónica en el acto.",
            "point_of_sale", "Pyme, Pro, Business Full y Enterprise"),
        ["INVENTARIO"] = new("Inventario",
            "Existencias por sucursal, kardex, costo promedio, stock mínimo, traslados entre sucursales y control de lotes y vencimientos.",
            "inventory_2", "Pro, Business Full y Enterprise"),
        ["COMPRAS"] = new("Compras y cuentas por pagar",
            "Proveedores, órdenes de compra con aprobación por monto, recepciones parciales y control de lo que debes.",
            "shopping_cart", "Business Full y Enterprise"),
        ["NEOCRM"] = new("CRM",
            "Contactos, oportunidades por etapa, actividades de seguimiento y cotizaciones que se convierten en factura.",
            "handshake", "Pro, Business Full y Enterprise"),
        ["NEOAGENDA"] = new("Agenda de citas",
            "Calendario por empleado sin traslapes, precio congelado al agendar y cálculo de comisiones por servicio.",
            "calendar_month", "Business Full y Enterprise"),
        ["NEOTESORERIA"] = new("Tesorería",
            "Cuentas de banco y caja, movimientos de entrada y salida, y conciliación bancaria.",
            "account_balance", "Business Full y Enterprise"),
        ["NEOCONTA"] = new("Contabilidad",
            "Asientos contables automáticos desde tus documentos, balanza de comprobación y reversas trazables.",
            "calculate", "Business Full y Enterprise"),
        ["NEOBI"] = new("Libros fiscales y reportes",
            "Libros de ventas a consumidor y contribuyentes, libro de compras y resumen para el F-07, exportables a CSV.",
            "insights", "Contador, Business Full y Enterprise"),
        ["NEORRHH"] = new("Recursos humanos y planilla",
            "Empleados, planillas con cálculo de ISSS, AFP y renta, prestaciones y recibos en PDF.",
            "badge", "Business Full y Enterprise"),
        ["NEOSCANAI"] = new("NeoScan AI",
            "Fotografía una factura de proveedor y el sistema extrae los datos para registrarla como gasto o compra.",
            "document_scanner", "Pro, Business Full y Enterprise"),
        ["NEOPROFIT"] = new("NeoProfit",
            "Cuánto vendiste, cuánto te costó y cuánto te quedó, con gastos y compras integrados.",
            "trending_up", "Business Full y Enterprise"),
        ["NEOPORTAL"] = new("Portal de clientes",
            "Enlaces firmados para que tus clientes consulten y descarguen sus documentos sin cuenta.",
            "public", "Enterprise"),
        ["NEOCONNECT"] = new("NeoConnect (API)",
            "API con llaves y webhooks para integrar tu ERP o tu tienda en línea con la facturación.",
            "api", "Integrador API y Enterprise"),
        ["EVENTOSDTE"] = new("Eventos DTE",
            "Anulación e invalidación de documentos ante Hacienda con su respaldo y trazabilidad.",
            "event_note", "Business Full y Enterprise"),
        ["CONTINGENCIA"] = new("Contingencia",
            "Sigue facturando sin conexión o con Hacienda caída, y transmite después por lotes de forma automática.",
            "cloud_off", "Pro, Business Full y Enterprise"),
    };

    public static Info Describir(string codigo) =>
        Mapa.TryGetValue(codigo, out var info)
            ? info
            : new Info(codigo, "Este módulo no está incluido en tu plan actual.", "lock", "planes superiores");
}
