namespace NeoSTP.Domain.Core.Dte.Eventos;

/// <summary>
/// Tipos de evento DTE soportados (alineados con la matriz CAT del Sprint 14).
/// El código se usa como discriminador en Dte_Eventos.TipoEventoCodigo y como
/// llave hacia Dte_CertificacionMatriz.TipoDteCodigo.
/// </summary>
public static class TipoEventoCodigos
{
    /// <summary>Anulación de un DTE procesado. Manual MH, esquema invalidacion-schema, endpoint /fesv/anulardte.</summary>
    public const string Invalidacion = "INVALIDACION";

    /// <summary>Reporte de lote de DTE emitidos en contingencia. Esquema contingencia-schema, endpoint /fesv/contingencia.</summary>
    public const string Contingencia = "CONTINGENCIA";

    /// <summary>Devolución/retorno de mercancía (esquema fe-eret/tipoEvento=18, /fesv/recepciondte).</summary>
    public const string Retorno = "RETORNO";

    /// <summary>Operaciones especiales (esquema fe-eop/tipoEvento=17, /fesv/recepciondte).</summary>
    public const string OperacionesEspeciales = "OPERACIONES_ESPECIALES";

    public static readonly string[] Todos =
    {
        Invalidacion, Contingencia, Retorno, OperacionesEspeciales,
    };
}

/// <summary>
/// Rol de un DTE relacionado dentro de un evento. Refleja la semántica que
/// usa Hacienda en cada esquema (un DTE puede ser "anulado" en un evento
/// de invalidación, "lote-contingencia" en uno de contingencia, etc.).
/// </summary>
public static class DteEventoRolCodigos
{
    /// <summary>El DTE referenciado se invalida.</summary>
    public const string Anulado = "ANULADO";

    /// <summary>El DTE referenciado se incluye en el lote de contingencia.</summary>
    public const string LoteContingencia = "LOTE_CONTINGENCIA";

    /// <summary>El DTE origen de un retorno u operaciones especiales.</summary>
    public const string Origen = "ORIGEN";

    /// <summary>El DTE referenciado es objeto de la operación (uso futuro).</summary>
    public const string Objeto = "OBJETO";

    public static readonly string[] Todos =
    {
        Anulado, LoteContingencia, Origen, Objeto,
    };
}
