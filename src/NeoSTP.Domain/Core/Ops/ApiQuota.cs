using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Ops;

/// <summary>
/// Definición de una cuota de peticiones (rate limit) aplicable a un ámbito.
/// El ámbito puede ser global, por empresa, plan, usuario, API key o módulo.
/// La cuota limita <see cref="LimitePeticiones"/> en una ventana deslizante de
/// <see cref="VentanaSegundos"/>; al exceder, la petición recibe HTTP 429.
/// </summary>
public class ApiQuota : AuditableEntity
{
    /// <summary>Empresa dueña de la regla. Null = regla de sistema / default global.</summary>
    public int? EmpresaId { get; set; }

    /// <summary>GLOBAL | EMPRESA | PLAN | USUARIO | APIKEY | MODULO</summary>
    public string Ambito { get; set; } = ApiQuotaAmbito.Global;

    /// <summary>
    /// Referencia del ámbito (ej. planId, usuarioId, apiKeyId, código de módulo).
    /// Null cuando el ámbito es GLOBAL o cuando aplica a toda la empresa.
    /// </summary>
    public string? AmbitoRef { get; set; }

    /// <summary>Ventana de tiempo en segundos sobre la que se cuenta.</summary>
    public int VentanaSegundos { get; set; } = 60;

    /// <summary>Número máximo de peticiones permitidas dentro de la ventana.</summary>
    public int LimitePeticiones { get; set; }

    public bool Activo { get; set; } = true;

    public string? Descripcion { get; set; }
}

public static class ApiQuotaAmbito
{
    public const string Global = "GLOBAL";
    public const string Empresa = "EMPRESA";
    public const string Plan = "PLAN";
    public const string Usuario = "USUARIO";
    public const string ApiKey = "APIKEY";
    public const string Modulo = "MODULO";
}
