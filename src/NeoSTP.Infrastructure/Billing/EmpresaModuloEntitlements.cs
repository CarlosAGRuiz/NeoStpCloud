using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Infrastructure.Billing;

public static class EmpresaModuloEntitlements
{
    public static bool HasGrant(EmpresaModulo module)
        => module.ComplementoAutorizado
            && module.ComplementoAutorizadoAt is DateTime grantedAt && grantedAt <= DateTime.UtcNow
            && !string.IsNullOrWhiteSpace(module.ComplementoAutorizadoBy)
            && !string.IsNullOrWhiteSpace(module.ComplementoMotivo);
}
