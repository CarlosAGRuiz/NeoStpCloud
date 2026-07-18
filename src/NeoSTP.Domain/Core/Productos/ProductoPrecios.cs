using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Productos;

/// <summary>
/// Escala de precio por volumen: a partir de <see cref="CantidadMinima"/> unidades,
/// el precio unitario baja a <see cref="PrecioUnitario"/> (ferretería, mayoreo).
/// </summary>
public class ProductoPrecioEscala : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public decimal CantidadMinima { get; set; }
    public decimal PrecioUnitario { get; set; }
}

/// <summary>
/// Unidad alternativa de venta (docena, caja, fardo) con factor de conversión a la
/// unidad base y precio propio opcional (null = precio base × factor).
/// </summary>
public class ProductoUnidadAlternativa : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    /// <summary>Código MH CAT-014 de la unidad alternativa (58=Docena, …).</summary>
    public string UnidadMedidaCodigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;

    /// <summary>Unidades base que contiene la unidad alternativa (docena = 12).</summary>
    public decimal Factor { get; set; }

    /// <summary>Precio de la unidad alternativa; null = precio base × factor.</summary>
    public decimal? PrecioUnitario { get; set; }
}
