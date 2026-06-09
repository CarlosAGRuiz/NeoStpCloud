using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Pos;

/// <summary>
/// Configuración de una impresora de tickets de la empresa. Define cómo se imprime:
/// por el navegador, enviando ESC/POS a una IP de red, o delegando a la app móvil.
/// </summary>
public class ImpresoraPos : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? PuntoVentaId { get; set; }

    public string Nombre { get; set; } = null!;

    /// <summary>NAVEGADOR | RED | APP.</summary>
    public string Conexion { get; set; } = ConexionImpresora.Navegador;

    /// <summary>Ancho del papel térmico en mm (58 u 80).</summary>
    public int AnchoMm { get; set; } = 80;

    /// <summary>IP de la impresora (sólo conexión RED).</summary>
    public string? Ip { get; set; }
    public int Puerto { get; set; } = 9100;

    /// <summary>Corte automático de papel al final (ESC/POS).</summary>
    public bool CorteAutomatico { get; set; } = true;

    public bool EsPredeterminada { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVA";
    public string? Notas { get; set; }
}

public static class ConexionImpresora
{
    public const string Navegador = "NAVEGADOR";
    public const string Red = "RED";
    public const string App = "APP";

    public static readonly string[] All = [Navegador, Red, App];
}

public static class EstadosImpresora
{
    public const string Activa = "ACTIVA";
    public const string Inactiva = "INACTIVA";
}
