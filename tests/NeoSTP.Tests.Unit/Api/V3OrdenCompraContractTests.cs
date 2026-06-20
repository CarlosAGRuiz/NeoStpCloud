using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Routing;
using NeoSTP.Api.Authorization;
using NeoSTP.Api.Controllers;
using Xunit;

namespace NeoSTP.Tests.Unit.Api;

public class V3OrdenCompraContractTests
{
    [Fact]
    public void V3S1_OrdenesCompra_MantienenRutasModuloYPermisos()
    {
        typeof(ComprasApiController).GetCustomAttributes<RequireModuleAttribute>()
            .Should().ContainSingle(x => x.Codigo == "COMPRAS");

        var endpoints = new[]
        {
            new Endpoint(nameof(ComprasApiController.ListOrdenes), "GET", "ordenes", "Compras.Ver"),
            new Endpoint(nameof(ComprasApiController.GetOrden), "GET", "ordenes/{id:int}", "Compras.Ver"),
            new Endpoint(nameof(ComprasApiController.CrearOrden), "POST", "ordenes", "Compras.Gestionar"),
            new Endpoint(nameof(ComprasApiController.ActualizarOrden), "PUT", "ordenes/{id:int}", "Compras.Gestionar"),
            new Endpoint(nameof(ComprasApiController.EmitirOrden), "POST", "ordenes/{id:int}/emitir", "Compras.Gestionar"),
            new Endpoint(nameof(ComprasApiController.CancelarOrden), "POST", "ordenes/{id:int}/cancelar", "Compras.Gestionar"),
            new Endpoint(nameof(ComprasApiController.RecibirOrden), "POST", "ordenes/{id:int}/recepciones", "Compras.Gestionar"),
            new Endpoint(nameof(ComprasApiController.ConvertirOrdenAFactura), "POST", "ordenes/{id:int}/convertir-factura", "Compras.Gestionar"),
        };

        foreach (var endpoint in endpoints)
        {
            var method = typeof(ComprasApiController).GetMethod(endpoint.Method)!;
            method.Should().NotBeNull();
            method.GetCustomAttributes<HttpMethodAttribute>()
                .Should().ContainSingle(x => x.HttpMethods.Contains(endpoint.Verb) && x.Template == endpoint.Route);
            method.GetCustomAttributes<RequirePermisoAttribute>()
                .Should().ContainSingle(x => x.Codigo == endpoint.Permission);
        }
    }

    private sealed record Endpoint(string Method, string Verb, string Route, string Permission);
}
