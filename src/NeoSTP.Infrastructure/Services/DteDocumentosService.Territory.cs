using NeoSTP.Application.Common;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    private async Task<Result<DteTerritory>> ResolverTerritorioParaGeneracionAsync(
        Empresa emisor, DteDocumento doc, int empresaId, bool esquemaNuevo, CancellationToken ct)
    {
        var original = new DteTerritory(doc.ReceptorDepartamentoCodigo, doc.ReceptorMunicipioCodigo, doc.ReceptorDistritoCodigo);
        if (_lookup is null || !DteGeneratorService.EsTipoConTerritorioVerificado(doc.TipoDteCodigo))
            return Result<DteTerritory>.Ok(original);
        var departments = await _lookup.GetCatalogoAsync(CatalogCodes.DepartamentoEs, empresaId, null, ct);
        var municipalities = await _lookup.GetCatalogoAsync(CatalogCodes.MunicipioEs, empresaId, null, ct);
        var districts = await _lookup.GetCatalogoAsync(CatalogCodes.DistritoEs, empresaId, null, ct);
        var requireDistrict = DteGeneratorService.RequiereTerritorio2024(doc.TipoDteCodigo, esquemaNuevo);
        var issuer = DteTerritoryResolver.Resolve(emisor.Departamento, emisor.Municipio, emisor.Distrito,
            departments, municipalities, districts, requireDistrict);
        if (issuer.IsFailure) return Result<DteTerritory>.Fail("Emisor: " + issuer.Error, issuer.ErrorCode);
        emisor.Departamento = issuer.Value!.Department;
        emisor.Municipio = issuer.Value.Municipality;
        emisor.Distrito = issuer.Value.District;
        if (doc.TipoDteCodigo == "11" || (string.IsNullOrWhiteSpace(original.Department)
            && string.IsNullOrWhiteSpace(original.Municipality) && string.IsNullOrWhiteSpace(original.District)))
            return Result<DteTerritory>.Ok(original);
        var receiver = DteTerritoryResolver.Resolve(original.Department, original.Municipality, original.District,
            departments, municipalities, districts, requireDistrict);
        return receiver.IsFailure
            ? Result<DteTerritory>.Fail("Receptor: " + receiver.Error, receiver.ErrorCode)
            : receiver;
    }
}
