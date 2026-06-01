namespace NeoSTP.Application.Legal;

/// <summary>
/// Constantes para los tipos de consentimiento legal registrables.
/// </summary>
public static class ConsentType
{
    public const string Terms   = "TERMS";
    public const string Privacy = "PRIVACY";
    public const string Cookies = "COOKIES";
    public const string Dpa     = "DPA";

    public static readonly IReadOnlyList<string> All = [Terms, Privacy, Cookies, Dpa];
}
