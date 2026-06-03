using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Connect;

/// <summary>
/// NeoConnect — gestión de API Keys: hashing, validación, revocación, expiración y aislamiento.
/// </summary>
public class ConnectApiKeyServiceTests
{
    private const int EmpresaA = 5;
    private const int EmpresaB = 6;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"connect-keys-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    private static ConnectApiKeyService NewSvc(NeoStpDbContext db)
        => new(db, NullLogger<ConnectApiKeyService>.Instance);

    private static CrearApiKeyRequest Req(int empresa, params string[] scopes) => new()
    {
        EmpresaId = empresa,
        Nombre = "Integración ERP",
        Scopes = scopes.Length > 0 ? scopes : new[] { ConnectScopes.DteWrite },
    };

    [Fact]
    public async Task Crear_DevuelveRawKeyYHashConsultableLuego()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.CrearAsync(Req(EmpresaA, ConnectScopes.DteWrite, ConnectScopes.DteRead), "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.RawKey.Should().NotBeNullOrWhiteSpace();
        r.Value.Key.Prefix.Should().Be(r.Value.RawKey[..8]);

        // El hash almacenado NO es la raw key
        var stored = await db.ConnectApiKeys.SingleAsync();
        stored.KeyHash.Should().NotBe(r.Value.RawKey);
        stored.KeyHash.Should().Be(ConnectApiKeyService.HashKey(r.Value.RawKey));

        // La raw key valida y resuelve empresa + scopes
        var ctx = await svc.ValidarAsync(r.Value.RawKey);
        ctx.Should().NotBeNull();
        ctx!.EmpresaId.Should().Be(EmpresaA);
        ctx.Scopes.Should().Contain(ConnectScopes.DteWrite);
        ctx.HasScope(ConnectScopes.DteRead).Should().BeTrue();
    }

    [Theory]
    [InlineData("", new[] { ConnectScopes.DteWrite })]
    public async Task Crear_SinNombre_Validation(string nombre, string[] scopes)
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.CrearAsync(new CrearApiKeyRequest { EmpresaId = EmpresaA, Nombre = nombre, Scopes = scopes }, "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Crear_SinScopes_Validation()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.CrearAsync(new CrearApiKeyRequest { EmpresaId = EmpresaA, Nombre = "X", Scopes = Array.Empty<string>() }, "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Validar_KeyInexistente_Null()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        (await svc.ValidarAsync("nsk_inexistente")).Should().BeNull();
        (await svc.ValidarAsync("")).Should().BeNull();
    }

    [Fact]
    public async Task Validar_KeyRevocada_Null()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var creada = await svc.CrearAsync(Req(EmpresaA), "tester");
        var id = creada.Value!.Key.Id;

        var rev = await svc.RevocarAsync(id, EmpresaA, "tester");
        rev.IsSuccess.Should().BeTrue();

        (await svc.ValidarAsync(creada.Value.RawKey)).Should().BeNull();
    }

    [Fact]
    public async Task Validar_KeyExpirada_Null()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        db.ConnectApiKeys.Add(new ConnectApiKey
        {
            EmpresaId = EmpresaA, Nombre = "Vencida", Prefix = "abcd1234",
            KeyHash = ConnectApiKeyService.HashKey("raw-expirada"),
            Scopes = ConnectScopes.DteRead, Activo = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow.AddDays(-10),
        });
        await db.SaveChangesAsync();

        (await svc.ValidarAsync("raw-expirada")).Should().BeNull();
    }

    [Fact]
    public async Task Revocar_YaRevocada_AlreadyRevoked()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var creada = await svc.CrearAsync(Req(EmpresaA), "tester");
        var id = creada.Value!.Key.Id;
        await svc.RevocarAsync(id, EmpresaA, "tester");

        var r2 = await svc.RevocarAsync(id, EmpresaA, "tester");
        r2.IsFailure.Should().BeTrue();
        r2.ErrorCode.Should().Be("ALREADY_REVOKED");
    }

    [Fact]
    public async Task Revocar_DeOtraEmpresa_NotFound()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        var creada = await svc.CrearAsync(Req(EmpresaA), "tester");
        var id = creada.Value!.Key.Id;

        var r = await svc.RevocarAsync(id, EmpresaB, "tester");
        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("NOT_FOUND");
        // sigue válida para su empresa
        (await svc.ValidarAsync(creada.Value.RawKey)).Should().NotBeNull();
    }

    [Fact]
    public async Task Listar_AisladoPorEmpresa()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CrearAsync(Req(EmpresaA), "tester");
        await svc.CrearAsync(Req(EmpresaA), "tester");
        await svc.CrearAsync(Req(EmpresaB), "tester");

        (await svc.ListarAsync(EmpresaA)).Should().HaveCount(2);
        (await svc.ListarAsync(EmpresaB)).Should().HaveCount(1);
    }
}
