using FluentAssertions;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Infrastructure.Rrhh;
using Xunit;

namespace NeoSTP.Tests.Unit.Rrhh;

/// <summary>NEORRHH Sprint 3 — NominaPdfService: genera un recibo PDF válido (smoke).</summary>
public class NominaPdfServiceTests
{
    [Fact]
    public void GenerarRecibo_ProduceUnPdfValido()
    {
        var svc = new NominaPdfService();
        var modelo = new ReciboNominaModel
        {
            EmpresaNombre = "NEO SOFTWARE TECH PRO", PeriodoEtiqueta = "06/2026 · Q1",
            FechaInicio = new DateOnly(2026, 6, 1), FechaFin = new DateOnly(2026, 6, 15), EstadoCodigo = "CERRADA",
            EmpleadoCodigo = "E001", EmpleadoNombre = "Juan Pérez", Cargo = "Desarrollador",
            IsssNumero = "12345", AfpInstitucion = "Crecer", AfpNumero = "67890",
            SalarioMensual = 1000m, Devengado = 500m, Isss = 15m, Afp = 36.25m, Renta = 30.23m,
            TotalDeducciones = 81.48m, SalarioNeto = 418.52m,
        };

        var bytes = svc.GenerarRecibo(modelo);

        bytes.Should().NotBeNullOrEmpty();
        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }
}
