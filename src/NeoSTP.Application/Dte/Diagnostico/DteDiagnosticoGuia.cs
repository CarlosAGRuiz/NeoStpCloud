using System.Text.Json;
using System.Text.RegularExpressions;
using NeoSTP.Application.Dte.Diagnostico.Dtos;

namespace NeoSTP.Application.Dte.Diagnostico;

/// <summary>
/// Interpreta evidencia de la respuesta, no asigna un significado universal a un número MH.
/// Compartido por el detalle DTE, API y diagnóstico histórico. No modifica datos fiscales.
/// </summary>
public static partial class DteDiagnosticoGuia
{
    public static DteDiagnosticoActualDto Crear(string? estado, string? sello, DateTime? enviadoAt,
        string? respuesta, string? errorCodigo = null, string? error = null, IEnumerable<string>? validaciones = null)
    {
        var mh = Leer(respuesta);
        var observaciones = mh.Observaciones.Concat(validaciones ?? []).Distinct().ToArray();
        var mensajes = new[] { mh.Descripcion, error }.Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>().Concat(observaciones).ToArray();
        var campos = mensajes.SelectMany(ExtraerCampos).DistinctBy(x => x.Campo).ToArray();
        var texto = string.Join(" ", mensajes);
        var duplicado = texto.Contains("DUPLICAD", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("YA EXISTE", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("YA FUE PROCESADO", StringComparison.OrdinalIgnoreCase);

        DteDiagnosticoActualDto Guia(string codigo, string mensaje, string paso, string accion, bool consultar = false)
            => new(codigo, mensaje, mh.Codigo, mh.Descripcion ?? error, observaciones, campos, paso,
                accion + " Conserve el mismo ID, número de control y código de generación; no cree otra factura para reintentar.", consultar);

        DteDiagnosticoActualDto Consultar() => Guia("DTE_RESULTADO_INCIERTO",
            "La recepción no está confirmada. No vuelva a emitir esta venta.", "CONCILIAR_HACIENDA",
            "Consulte con soporte el estado en Hacienda usando el código de generación y el ambiente del DTE. " +
            "Consultar el detalle en la API solo muestra el estado local; no consulta Hacienda.", true);

        if (estado == "INVALIDADO")
            return Guia("DTE_INVALIDADO", "El documento está invalidado.", "SOLO_CONSULTA", "No regenere, firme ni envíe este documento.");
        if (estado == "PROCESADO")
            return string.IsNullOrWhiteSpace(sello) ? Consultar() : Guia("DTE_PROCESADO",
                "Documento procesado con sello de Hacienda.", "SOLO_CONSULTA", "Puede consultar o descargar el documento; no requiere otro envío.");
        if (errorCodigo == "DTE_RESULTADO_INCIERTO" || !string.IsNullOrWhiteSpace(sello) || estado is "ENVIADO" or "CONTINGENCIA"
            || (estado == "FIRMADO" && errorCodigo is null) || duplicado
            || mh.Estado is "PROCESADO" or "RECIBIDO" or "ENVIADO" or "CONTINGENCIA"
            || mh.Clasificacion is "TIMEOUT" or "NETWORK_ERROR" or "ERROR_SERVIDOR" or "RESPUESTA_INVALIDA"
            || mh.Http is 0 or >= 500)
            return Consultar();

        // Un error local durante la recuperación no convierte un envío incierto en rechazo.
        if (enviadoAt.HasValue && (mh.Estado is not ("RECHAZADO" or "ERROR" or "NO_AUTORIZADO")
            || (mh.Estado == "ERROR" && string.IsNullOrWhiteSpace(mh.Codigo) && campos.Length == 0)))
            return Consultar();

        if (errorCodigo is "HACIENDA_AUTH_FAILED" or "DTE_CREDENCIALES_REQUERIDAS" or "CONFIG_NOT_FOUND"
            || mh.Estado == "NO_AUTORIZADO" || mh.Http is 401 or 403)
            return Guia(errorCodigo ?? "HACIENDA_AUTH_FAILED", "Revise la configuración de acceso a Hacienda de la empresa.",
                "REVISAR_CONFIGURACION", "Verifique usuario, credenciales y ambiente en Configuración DTE. No cambie el ambiente del documento para solucionar el acceso.");
        if (errorCodigo is "DTE_AMBIENTE_INCOMPATIBLE" or "DTE_AMBIENTE_INVALIDO" or "DTE_ESTABLECIMIENTO_INCOMPATIBLE" or "DTE_PAYLOAD_INCOMPATIBLE")
            return Guia(errorCodigo, "La configuración fiscal no coincide con el documento existente.", "REVISAR_CONFIGURACION",
                "Solicite a soporte revisar ambiente, establecimiento y punto de venta originales. No renumere ni traslade el DTE a otro ambiente.");
        if (errorCodigo is "FIRMA_FAILED" or "FIRMA_MOCK_NO_ENVIABLE" or "DECRYPT_FAILED")
            return Guia(errorCodigo, "No se pudo preparar una firma válida para Hacienda.", "REVISAR_CONFIGURACION",
                "Revise el firmador, la vigencia del certificado y su configuración con el administrador. No comparta contraseñas ni claves privadas.");

        if (campos.Length > 0)
            return Guia(errorCodigo ?? "HACIENDA_DATOS_INVALIDOS", campos[0].Mensaje, "CORREGIR_DATOS",
                "Corrija los campos indicados en su origen. Revise el JSON regenerado del DTE existente y vuelva a validarlo y firmarlo antes del envío explícito.");
        if (mh.Estado is "RECHAZADO" or "ERROR" || errorCodigo is not null)
            return Guia(errorCodigo ?? "HACIENDA_RECHAZO", "El documento requiere revisión antes de continuar.", "REVISAR_DIAGNOSTICO",
                "Revise el mensaje técnico y las observaciones con soporte. No cambie datos fiscales ni códigos al azar.");
        if (estado is "BORRADOR" or "GENERADO" or "VALIDADO")
            return Guia("DTE_PENDIENTE", "La emisión del documento todavía no ha terminado.", "CONTINUAR_DOCUMENTO",
                "Abra el DTE existente y revise el paso pendiente. La creación del borrador no confirma recepción en Hacienda.");
        // FIRMADO puede ser el último estado durable si el proceso perdió la respuesta.
        return Consultar();
    }

    private sealed record Respuesta(string? Estado, string? Codigo, string? Descripcion,
        string? Clasificacion, int? Http, IReadOnlyList<string> Observaciones);

    private static Respuesta Leer(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var json = JsonDocument.Parse(raw);
                var root = json.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    string? Texto(string key) => root.TryGetProperty(key, out var v)
                        ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null : null;
                    var obs = root.TryGetProperty("observaciones", out var values) && values.ValueKind == JsonValueKind.Array
                        ? values.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String)
                            .Select(v => v.GetString()!).ToArray() : [];
                    return new(Texto("estado")?.ToUpperInvariant(), Texto("codigoMsg"), Texto("descripcionMsg"),
                        Texto("clasificaMsg")?.ToUpperInvariant(), int.TryParse(Texto("codigoHttp"), out var http) ? http : null, obs);
                }
            }
            catch (JsonException) { /* Respuesta histórica no JSON: no inventar un rechazo. */ }
        }
        return new(null, null, null, null, null, []);
    }

    [GeneratedRegex(@"(?:#?/|\[)?(?<campo>(?:emisor|receptor)(?:[./][a-zA-Z][a-zA-Z0-9]*)+)", RegexOptions.IgnoreCase)]
    private static partial Regex CampoRegex();

    private static IEnumerable<DteProblemaCampoDto> ExtraerCampos(string texto)
    {
        foreach (Match match in CampoRegex().Matches(texto))
        {
            var campo = match.Groups["campo"].Value.Replace('/', '.');
            var emisor = campo.StartsWith("emisor.", StringComparison.OrdinalIgnoreCase);
            var sujeto = emisor ? "la empresa emisora" : "el receptor";
            var seccion = emisor ? "EMPRESA" : "RECEPTOR_DTE";
            var origen = emisor ? "la ficha fiscal de la empresa" : "los datos del receptor del DTE (revise también la ficha del cliente)";
            var propiedad = campo.Split('.').Last().ToLowerInvariant();
            var (mensaje, accion) = propiedad switch
            {
                "codactividad" or "descactividad" => ($"Revise la actividad económica de {sujeto}.",
                    $"Verifique en {origen} el código y la descripción de la actividad registrada para ese contribuyente ante Hacienda. Un código válido del catálogo no garantiza que esté autorizado para ese NIT."),
                "departamento" or "municipio" => ($"Revise el {propiedad} de {sujeto}.",
                    $"Seleccione en {origen} el departamento y su municipio desde los catálogos. Compruebe que el JSON use sus códigos, no los nombres, y que ambos correspondan entre sí."),
                "nit" or "nrc" or "numdocumento" or "tipodocumento" => ($"Revise la identificación de {sujeto}.",
                    $"Verifique el tipo y número del documento en {origen} contra los datos registrados. No sustituya la identificación por una de prueba."),
                "correo" or "telefono" => ($"Revise el {propiedad} de {sujeto}.", $"Complete o corrija el {propiedad} en {origen} y compruebe el formato requerido."),
                _ => ($"Revise el campo {campo} de {sujeto}.", $"Revise {origen} y la observación técnica antes de regenerar el JSON del documento existente.")
            };
            yield return new(campo, seccion, mensaje, accion);
        }
    }

    /// <summary>Evita el diagnóstico erróneo heredado de códigos MH sin contexto.</summary>
    public static ErrorCatalogoDto AjustarCatalogo(ErrorCatalogoDto item) => item.Codigo is "008" or "096" or "802"
        ? item with { MensajeTecnico = $"Código MH {item.Codigo}: consultar respuesta y observaciones",
            Descripcion = "Requiere interpretar el mensaje y los campos de la respuesta de Hacienda",
            CausaProbable = "El número de código por sí solo no identifica el campo ni la corrección necesaria.",
            AccionSugerida = "Abra el diagnóstico del DTE y revise la respuesta técnica. Si indica duplicado o recepción incierta, concilie el documento existente antes de reenviar." }
        : item;
}
