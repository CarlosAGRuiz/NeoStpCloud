using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Inventario;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Notificaciones;

/// <summary>Generación de alertas — derivación de productos bajo stock mínimo (inventario).</summary>
public class AlertaGeneracionServiceTests
{
    private const int Empresa = 70;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"alertagen-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Tienda", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static (AlertaGeneracionService svc, IAlertaService alertas) NewSvc(NeoStpDbContext db)
    {
        var alertas = Substitute.For<IAlertaService>();
        var cobranza = Substitute.For<ICobranzaService>();
        cobranza.GetPendientesAsync(Arg.Any<int>(), Arg.Any<CobranzaQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<CobroPendienteDto>>.Ok(PagedResult<CobroPendienteDto>.Create(new List<CobroPendienteDto>(), 0, 1, 50)));
        return (new AlertaGeneracionService(db, alertas, cobranza), alertas);
    }

    private static void SeedExistencia(NeoStpDbContext db, decimal cantidad, decimal stockMinimo)
    {
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = Empresa, CodigoInterno = "P1", Nombre = "Café", PrecioUnitario = 10m,
            EstadoCodigo = "ACTIVO", UnidadMedidaCodigo = "59", TipoItem = "BIEN",
        });
        db.ExistenciasProducto.Add(new ExistenciaProducto
        { EmpresaId = Empresa, ProductoId = 1, Cantidad = cantidad, CostoPromedio = 5m, StockMinimo = stockMinimo });
        db.SaveChanges();
    }

    [Fact]
    public async Task Genera_AlertaDeStockBajo()
    {
        var db = NewDb(); var (svc, alertas) = NewSvc(db);
        SeedExistencia(db, cantidad: 10m, stockMinimo: 15m); // 10 <= 15 → bajo

        var creadas = await svc.GenerarAsync(Empresa);

        creadas.Should().Be(1);
        await alertas.Received(1).CrearAsync(
            Arg.Is<CrearAlertaRequest>(r => r.TipoCodigo == AlertaTipos.StockBajo && r.EntidadId == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoGenera_SiStockSobreMinimo()
    {
        var db = NewDb(); var (svc, alertas) = NewSvc(db);
        SeedExistencia(db, cantidad: 20m, stockMinimo: 15m); // 20 > 15 → ok

        var creadas = await svc.GenerarAsync(Empresa);

        creadas.Should().Be(0);
        await alertas.DidNotReceive().CrearAsync(Arg.Any<CrearAlertaRequest>(), Arg.Any<CancellationToken>());
    }
}
