namespace NeoSTP.Domain.Core.Dte;

public static class DteAmbientes
{
    public const string Pruebas = "PRUEBAS";
    public const string Produccion = "PRODUCCION";

    public static bool EsValido(string? ambiente) => ambiente is Pruebas or Produccion;

    // Nunca convertir un valor desconocido silenciosamente a pruebas.
    public static string CodigoMh(string ambiente) => ambiente switch
    {
        Pruebas => "00",
        Produccion => "01",
        _ => throw new ArgumentException("Ambiente fiscal inválido.", nameof(ambiente)),
    };
}
