using System.Text.Json;
using NeoSTP.Application.Catalogos.Dtos;

namespace NeoSTP.Web.Models;

/// <summary>
/// Compatibilidad de catálogos territoriales para el formulario de clientes.
/// La relación vigente vive en ParentCodigo; MetadataJson se conserva sólo para
/// instalaciones antiguas que todavía guardan { "departamento": "..." }.
/// </summary>
public static class ClienteTerritorialUi
{
    public static string DepartamentoDe(CatalogoItemDto municipio)
    {
        if (!string.IsNullOrWhiteSpace(municipio.ParentCodigo))
            return municipio.ParentCodigo.Trim();

        if (string.IsNullOrWhiteSpace(municipio.MetadataJson))
            return string.Empty;

        try
        {
            using var metadata = JsonDocument.Parse(municipio.MetadataJson);
            return metadata.RootElement.TryGetProperty("departamento", out var departamento)
                ? departamento.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
