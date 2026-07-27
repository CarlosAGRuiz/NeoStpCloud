using NeoSTP.Application.Common;

namespace NeoSTP.Application.Datos;

/// <summary>
/// Portabilidad de datos (E8): exporta todo lo que la empresa tiene en la plataforma
/// como un ZIP de CSVs.
///
/// Es argumento de venta antes que de salida: al cliente que duda en migrar le pesa
/// menos entrar si sabe que puede llevarse sus datos cuando quiera. También sirve para
/// respaldos propios y para auditorías.
/// </summary>
public interface IPortabilidadService
{
    /// <summary>
    /// Arma el ZIP con los datos de la empresa. Devuelve el contenido y el nombre
    /// sugerido del archivo.
    /// </summary>
    Task<Result<ExportacionDatosDto>> ExportarAsync(int empresaId, string? actor, CancellationToken ct = default);
}

public sealed class ExportacionDatosDto
{
    public string NombreArchivo { get; set; } = null!;
    public byte[] Contenido { get; set; } = [];

    /// <summary>Filas exportadas por archivo, para mostrarle al usuario qué se llevó.</summary>
    public IReadOnlyDictionary<string, int> Resumen { get; set; } = new Dictionary<string, int>();
}
