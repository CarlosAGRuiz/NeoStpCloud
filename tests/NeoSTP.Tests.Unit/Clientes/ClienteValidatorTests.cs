using FluentAssertions;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Clientes.Dtos;
using Xunit;

namespace NeoSTP.Tests.Unit.Clientes;

public class ClienteValidatorTests
{
    [Theory]
    [InlineData("12345678-9", true)]
    [InlineData("00000000-0", true)]
    [InlineData("123456789", false)]
    [InlineData("1234567-89", false)]
    [InlineData("ABCDEFGH-1", false)]
    public void DuiFormat_IsValidatedCorrectly(string dui, bool shouldPass)
    {
        var req = ConsumidorFinal(tipo: "DUI", numero: dui);

        var errors = ClienteValidator.Validate(req);

        var hasDuiError = errors.Any(e => e.Contains("DUI inválido"));
        if (shouldPass) hasDuiError.Should().BeFalse($"DUI '{dui}' debería ser válido");
        else hasDuiError.Should().BeTrue($"DUI '{dui}' debería ser inválido");
    }

    [Fact]
    public void EmptyNumeroDocumento_ReportsObligatorio()
    {
        var req = ConsumidorFinal(tipo: "DUI", numero: "");
        var errors = ClienteValidator.Validate(req);
        errors.Should().Contain(e => e.Contains("obligatorio"));
    }

    [Theory]
    [InlineData("0614-010100-101-1", true)]
    [InlineData("06140101001011", true)]
    [InlineData("0614-010100-101", false)]
    [InlineData("0614 010100 101 1", false)]
    public void NitFormat_AcceptsBothFormats(string nit, bool shouldPass)
    {
        var req = ConsumidorFinal(tipo: "NIT", numero: nit);

        var errors = ClienteValidator.Validate(req);

        var hasNitError = errors.Any(e => e.Contains("NIT inválido"));
        if (shouldPass) hasNitError.Should().BeFalse($"NIT '{nit}' debería ser válido");
        else hasNitError.Should().BeTrue($"NIT '{nit}' debería ser inválido");
    }

    [Fact]
    public void Contribuyente_RequiresNrcAndCodigoActividad()
    {
        var req = new CreateClienteRequest
        {
            TipoDocumentoCodigo = "NIT",
            NumeroDocumento = "0614-010100-101-1",
            Nombre = "Empresa S.A.",
            TipoContribuyenteCodigo = "CONTRIBUYENTE",
            // sin Nrc ni CodigoActividad
        };

        var errors = ClienteValidator.Validate(req);

        errors.Should().Contain(e => e.Contains("NRC es obligatorio"));
        errors.Should().Contain(e => e.Contains("Código de actividad"));
    }

    [Fact]
    public void Contribuyente_WithValidNrcAndActividad_PassesValidation()
    {
        var req = new CreateClienteRequest
        {
            TipoDocumentoCodigo = "NIT",
            NumeroDocumento = "0614-010100-101-1",
            Nombre = "Empresa S.A.",
            TipoContribuyenteCodigo = "CONTRIBUYENTE",
            Nrc = "123456-7",
            CodigoActividad = "62010",
            ActividadEconomica = "Programación",
        };

        var errors = ClienteValidator.Validate(req);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ConsumidorFinal_DoesNotRequireNrc()
    {
        var req = ConsumidorFinal();
        var errors = ClienteValidator.Validate(req);
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1234567", true)]
    [InlineData("123456-7", true)]
    [InlineData("12345-678", false)]
    [InlineData("abc", false)]
    public void NrcFormat_IsValidatedForContribuyentes(string nrc, bool shouldPass)
    {
        var req = new CreateClienteRequest
        {
            TipoDocumentoCodigo = "NIT",
            NumeroDocumento = "0614-010100-101-1",
            Nombre = "X",
            TipoContribuyenteCodigo = "CONTRIBUYENTE",
            Nrc = nrc,
            CodigoActividad = "62010",
        };

        var errors = ClienteValidator.Validate(req);
        var hasNrcFormatError = errors.Any(e => e.Contains("NRC inválido"));
        if (shouldPass) hasNrcFormatError.Should().BeFalse();
        else hasNrcFormatError.Should().BeTrue();
    }

    [Fact]
    public void Correo_InvalidFormat_ReportsError()
    {
        var req = ConsumidorFinal();
        req.Correo = "no-es-correo";
        var errors = ClienteValidator.Validate(req);
        errors.Should().Contain(e => e.Contains("Correo inválido"));
    }

    [Theory]
    [InlineData("0614010100  1011", "0614-010100-101-1")]
    [InlineData("06140101001011",   "0614-010100-101-1")]
    [InlineData("0614-010100-101-1","0614-010100-101-1")]
    public void NormalizeNit_FormatsToCanonical(string input, string expected)
    {
        ClienteValidator.NormalizeNit(input).Should().Be(expected);
    }

    private static CreateClienteRequest ConsumidorFinal(string tipo = "DUI", string numero = "12345678-9") => new()
    {
        TipoDocumentoCodigo = tipo,
        NumeroDocumento = numero,
        Nombre = "Juan Pérez",
        TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
    };
}
