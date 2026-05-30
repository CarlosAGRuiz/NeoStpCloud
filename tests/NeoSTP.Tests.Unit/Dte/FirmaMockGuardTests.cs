using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// Guardrail anti-mock: el sistema no debe enviar una firma mock/none al Hacienda real.
/// Estas pruebas validan <see cref="DteDocumentosService.EsFirmaMock"/>, el detector usado
/// en <c>EnviarAsync</c> para bloquear el envío.
/// </summary>
public class FirmaMockGuardTests
{
    private const string Sample = """{"identificacion":{"version":1,"tipoDte":"01"}}""";

    [Fact]
    public async Task EsFirmaMock_DetectaJwsDelFirmadorMock()
    {
        var mock = new MockDteSignerService(NullLogger<MockDteSignerService>.Instance);
        var r = await mock.FirmarAsync(Sample, certificadoBlob: null, certificadoPassword: null);

        DteDocumentosService.EsFirmaMock(r.JsonFirmado).Should().BeTrue(
            "el header del firmador mock contiene alg none/mock");
    }

    [Fact]
    public void EsFirmaMock_FalseParaHeaderRs512()
    {
        // Header real de Hacienda: {"alg":"RS512"}
        var header = B64U(Encoding.UTF8.GetBytes("""{"alg":"RS512"}"""));
        var payload = B64U(Encoding.UTF8.GetBytes(Sample));
        var jws = $"{header}.{payload}.ZmFrZXNpZw";

        DteDocumentosService.EsFirmaMock(jws).Should().BeFalse(
            "una firma RS512 real no es mock");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin-puntos")]
    public void EsFirmaMock_RobustoAnteEntradasInvalidas(string? jws)
    {
        DteDocumentosService.EsFirmaMock(jws).Should().BeFalse();
    }

    private static string B64U(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
