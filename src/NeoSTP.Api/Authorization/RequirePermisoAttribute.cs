using Microsoft.AspNetCore.Authorization;

namespace NeoSTP.Api.Authorization;

/// <summary>
/// Marca un endpoint como autorizado solo si el usuario tiene el permiso indicado.
/// Internamente crea una policy "permiso:{Codigo}" que valida el claim 'permiso'.
/// Los SuperAdmin siempre pasan.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequirePermisoAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "permiso:";

    public RequirePermisoAttribute(string codigo) : base($"{PolicyPrefix}{codigo}")
    {
        Codigo = codigo;
    }

    public string Codigo { get; }
}
