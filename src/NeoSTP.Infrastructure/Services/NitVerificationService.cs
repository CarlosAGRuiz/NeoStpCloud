using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Lookups;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Verificación de NIT/DUI: valida el formato salvadoreño y busca en los datos locales de la
/// empresa (clientes y emisor) para autocompletar el receptor. La verificación en línea contra
/// MH no es pública; este servicio deja el hook listo (Fuente=MH) para integrarla a futuro.
/// </summary>
public class NitVerificationService : INitVerificationService
{
    private readonly NeoStpDbContext _db;

    public NitVerificationService(NeoStpDbContext db) => _db = db;

    public async Task<NitVerificacionDto> VerificarAsync(int empresaId, string documento, CancellationToken ct = default)
    {
        var digitos = new string((documento ?? string.Empty).Where(char.IsDigit).ToArray());

        var (formatoValido, tipo, normalizado) = digitos.Length switch
        {
            14 => (true, "NIT", $"{digitos[..4]}-{digitos.Substring(4, 6)}-{digitos.Substring(10, 3)}-{digitos[13]}"),
            9 => (true, "DUI", $"{digitos[..8]}-{digitos[8]}"),
            _ => (false, "DESCONOCIDO", documento ?? string.Empty),
        };

        var dto = new NitVerificacionDto
        {
            FormatoValido = formatoValido,
            TipoDocumento = tipo,
            DocumentoNormalizado = normalizado,
            Fuente = "FORMATO",
            Mensaje = formatoValido
                ? $"Formato de {tipo} válido."
                : "Formato no reconocido (se esperaba NIT de 14 dígitos o DUI de 9 dígitos).",
        };

        if (!formatoValido || digitos.Length == 0) return dto;

        // Búsqueda local: cliente de la empresa con ese número de documento (con o sin guiones).
        var cliente = await _db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && (c.NumeroDocumento == normalizado || c.NumeroDocumento == digitos))
            .Select(c => new { c.Nombre, c.Nrc, c.TipoContribuyenteCodigo })
            .FirstOrDefaultAsync(ct);
        if (cliente is not null)
        {
            dto.EncontradoLocal = true;
            dto.Nombre = cliente.Nombre;
            dto.Nrc = cliente.Nrc;
            dto.TipoContribuyente = cliente.TipoContribuyenteCodigo;
            dto.Fuente = "LOCAL";
            dto.Mensaje = "Encontrado en tus clientes.";
            return dto;
        }

        // ¿Coincide con alguna empresa registrada (emisor)?
        var empresa = await _db.Empresas.AsNoTracking()
            .Where(e => e.Nit == normalizado || e.Nit == digitos)
            .Select(e => new { e.RazonSocial, e.Nrc })
            .FirstOrDefaultAsync(ct);
        if (empresa is not null)
        {
            dto.EncontradoLocal = true;
            dto.Nombre = empresa.RazonSocial;
            dto.Nrc = empresa.Nrc;
            dto.Fuente = "LOCAL";
            dto.Mensaje = "Coincide con una empresa registrada.";
        }

        return dto;
    }
}
