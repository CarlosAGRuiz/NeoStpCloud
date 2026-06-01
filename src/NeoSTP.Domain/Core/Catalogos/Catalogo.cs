using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Catalogos;

/// <summary>
/// Catálogo genérico reutilizable: tipos de factura, monedas, estados, formas de pago, etc.
/// Si EmpresaId es null, el catálogo es global del sistema.
/// </summary>
public class Catalogo : AuditableEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; } = true;
    public int? EmpresaId { get; set; }

    /// <summary>
    /// Versión del catálogo. Permite mantener históricos cuando MH publica una
    /// nueva versión de un catálogo oficial (CAT-013 v2, etc.) sin sobrescribir
    /// la anterior. Default: 1.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>JSON libre con metadatos del catálogo (origen, fuente MH, fechas, etc.).</summary>
    public string? MetadataJson { get; set; }

    public ICollection<CatalogoItem> Items { get; set; } = new List<CatalogoItem>();
}
