namespace NeoSTP.Application.Auth.Dtos;

/// <summary>Petición que solo lleva un código TOTP o de recuperación.</summary>
public class MfaCodeRequest
{
    public string Code { get; set; } = null!;
}
