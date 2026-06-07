using NeoSTP.Application.Common;

namespace NeoSTP.Application.Auth.Abstractions;

/// <summary>Valida que una contraseña cumpla la política de complejidad configurada (M6.2).</summary>
public interface IPasswordPolicy
{
    /// <summary>Ok si cumple; Fail con código PWD_WEAK y la lista de requisitos faltantes.</summary>
    Result Validate(string? password);

    /// <summary>Descripción legible de los requisitos (para mostrar en formularios).</summary>
    string Describe();
}
