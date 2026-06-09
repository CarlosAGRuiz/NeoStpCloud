using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Crm;
using NeoSTP.Infrastructure.Persistence;
using Xunit;

namespace NeoSTP.Tests.Unit.Crm;

public class CrmSchemaTests
{
    [Fact]
    public void ModeloCrm_IncluyeCotizacionesYLineas()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"crm-schema-{Guid.NewGuid()}")
            .Options;

        using var db = new NeoStpDbContext(options);
        var model = db.Model;

        model.FindEntityType(typeof(CotizacionCrm))!.GetTableName().Should().Be("Crm_Cotizaciones");
        model.FindEntityType(typeof(CotizacionCrmLinea))!.GetTableName().Should().Be("Crm_CotizacionLineas");

        var cotizacion = model.FindEntityType(typeof(CotizacionCrm))!;
        cotizacion.FindNavigation(nameof(CotizacionCrm.Lineas)).Should().NotBeNull();
        cotizacion.FindProperty(nameof(CotizacionCrm.Total))!.GetPrecision().Should().Be(18);
        cotizacion.FindIndex(new[]
        {
            cotizacion.FindProperty(nameof(CotizacionCrm.EmpresaId))!,
            cotizacion.FindProperty(nameof(CotizacionCrm.Numero))!,
        })!.IsUnique.Should().BeTrue();
    }
}
