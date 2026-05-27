using Microsoft.AspNetCore.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Api.Authorization;

public class ModuloRequirement : IAuthorizationRequirement
{
    public ModuloRequirement(string codigo) { Codigo = codigo; }
    public string Codigo { get; }
}

public class ModuloAuthorizationHandler : AuthorizationHandler<ModuloRequirement>
{
    private readonly ICurrentUser _currentUser;
    private readonly ILicenciaResolver _licencias;

    public ModuloAuthorizationHandler(ICurrentUser currentUser, ILicenciaResolver licencias)
    {
        _currentUser = currentUser;
        _licencias = licencias;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ModuloRequirement requirement)
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            context.Succeed(requirement);
            return;
        }

        if (_currentUser.EmpresaId is not int empresaId)
        {
            return; // no empresa: no se autoriza
        }

        var licencia = await _licencias.ResolveAsync(empresaId);
        if (licencia is null || !licencia.Vigente) return;

        var modulo = licencia.Modulos.FirstOrDefault(m =>
            string.Equals(m.Codigo, requirement.Codigo, StringComparison.OrdinalIgnoreCase));
        if (modulo is null || !modulo.Activo) return;

        context.Succeed(requirement);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireModuleAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "modulo:";

    public RequireModuleAttribute(string codigo) : base($"{PolicyPrefix}{codigo}")
    {
        Codigo = codigo;
    }

    public string Codigo { get; }
}
