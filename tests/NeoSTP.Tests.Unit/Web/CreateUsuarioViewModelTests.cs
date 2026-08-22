using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using NeoSTP.Web.Models;

namespace NeoSTP.Tests.Unit.Web;

public class CreateUsuarioViewModelTests
{
    [Fact]
    public void Validar_SinAceptacionLegal_RechazaElFormulario()
    {
        var model = ModeloValido();
        model.AceptaTerminos = false;

        var errors = Validar(model);

        errors.Should().ContainSingle(error =>
            error.MemberNames.Contains(nameof(CreateUsuarioViewModel.AceptaTerminos)) &&
            error.ErrorMessage == CreateUsuarioViewModel.AceptacionLegalRequerida);
    }

    [Fact]
    public void Validar_ConAceptacionLegal_AceptaElFormulario()
    {
        var model = ModeloValido();
        model.AceptaTerminos = true;

        Validar(model).Should().BeEmpty();
    }

    private static List<ValidationResult> Validar(CreateUsuarioViewModel model)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);
        return errors;
    }

    private static CreateUsuarioViewModel ModeloValido() => new()
    {
        Username = "admin.cliente",
        Email = "admin@cliente.test",
        NombreCompleto = "Administrador Cliente",
        Password = "ClaveSegura123",
        TipoUsuarioCodigo = "ADMIN",
    };
}
