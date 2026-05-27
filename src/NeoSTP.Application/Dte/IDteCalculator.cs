using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Application.Dte;

/// <summary>
/// Calcula los totales del documento DTE.
/// Para Factura (01) el IVA está incluido en el precio; para CCF (03) se separa.
/// </summary>
public interface IDteCalculator
{
    void Recalcular(DteDocumento documento);
}
