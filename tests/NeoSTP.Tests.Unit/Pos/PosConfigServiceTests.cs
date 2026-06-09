using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Pos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Pos;

/// <summary>NEOPOS — PosConfigService: impresoras e impresión por red.</summary>
public class PosConfigServiceTests
{
    private const int Empresa = 33;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"poscfg-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Tienda", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static (PosConfigService svc, IPosService pos, INetworkPrinter printer) NewSvc(NeoStpDbContext db)
    {
        var pos = Substitute.For<IPosService>();
        var printer = Substitute.For<INetworkPrinter>();
        printer.EnviarAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        var svc = new PosConfigService(db, Substitute.For<IAuditoriaService>(), pos, printer);
        return (svc, pos, printer);
    }

    [Fact]
    public async Task GuardarImpresora_Red_SinIp_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);

        var r = await svc.GuardarImpresoraAsync(Empresa, null, new GuardarImpresoraRequest
        { Nombre = "Caja", Conexion = "RED", Ip = null }, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task GuardarImpresora_SoloUnaPredeterminada()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        await svc.GuardarImpresoraAsync(Empresa, null, new GuardarImpresoraRequest { Nombre = "A", Conexion = "NAVEGADOR", EsPredeterminada = true }, "t");

        await svc.GuardarImpresoraAsync(Empresa, null, new GuardarImpresoraRequest { Nombre = "B", Conexion = "NAVEGADOR", EsPredeterminada = true }, "t");

        var predeterminadas = await db.ImpresorasPos.CountAsync(i => i.EsPredeterminada);
        predeterminadas.Should().Be(1);
    }

    [Fact]
    public async Task ImprimirVentaEnRed_EnviaBytesEscPos()
    {
        var db = NewDb(); var (svc, pos, printer) = NewSvc(db);
        var imp = await svc.GuardarImpresoraAsync(Empresa, null, new GuardarImpresoraRequest
        { Nombre = "Cocina", Conexion = "RED", Ip = "192.168.1.50", Puerto = 9100, AnchoMm = 80 }, "t");
        pos.GetTicketAsync(Empresa, 7, Arg.Any<CancellationToken>())
            .Returns(Result<TicketModel>.Ok(new TicketModel { EmpresaNombre = "Tienda", Numero = "POS-1", Total = 5m,
                Lineas = [new TicketLinea { Descripcion = "x", Cantidad = 1, PrecioUnitario = 5m, Total = 5m }] }));

        var r = await svc.ImprimirVentaEnRedAsync(Empresa, 7, imp.Value!.Id, "t");

        r.IsSuccess.Should().BeTrue();
        await printer.Received(1).EnviarAsync("192.168.1.50", 9100,
            Arg.Is<byte[]>(b => b.Length > 10 && b[0] == 0x1B), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImprimirVentaEnRed_ImpresoraNoRed_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var imp = await svc.GuardarImpresoraAsync(Empresa, null, new GuardarImpresoraRequest { Nombre = "Nav", Conexion = "NAVEGADOR" }, "t");

        var r = await svc.ImprimirVentaEnRedAsync(Empresa, 1, imp.Value!.Id, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }
}
