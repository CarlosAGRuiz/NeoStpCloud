namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Contador atómico de correlativo por empresa y tipo de DTE.
/// Reemplaza el COUNT(*)+1 con un incremento atómico SQL.
/// </summary>
public class DteCorrelativo
{
    public int EmpresaId { get; set; }
    public string TipoDteCodigo { get; set; } = null!;
    /// <summary>Último correlativo emitido.</summary>
    public int UltimoCorrelativo { get; set; }
    public DateTime ActualizadoAt { get; set; }
}
