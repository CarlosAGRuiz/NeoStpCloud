using FluentAssertions;
using NeoSTP.Api.Authorization;
using NeoSTP.Api.Controllers;
using Xunit;

namespace NeoSTP.Tests.Unit.Api;

public class RrhhPrestacionesApiContractTests
{
    [Theory]
    [InlineData("GetPoliticaPrestaciones", "Rrhh.Nomina.Ver")]
    [InlineData("UpdatePoliticaPrestaciones", "Rrhh.Nomina.Gestionar")]
    [InlineData("ListVacaciones", "Rrhh.Nomina.Ver")]
    [InlineData("SolicitarVacacion", "Rrhh.Nomina.Gestionar")]
    [InlineData("AprobarVacacion", "Rrhh.Nomina.Gestionar")]
    [InlineData("ListAguinaldos", "Rrhh.Nomina.Ver")]
    [InlineData("CalcularAguinaldos", "Rrhh.Nomina.Gestionar")]
    [InlineData("AprobarAguinaldos", "Rrhh.Nomina.Gestionar")]
    public void EndpointsPrestaciones_DeclaranPermiso(string action, string permiso)
    {
        var method = typeof(RrhhApiController).GetMethod(action);

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(RequirePermisoAttribute), true)
            .Cast<RequirePermisoAttribute>().Should().ContainSingle(x => x.Codigo == permiso);
    }
}
