using FluentAssertions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Infrastructure.Auth;
using Xunit;

namespace NeoSTP.Tests.Unit.Auth;

/// <summary>M6.2 — política de complejidad de contraseña configurable.</summary>
public class PasswordPolicyTests
{
    private static PasswordPolicy Build(PasswordPolicyOptions? p = null)
        => new(Options.Create(new SecurityOptions { Password = p ?? new PasswordPolicyOptions() }));

    [Theory]
    [InlineData("Abcdef12")]      // 8, mayús, minús, dígito
    [InlineData("MiClave2026")]
    public void Validate_CumpleDefault_Ok(string pwd)
        => Build().Validate(pwd).IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("")]              // vacía
    [InlineData("abc1")]          // corta
    [InlineData("abcdefgh")]      // sin mayús ni dígito
    [InlineData("ABCDEFGH1")]     // sin minús
    [InlineData("Abcdefgh")]      // sin dígito
    public void Validate_NoCumpleDefault_FailConPwdWeak(string pwd)
    {
        var r = Build().Validate(pwd);
        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("PWD_WEAK");
        r.ValidationErrors.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_Null_Fail()
        => Build().Validate(null).IsFailure.Should().BeTrue();

    [Fact]
    public void Validate_RequiereSimbolo_CuandoSeConfigura()
    {
        var policy = Build(new PasswordPolicyOptions { RequireNonAlphanumeric = true });
        policy.Validate("Abcdef12").IsFailure.Should().BeTrue();
        policy.Validate("Abcdef12!").IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_PoliticaRelajada_PermiteSimple()
    {
        var policy = Build(new PasswordPolicyOptions
        {
            MinLength = 4, RequireUppercase = false, RequireLowercase = false, RequireDigit = false,
        });
        policy.Validate("abcd").IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Describe_IncluyeRequisitos()
    {
        var texto = Build().Describe();
        texto.Should().Contain("8");
        texto.Should().Contain("mayúscula");
        texto.Should().Contain("número");
    }
}
