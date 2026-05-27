using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Clientes;

/// <summary>
/// Receptor de DTE registrado por la empresa.
/// Puede ser consumidor final (sin NRC ni actividad económica) o contribuyente.
/// </summary>
public class Cliente : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Código del catálogo TIPO_DOC_IDENTIDAD: DUI, NIT, PASAPORTE, CARNET_RESIDENTE, OTRO.</summary>
    public string TipoDocumentoCodigo { get; set; } = "DUI";
    public string NumeroDocumento { get; set; } = null!;
    public string? Nrc { get; set; }

    public string Nombre { get; set; } = null!;
    public string? NombreComercial { get; set; }

    /// <summary>Código del catálogo TIPO_CONTRIBUYENTE: CONSUMIDOR_FINAL, CONTRIBUYENTE, GRAN_CONTRIBUYENTE.</summary>
    public string TipoContribuyenteCodigo { get; set; } = "CONSUMIDOR_FINAL";

    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }

    public string? DepartamentoCodigo { get; set; }
    public string? MunicipioCodigo { get; set; }
    public string? Direccion { get; set; }

    public string? Correo { get; set; }
    public string? Telefono { get; set; }

    public string EstadoCodigo { get; set; } = "ACTIVO";

    public bool EsContribuyente
        => TipoContribuyenteCodigo == "CONTRIBUYENTE" || TipoContribuyenteCodigo == "GRAN_CONTRIBUYENTE";
}
