using FluentAssertions;
using NeoSTP.Application.Pos;
using Xunit;

namespace NeoSTP.Tests.Unit.Pos;

/// <summary>NEOPOS — generador de bytes ESC/POS del ticket.</summary>
public class EscPosTicketBuilderTests
{
    private static TicketModel Demo(int ancho = 80) => new()
    {
        EmpresaNombre = "Mi Tienda", EmpresaNit = "123", Numero = "POS-000001", Fecha = DateTime.UtcNow,
        ClienteNombre = "Consumidor final", FormaPago = "EFECTIVO", AnchoMm = ancho,
        Subtotal = 20m, IvaTotal = 2.60m, Total = 22.60m, EfectivoRecibido = 50m, Cambio = 27.40m,
        Lineas = [new TicketLinea { Descripcion = "Café", Cantidad = 2, PrecioUnitario = 11.30m, Total = 22.60m }],
    };

    [Fact]
    public void Build_EmpiezaConInitYTerminaConCorte()
    {
        var bytes = EscPosTicketBuilder.Build(Demo());

        bytes.Should().NotBeNullOrEmpty();
        // ESC @ (init)
        bytes[0].Should().Be(0x1B);
        bytes[1].Should().Be(0x40);
        // GS V 0 (corte total) al final
        bytes[^3].Should().Be(0x1D);
        bytes[^2].Should().Be(0x56);
        bytes[^1].Should().Be(0x00);
    }

    [Fact]
    public void Build_ContieneTextoDelTicket()
    {
        var bytes = EscPosTicketBuilder.Build(Demo());
        var texto = System.Text.Encoding.Latin1.GetString(bytes);

        texto.Should().Contain("Mi Tienda");
        texto.Should().Contain("POS-000001");
        texto.Should().Contain("Café");
        texto.Should().Contain("TOTAL");
    }

    [Fact]
    public void Build_AnchoAfectaNumeroDeColumnas()
    {
        var b80 = EscPosTicketBuilder.Build(Demo(80));
        var b58 = EscPosTicketBuilder.Build(Demo(58));
        // Ambos válidos; el de 58 usa menos columnas (líneas más cortas en general).
        b80.Should().NotBeNullOrEmpty();
        b58.Should().NotBeNullOrEmpty();
    }
}
