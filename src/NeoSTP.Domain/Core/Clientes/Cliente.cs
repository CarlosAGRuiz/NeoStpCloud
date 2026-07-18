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
    /// <summary>Opcional para clientes extranjeros (pasaporte u otro documento del país de origen).</summary>
    public string? NumeroDocumento { get; set; }
    public string? Nrc { get; set; }

    public string Nombre { get; set; } = null!;
    public string? NombreComercial { get; set; }

    /// <summary>Código del catálogo TIPO_CONTRIBUYENTE: CONSUMIDOR_FINAL, CONTRIBUYENTE, GRAN_CONTRIBUYENTE.</summary>
    public string TipoContribuyenteCodigo { get; set; } = "CONSUMIDOR_FINAL";

    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }

    public string? DepartamentoCodigo { get; set; }
    public string? MunicipioCodigo { get; set; }
    /// <summary>Código de Distrito (CAT-008, división territorial 2024). Requerido para DTE v2/v4.</summary>
    public string? DistritoCodigo { get; set; }
    public string? Direccion { get; set; }

    public string? Correo { get; set; }
    public string? Telefono { get; set; }

    /// <summary>
    /// País de residencia: código del catálogo PAIS (CAT-020, coincide con el código MH,
    /// p. ej. 9300 El Salvador). Null equivale a El Salvador.
    /// </summary>
    public string? PaisCodigo { get; set; }

    /// <summary>Tipo de persona (CAT-029): 1 natural, 2 jurídica. Requerido por la factura de exportación.</summary>
    public int? TipoPersona { get; set; }

    public string EstadoCodigo { get; set; } = "ACTIVO";

    /// <summary>Etiqueta operativa del CRM: VIP | FRECUENTE | null. "Moroso" se deriva de cobranza.</summary>
    public string? Etiqueta { get; set; }

    public bool EsContribuyente
        => TipoContribuyenteCodigo == "CONTRIBUYENTE" || TipoContribuyenteCodigo == "GRAN_CONTRIBUYENTE";

    public bool EsExtranjero
        => !string.IsNullOrEmpty(PaisCodigo) && PaisCodigo != PaisCodigos.ElSalvador;
}

public static class PaisCodigos
{
    /// <summary>Código MH de El Salvador en el CAT-020.</summary>
    public const string ElSalvador = "9300";
}
