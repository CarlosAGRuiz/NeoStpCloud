namespace NeoSTP.Application.Auth;

/// <summary>
/// Configuración global de SSO OIDC (sección "Sso"). El SaaS registra UNA app
/// multi-tenant de Microsoft Entra y UN cliente de Google; cada empresa mapea su
/// dominio de correo a su cuenta (ver EmpresaSso). Deshabilitado por defecto:
/// mientras <see cref="Enabled"/> sea false no se registran esquemas OIDC.
/// </summary>
public sealed class SsoOptions
{
    public const string SectionName = "Sso";

    public bool Enabled { get; set; }

    public SsoProviderOptions Microsoft { get; set; } = new()
    {
        // Endpoint común multi-tenant de Entra (v2.0). La restricción por directorio
        // se hace por empresa con EmpresaSso.TenantIdExterno.
        Authority = "https://login.microsoftonline.com/organizations/v2.0",
    };

    public SsoProviderOptions Google { get; set; } = new()
    {
        Authority = "https://accounts.google.com",
    };

    /// <summary>True si al menos un proveedor tiene ClientId configurado y el SSO está habilitado.</summary>
    public bool AnyProviderConfigured =>
        Enabled && (Microsoft.IsConfigured || Google.IsConfigured);
}

public sealed class SsoProviderOptions
{
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
