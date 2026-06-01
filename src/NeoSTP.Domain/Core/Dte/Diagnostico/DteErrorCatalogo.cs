using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Diagnostico;

/// <summary>
/// Catálogo de errores conocidos de Hacienda y errores internos del sistema.
/// Permite mostrar al usuario una explicación clara, causa probable y acción sugerida.
/// </summary>
public class DteErrorCatalogo : AuditableEntity
{
    /// <summary>Código del error: ej. 095, 096, 802, FIRMA_FAILED, HACIENDA_AUTH_FAILED.</summary>
    public string Codigo { get; set; } = null!;

    /// <summary>HACIENDA | INTERNO | FIRMA</summary>
    public string Tipo { get; set; } = DteErrorTipo.Hacienda;

    /// <summary>Mensaje técnico tal como lo devuelve Hacienda o el sistema.</summary>
    public string MensajeTecnico { get; set; } = null!;

    /// <summary>Descripción amigable para el usuario.</summary>
    public string Descripcion { get; set; } = null!;

    /// <summary>Causa probable del error.</summary>
    public string CausaProbable { get; set; } = null!;

    /// <summary>Acción sugerida para corregirlo.</summary>
    public string AccionSugerida { get; set; } = null!;

    /// <summary>Nivel de severidad: ERROR | WARNING | INFO</summary>
    public string Severidad { get; set; } = "ERROR";

    public bool Activo { get; set; } = true;
}

public static class DteErrorTipo
{
    public const string Hacienda = "HACIENDA";
    public const string Interno = "INTERNO";
    public const string Firma = "FIRMA";
}
