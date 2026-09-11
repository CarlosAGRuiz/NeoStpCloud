namespace NeoSTP.Domain.Core.Productos;

/// <summary>Semántica fiscal del precio de venta capturado.</summary>
public static class TipoPrecioCodigos
{
    public const string IvaIncluido = "IVA_INCLUIDO";
    public const string IvaExcluido = "IVA_EXCLUIDO";

    public static readonly string[] All = [IvaIncluido, IvaExcluido];

    public static bool EsValido(string? codigo)
        => All.Contains(Normalizar(codigo), StringComparer.Ordinal);

    public static string Normalizar(string? codigo)
        => string.IsNullOrWhiteSpace(codigo)
            ? IvaIncluido
            : codigo.Trim().ToUpperInvariant();
}
