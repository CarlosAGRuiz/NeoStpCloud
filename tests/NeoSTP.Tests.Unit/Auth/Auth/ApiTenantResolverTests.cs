using FluentAssertions;
using NeoSTP.Api.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Auth;

public sealed class ApiTenantResolverTests
{
    [Fact]
    public void TenantUser_CannotOverrideOwnCompany()
    {
        var user = Substitute.For<ICurrentUser>();
        user.EmpresaId.Returns(1);

        var result = ApiTenantResolver.Resolve(user, 2);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorCode.Should().Be("TENANT_OVERRIDE_FORBIDDEN");
    }

    [Fact]
    public void SuperAdmin_MustSelectAnExplicitCompany()
    {
        var user = Substitute.For<ICurrentUser>();
        user.EmpresaId.Returns((int?)null);
        user.TipoUsuarioCodigo.Returns("SUPERADMIN");

        var missing = ApiTenantResolver.Resolve(user, null);
        var selected = ApiTenantResolver.Resolve(user, 2);

        missing.Success.Should().BeFalse();
        missing.StatusCode.Should().Be(400);
        selected.Success.Should().BeTrue();
        selected.EmpresaId.Should().Be(2);
    }

    [Fact]
    public void NonPlatformUser_WithoutTenant_IsRejected()
    {
        var user = Substitute.For<ICurrentUser>();
        user.EmpresaId.Returns((int?)null);
        user.TipoUsuarioCodigo.Returns("OPERADOR");

        var result = ApiTenantResolver.Resolve(user, 2);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.ErrorCode.Should().Be("AUTH_NO_TENANT");
    }
}
