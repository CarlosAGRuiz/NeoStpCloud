using FluentAssertions;
using NeoSTP.Infrastructure.Auth;
using Xunit;

namespace NeoSTP.Tests.Unit.Auth;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsDifferentHashesForSamePassword()
    {
        var h1 = _hasher.Hash("Pa$$word1");
        var h2 = _hasher.Hash("Pa$$word1");

        h1.Should().NotBe(h2, "BCrypt usa salt aleatorio en cada hash");
        h1.Should().StartWith("$2");
    }

    [Fact]
    public void Verify_ReturnsTrue_WhenPasswordMatches()
    {
        var hash = _hasher.Hash("Strong#Password!2026");
        _hasher.Verify("Strong#Password!2026", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenPasswordDoesNotMatch()
    {
        var hash = _hasher.Hash("Strong#Password!2026");
        _hasher.Verify("otra", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenHashIsMalformed()
    {
        _hasher.Verify("cualquier", "no-es-un-hash-bcrypt").Should().BeFalse();
    }
}
