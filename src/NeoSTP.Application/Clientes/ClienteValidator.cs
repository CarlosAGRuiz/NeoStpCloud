using System.Text.RegularExpressions;
using NeoSTP.Application.Clientes.Dtos;

namespace NeoSTP.Application.Clientes;

/// <summary>
/// Validaciones fiscales para clientes en El Salvador.
/// Formatos:
/// - DUI: 9 digitos con guion antes del ultimo (########-#)
/// - NIT: 14 digitos con guiones (####-######-###-#) o sin guiones
/// - NRC: 1-7 digitos opcionalmente con guion (####### o ######-#)
/// </summary>
public static class ClienteValidator
{
    private static readonly Regex DuiPattern = new(@"^\d{8}-\d$", RegexOptions.Compiled);
    private static readonly Regex NitWithDashesPattern = new(@"^\d{4}-\d{6}-\d{3}-\d$", RegexOptions.Compiled);
    private static readonly Regex NitNoFormatPattern = new(@"^\d{14}$", RegexOptions.Compiled);
    private static readonly Regex NrcPattern = new(@"^\d{1,7}(-\d)?$", RegexOptions.Compiled);

    public static List<string> Validate(CreateClienteRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Nombre))
            errors.Add("El nombre es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.NumeroDocumento))
            errors.Add("El número de documento es obligatorio.");
        else
        {
            var doc = request.NumeroDocumento.Trim();
            var tipo = (request.TipoDocumentoCodigo ?? "").Trim().ToUpperInvariant();
            switch (tipo)
            {
                case "DUI":
                    if (!DuiPattern.IsMatch(doc))
                        errors.Add("DUI inválido. Formato esperado: 12345678-9");
                    break;
                case "NIT":
                    if (!NitWithDashesPattern.IsMatch(doc) && !NitNoFormatPattern.IsMatch(doc))
                        errors.Add("NIT inválido. Formato esperado: 0614-010100-101-1 o 14 dígitos sin guiones.");
                    break;
                case "PASAPORTE":
                case "CARNET_RESIDENTE":
                case "OTRO":
                    if (doc.Length < 3)
                        errors.Add("Documento demasiado corto.");
                    break;
                default:
                    errors.Add($"Tipo de documento desconocido: {request.TipoDocumentoCodigo}");
                    break;
            }
        }

        var tipoContrib = (request.TipoContribuyenteCodigo ?? "").Trim().ToUpperInvariant();
        if (tipoContrib != "CONSUMIDOR_FINAL" && tipoContrib != "CONTRIBUYENTE" && tipoContrib != "GRAN_CONTRIBUYENTE")
        {
            errors.Add($"Tipo de contribuyente inválido: {request.TipoContribuyenteCodigo}");
        }

        var esContribuyente = tipoContrib == "CONTRIBUYENTE" || tipoContrib == "GRAN_CONTRIBUYENTE";
        if (esContribuyente)
        {
            if (string.IsNullOrWhiteSpace(request.Nrc))
                errors.Add("NRC es obligatorio para contribuyentes.");
            else if (!NrcPattern.IsMatch(request.Nrc.Trim()))
                errors.Add("NRC inválido. Formato esperado: 7 dígitos con o sin guion (1234567 o 123456-7).");

            if (string.IsNullOrWhiteSpace(request.CodigoActividad))
                errors.Add("Código de actividad es obligatorio para contribuyentes.");
        }

        if (!string.IsNullOrWhiteSpace(request.Correo) && !request.Correo.Contains('@'))
            errors.Add("Correo inválido.");

        return errors;
    }

    public static string NormalizeNit(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var clean = new string(input.Where(char.IsDigit).ToArray());
        return clean.Length == 14
            ? $"{clean[..4]}-{clean.Substring(4, 6)}-{clean.Substring(10, 3)}-{clean[13]}"
            : input.Trim();
    }
}
