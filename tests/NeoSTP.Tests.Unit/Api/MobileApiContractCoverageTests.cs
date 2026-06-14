using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using NeoSTP.Api.Authorization;
using NeoSTP.Api.Controllers;
using Xunit;

namespace NeoSTP.Tests.Unit.Api;

public class MobileApiContractCoverageTests
{
    [Fact]
    public void MobileControllers_RequierenAuth_ModulosYPermisosEsperados()
    {
        AssertClassAuth<DashboardController>("api/dashboard");
        AssertClassAuth<ClientesController>("api/clientes");
        AssertClassAuth<ProductosController>("api/productos");
        AssertClassAuth<DteConfiguracionController>("api/dte/configuracion");
        AssertClassAuth<DteController>("api/dte");
        AssertClassAuth<CobranzaController>("api/cobros");
        AssertClassAuth<AlertasController>("api/alertas");
        AssertClassAuth<ScanAiController>("api/scanai/documentos", module: "NEOSCANAI");
        AssertClassAuth<PosApiController>("api/pos", module: "NEOPOS");
    }

    [Fact]
    public void AuthMobileEndpoints_MantienenAnonimosYProtegidos()
    {
        AssertMethod<AuthController>(nameof(AuthController.Login), "POST", "login", allowAnonymous: true);
        AssertMethod<AuthController>(nameof(AuthController.Refresh), "POST", "refresh", allowAnonymous: true);
        AssertMethod<AuthController>(nameof(AuthController.Logout), "POST", "logout", authorize: true);
        AssertMethod<AuthController>(nameof(AuthController.Me), "GET", "me", authorize: true);
        AssertMethod<AuthController>(nameof(AuthController.MfaEnroll), "POST", "mfa/enroll", authorize: true);
        AssertMethod<AuthController>(nameof(AuthController.MfaConfirm), "POST", "mfa/confirm", authorize: true);
        AssertMethod<AuthController>(nameof(AuthController.MfaDisable), "POST", "mfa/disable", authorize: true);
    }

    [Fact]
    public void DteMobileEndpoints_MantienenPermisosDeLecturaEmisionYDescarga()
    {
        AssertMethod<DteConfiguracionController>(nameof(DteConfiguracionController.Get), "GET", null, "DTE.Configurar");
        AssertMethod<DteConfiguracionController>(nameof(DteConfiguracionController.Save), "PUT", null, "DTE.Configurar");
        AssertMethod<DteConfiguracionController>(nameof(DteConfiguracionController.UploadCertificado), "POST", "certificado", "DTE.Configurar");
        AssertMethod<DteConfiguracionController>(nameof(DteConfiguracionController.ProbarConexion), "POST", "probar-conexion", "DTE.Configurar");

        AssertMethod<DteController>(nameof(DteController.Emitir), "POST", "emitir", "DTE.Emitir");
        AssertMethod<DteController>(nameof(DteController.EmitirFactura), "POST", "emitir/factura", "DTE.Emitir");
        AssertMethod<DteController>(nameof(DteController.EmitirCcf), "POST", "emitir/credito-fiscal", "DTE.Emitir");
        AssertMethod<DteController>(nameof(DteController.EmitirNotaCredito), "POST", "emitir/nota-credito", "DTE.Emitir");
        AssertMethod<DteController>(nameof(DteController.EmitirNotaDebito), "POST", "emitir/nota-debito", "DTE.Emitir");
        AssertMethod<DteController>(nameof(DteController.List), "GET", "documentos", "DTE.Consultar");
        AssertMethod<DteController>(nameof(DteController.GetById), "GET", "documentos/{id:int}", "DTE.Consultar");
        AssertMethod<DteController>(nameof(DteController.DescargarPdf), "GET", "documentos/{id:int}/pdf", "DTE.Consultar");
        AssertMethod<DteController>(nameof(DteController.DescargarJson), "GET", "documentos/{id:int}/json", "DTE.Consultar");
        AssertMethod<DteController>(nameof(DteController.Reenviar), "POST", "documentos/{id:int}/reenviar", "DTE.Reenviar");
    }

    [Fact]
    public void MaestrosCobrosAlertasMobileEndpoints_MantienenContrato()
    {
        AssertMethod<ClientesController>(nameof(ClientesController.List), "GET", null, "Clientes.Ver");
        AssertMethod<ClientesController>(nameof(ClientesController.Get), "GET", "{id:int}", "Clientes.Ver");
        AssertMethod<ClientesController>(nameof(ClientesController.Create), "POST", null, "Clientes.Crear");
        AssertMethod<ClientesController>(nameof(ClientesController.Update), "PUT", "{id:int}", "Clientes.Editar");
        AssertMethod<ClientesController>(nameof(ClientesController.Inactivar), "PATCH", "{id:int}/inactivar", "Clientes.Editar");
        AssertMethod<ClientesController>(nameof(ClientesController.Etiqueta), "PATCH", "{id:int}/etiqueta", "Clientes.Editar");

        AssertMethod<ProductosController>(nameof(ProductosController.List), "GET", null, "Productos.Ver");
        AssertMethod<ProductosController>(nameof(ProductosController.Get), "GET", "{id:int}", "Productos.Ver");
        AssertMethod<ProductosController>(nameof(ProductosController.Create), "POST", null, "Productos.Crear");
        AssertMethod<ProductosController>(nameof(ProductosController.Update), "PUT", "{id:int}", "Productos.Editar");
        AssertMethod<ProductosController>(nameof(ProductosController.Inactivar), "PATCH", "{id:int}/inactivar", "Productos.Editar");

        AssertMethod<CobranzaController>(nameof(CobranzaController.Resumen), "GET", "resumen", "Cobros.Ver");
        AssertMethod<CobranzaController>(nameof(CobranzaController.Pendientes), "GET", "pendientes", "Cobros.Ver");
        AssertMethod<CobranzaController>(nameof(CobranzaController.SaldoCliente), "GET", "clientes/{clienteId:int}", "Cobros.Ver");
        AssertMethod<CobranzaController>(nameof(CobranzaController.Pagos), "GET", "dte/{dteId:int}/pagos", "Cobros.Ver");
        AssertMethod<CobranzaController>(nameof(CobranzaController.RegistrarPago), "POST", "dte/{dteId:int}/pagos", "Cobros.Gestionar");
        AssertMethod<CobranzaController>(nameof(CobranzaController.GenerarQr), "POST", "qr", "Cobros.Ver");

        AssertMethod<AlertasController>(nameof(AlertasController.List), "GET", null);
        AssertMethod<AlertasController>(nameof(AlertasController.Resumen), "GET", "resumen");
        AssertMethod<AlertasController>(nameof(AlertasController.RegistrarDispositivo), "POST", "dispositivos");
        AssertMethod<AlertasController>(nameof(AlertasController.Leer), "POST", "{id:int}/leer");
        AssertMethod<AlertasController>(nameof(AlertasController.Resolver), "POST", "{id:int}/resolver");
    }

    [Fact]
    public void ScanYPosMobileEndpoints_MantienenModuloPermisosYDescargas()
    {
        AssertMethod<ScanAiController>(nameof(ScanAiController.List), "GET", null, "ScanAI.Ver");
        AssertMethod<ScanAiController>(nameof(ScanAiController.Get), "GET", "{id:int}", "ScanAI.Ver");
        AssertMethod<ScanAiController>(nameof(ScanAiController.Archivo), "GET", "{id:int}/archivo", "ScanAI.Ver");
        AssertMethod<ScanAiController>(nameof(ScanAiController.Subir), "POST", null, "ScanAI.Ver");
        AssertMethod<ScanAiController>(nameof(ScanAiController.Corregir), "PUT", "{id:int}/campos", "ScanAI.Ver");
        AssertMethod<ScanAiController>(nameof(ScanAiController.RegistrarGasto), "POST", "{id:int}/registrar-gasto", "ScanAI.Confirmar");
        AssertMethod<ScanAiController>(nameof(ScanAiController.RegistrarCompra), "POST", "{id:int}/registrar-compra", "ScanAI.Confirmar");
        AssertMethod<ScanAiController>(nameof(ScanAiController.RegistrarDteRecibido), "POST", "{id:int}/registrar-dte-recibido", "ScanAI.Confirmar");

        AssertMethod<PosApiController>(nameof(PosApiController.ListVentas), "GET", "ventas", "Pos.Ver");
        AssertMethod<PosApiController>(nameof(PosApiController.GetVenta), "GET", "ventas/{id:int}", "Pos.Ver");
        AssertMethod<PosApiController>(nameof(PosApiController.CrearVenta), "POST", "ventas", "Pos.Vender");
        AssertMethod<PosApiController>(nameof(PosApiController.Ticket), "GET", "ventas/{id:int}/ticket", "Pos.Ver");
        AssertMethod<PosApiController>(nameof(PosApiController.EnviarTicket), "POST", "ventas/{id:int}/enviar", "Pos.Ver");
        AssertMethod<PosApiController>(nameof(PosApiController.PromoverVenta), "POST", "ventas/{id:int}/promover", "DTE.Emitir");
        AssertMethod<PosApiController>(nameof(PosApiController.Resumen), "GET", "resumen", "Pos.Ver");
        AssertMethod<PosApiController>(nameof(PosApiController.EstadoCaja), "GET", "caja/estado", "Pos.Ver");
        AssertMethod<PosApiController>(nameof(PosApiController.AbrirCaja), "POST", "caja/abrir", "Pos.Vender");
        AssertMethod<PosApiController>(nameof(PosApiController.CerrarCaja), "POST", "caja/{id:int}/cerrar", "Pos.Vender");
    }

    private static void AssertClassAuth<TController>(string route, string? module = null)
    {
        var type = typeof(TController);

        type.GetCustomAttributes<AuthorizeAttribute>().Should().NotBeEmpty();
        type.GetCustomAttributes<Microsoft.AspNetCore.Mvc.RouteAttribute>()
            .Should().ContainSingle(r => r.Template == route);

        if (module is not null)
        {
            type.GetCustomAttributes<AuthorizeAttribute>()
                .Should().Contain(a => a.Policy == $"{RequireModuleAttribute.PolicyPrefix}{module}");
        }
    }

    private static void AssertMethod<TController>(
        string methodName,
        string httpMethod,
        string? template,
        string? permiso = null,
        bool allowAnonymous = false,
        bool authorize = false)
    {
        var method = typeof(TController).GetMethod(methodName)!;
        method.Should().NotBeNull($"{typeof(TController).Name}.{methodName} debe existir para mobile");

        var http = method.GetCustomAttributes<HttpMethodAttribute>()
            .Should().ContainSingle().Subject;
        http.HttpMethods.Should().ContainSingle().Which.Should().Be(httpMethod);
        http.Template.Should().Be(template);

        if (permiso is not null)
        {
            method.GetCustomAttributes<RequirePermisoAttribute>()
                .Should().ContainSingle(a => a.Codigo == permiso);
        }

        if (allowAnonymous)
        {
            method.GetCustomAttributes<AllowAnonymousAttribute>().Should().NotBeEmpty();
        }

        if (authorize)
        {
            method.GetCustomAttributes<AuthorizeAttribute>().Should().NotBeEmpty();
        }
    }
}
