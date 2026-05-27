using Microsoft.AspNetCore.Authorization;

namespace NeoSTP.Api.Authorization;

public class PermisoRequirement : IAuthorizationRequirement
{
    public PermisoRequirement(string codigo)
    {
        Codigo = codigo;
    }

    public string Codigo { get; }
}
