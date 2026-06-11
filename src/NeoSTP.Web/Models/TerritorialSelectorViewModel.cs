using NeoSTP.Application.Lookups;

namespace NeoSTP.Web.Models;

/// <summary>
/// Selector Departamento→Municipio reutilizable (partial _SelectorTerritorial).
/// Guarda el NOMBRE (no el código) para ser compatible con entidades que ya
/// persisten texto libre (Empresa, Sucursal); la cascada se hace por código MH
/// vía data-attributes.
/// </summary>
public class TerritorialSelectorViewModel
{
    public string CampoDepartamento { get; set; } = "Departamento";
    public string CampoMunicipio { get; set; } = "Municipio";

    /// <summary>true = el value de las opciones es el código MH (Sucursales); false = el nombre (Empresas).</summary>
    public bool PorCodigo { get; set; }
    public string? ValorDepartamento { get; set; }
    public string? ValorMunicipio { get; set; }
    public IReadOnlyList<LookupItem> Departamentos { get; set; } = [];
    public IReadOnlyList<LookupItem> Municipios { get; set; } = [];
}
