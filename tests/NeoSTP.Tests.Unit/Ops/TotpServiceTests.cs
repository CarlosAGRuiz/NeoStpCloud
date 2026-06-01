using FluentAssertions;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>
/// Sprint 20.3 — TOTP RFC 6238. Incluye vectores conocidos del RFC (seed SHA1).
/// </summary>
public class TotpServiceTests
{
    // Seed de prueba del RFC 6238 ("12345678901234567890" ASCII) en Base32.
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private readonly TotpService _svc = new();

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1234567890, "005924")]
    public void GenerarCodigo_CoincideConVectoresRfc6238(long unix, string esperado)
    {
        var code = _svc.GenerarCodigo(RfcSecret, DateTimeOffset.FromUnixTimeSeconds(unix));
        code.Should().Be(esperado);
    }

    [Fact]
    public void GenerarSecreto_EsBase32Valido()
    {
        var secret = _svc.GenerarSecreto();
        secret.Should().MatchRegex("^[A-Z2-7]+$");
        secret.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    public void BuildOtpAuthUri_ContieneSecretoEmisorYAlgoritmo()
    {
        var uri = _svc.BuildOtpAuthUri("ABCDEF", "user@neostp.local", "NeoSTP Cloud");
        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain("secret=ABCDEF");
        uri.Should().Contain("algorithm=SHA1");
        uri.Should().Contain("digits=6");
    }

    [Fact]
    public void Validar_AceptaCodigoVigente()
    {
        var secret = _svc.GenerarSecreto();
        var code = _svc.GenerarCodigo(secret, DateTimeOffset.UtcNow);
        _svc.Validar(secret, code).Should().BeTrue();
    }

    [Fact]
    public void Validar_RechazaCodigoIncorrecto()
    {
        var secret = _svc.GenerarSecreto();
        _svc.Validar(secret, "000000").Should().BeFalse();
        _svc.Validar(secret, "").Should().BeFalse();
    }

    [Fact]
    public void Validar_ToleraDesfaseDeUnPaso()
    {
        var secret = _svc.GenerarSecreto();
        // Código del paso anterior (hace 30s) debe aceptarse con ventana ±1.
        var codeAnterior = _svc.GenerarCodigo(secret, DateTimeOffset.UtcNow.AddSeconds(-30));
        _svc.Validar(secret, codeAnterior, ventana: 1).Should().BeTrue();
    }
}
