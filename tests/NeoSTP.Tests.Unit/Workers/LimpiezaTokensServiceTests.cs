using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Workers;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Workers;

public class LimpiezaTokensServiceTests
{
    private static NeoStpDbContext BuildContext(string name)
        => new(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase(name).Options);

    private static LimpiezaTokensService BuildService(NeoStpDbContext db, int retentionDias = 30)
    {
        var opts = Options.Create(new WorkerOptions
        {
            LimpiezaTokens = new LimpiezaTokensOptions { RetentionDias = retentionDias },
        });
        return new LimpiezaTokensService(db, opts, NullLogger<LimpiezaTokensService>.Instance);
    }

    private static RefreshToken MakeToken(int usuarioId, DateTime expiresAt, DateTime? revokedAt = null)
        => new()
        {
            UsuarioId = usuarioId,
            Token = Guid.NewGuid().ToString(),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
        };

    [Fact]
    public async Task SinTokens_DevuelveCero()
    {
        await using var db = BuildContext(nameof(SinTokens_DevuelveCero));
        var resultado = await BuildService(db).LimpiarTokensVencidosAsync();
        resultado.Should().Be(0);
    }

    [Fact]
    public async Task TokenVencidoHacePocoTiempo_NoSeElimina()
    {
        await using var db = BuildContext(nameof(TokenVencidoHacePocoTiempo_NoSeElimina));
        // Vencido hace 5 días, retención = 30 días → no debe eliminarse
        db.RefreshTokens.Add(MakeToken(1, DateTime.UtcNow.AddDays(-5)));
        await db.SaveChangesAsync();

        var resultado = await BuildService(db, retentionDias: 30).LimpiarTokensVencidosAsync();

        resultado.Should().Be(0);
        db.RefreshTokens.Count().Should().Be(1);
    }

    [Fact]
    public async Task TokenVencidoSuperandoRetencion_SeElimina()
    {
        await using var db = BuildContext(nameof(TokenVencidoSuperandoRetencion_SeElimina));
        // Vencido hace 35 días, retención = 30 → debe eliminarse
        db.RefreshTokens.Add(MakeToken(1, DateTime.UtcNow.AddDays(-35)));
        await db.SaveChangesAsync();

        var resultado = await BuildService(db, retentionDias: 30).LimpiarTokensVencidosAsync();

        resultado.Should().Be(1);
        db.RefreshTokens.Count().Should().Be(0);
    }

    [Fact]
    public async Task TokenRevocadoSuperandoRetencion_SeElimina()
    {
        await using var db = BuildContext(nameof(TokenRevocadoSuperandoRetencion_SeElimina));
        // Expiración en el futuro, pero revocado hace 40 días → debe eliminarse
        db.RefreshTokens.Add(MakeToken(1,
            expiresAt: DateTime.UtcNow.AddDays(1),
            revokedAt: DateTime.UtcNow.AddDays(-40)));
        await db.SaveChangesAsync();

        var resultado = await BuildService(db, retentionDias: 30).LimpiarTokensVencidosAsync();

        resultado.Should().Be(1);
        db.RefreshTokens.Count().Should().Be(0);
    }

    [Fact]
    public async Task MixTokens_SoloEliminaElegibles()
    {
        await using var db = BuildContext(nameof(MixTokens_SoloEliminaElegibles));
        var ahora = DateTime.UtcNow;

        db.RefreshTokens.AddRange(
            MakeToken(1, ahora.AddDays(-35)),           // eliminable: vencido hace 35d
            MakeToken(1, ahora.AddDays(-10)),            // conservar: vencido hace 10d
            MakeToken(1, ahora.AddDays(5)),              // conservar: vigente
            MakeToken(2, ahora.AddDays(-5), ahora.AddDays(-32)) // eliminable: revocado hace 32d
        );
        await db.SaveChangesAsync();

        var resultado = await BuildService(db, retentionDias: 30).LimpiarTokensVencidosAsync();

        resultado.Should().Be(2);
        db.RefreshTokens.Count().Should().Be(2);
    }

    [Fact]
    public async Task TokenActivoNoSeElimina()
    {
        await using var db = BuildContext(nameof(TokenActivoNoSeElimina));
        db.RefreshTokens.Add(MakeToken(1, DateTime.UtcNow.AddDays(30)));  // válido por 30 días
        await db.SaveChangesAsync();

        var resultado = await BuildService(db).LimpiarTokensVencidosAsync();
        resultado.Should().Be(0);
    }
}
