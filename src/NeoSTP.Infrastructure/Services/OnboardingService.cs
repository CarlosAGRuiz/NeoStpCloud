using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Onboarding;
using NeoSTP.Application.Onboarding.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Deriva el estado de activación (onboarding) de una empresa directamente desde la BD.
/// Cada paso es una verificación barata; aísla por EmpresaId.
/// </summary>
public class OnboardingService : IOnboardingService
{
    private const string EstadoActivo = "ACTIVO";

    private readonly NeoStpDbContext _db;

    public OnboardingService(NeoStpDbContext db) => _db = db;

    public async Task<OnboardingEstadoDto> GetEstadoAsync(int empresaId, CancellationToken ct = default)
    {
        var empresa = await _db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new
            {
                e.RazonSocial,
                e.Nit,
                e.Nrc,
                e.CodigoActividad,
                e.Departamento,
                e.Municipio,
                e.Direccion,
            })
            .FirstOrDefaultAsync(ct);

        var config = await _db.DteConfiguracion.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId)
            .Select(c => new
            {
                TieneCredenciales =
                    !string.IsNullOrEmpty(c.UsuarioMh) &&
                    !string.IsNullOrEmpty(c.PasswordMhCifrado) &&
                    !string.IsNullOrEmpty(c.TipoEstablecimientoCodigo) &&
                    !string.IsNullOrEmpty(c.CodigoEstablecimientoMh) &&
                    !string.IsNullOrEmpty(c.CodigoPuntoVentaMh),
                TieneCertificado = c.CertificadoBlob != null && c.CertificadoBlob.Length > 0,
            })
            .FirstOrDefaultAsync(ct);

        var tieneCliente = await _db.Clientes.AsNoTracking()
            .AnyAsync(c => c.EmpresaId == empresaId && c.EstadoCodigo == EstadoActivo, ct);
        var tieneProducto = await _db.Productos.AsNoTracking()
            .AnyAsync(p => p.EmpresaId == empresaId && p.EstadoCodigo == EstadoActivo, ct);

        var tienePrimerDte = await _db.DteDocumentos.AsNoTracking()
            .AnyAsync(d => d.EmpresaId == empresaId && d.EstadoCodigo == DteEstadoCodigos.Procesado, ct);

        var perfilCompleto = empresa is not null &&
            !string.IsNullOrWhiteSpace(empresa.Nit) &&
            !string.IsNullOrWhiteSpace(empresa.Nrc) &&
            !string.IsNullOrWhiteSpace(empresa.CodigoActividad) &&
            !string.IsNullOrWhiteSpace(empresa.Departamento) &&
            !string.IsNullOrWhiteSpace(empresa.Municipio) &&
            !string.IsNullOrWhiteSpace(empresa.Direccion);

        var pasos = new List<OnboardingPasoDto>
        {
            new()
            {
                Codigo = OnboardingPasos.PerfilEmpresa,
                Titulo = "Completa el perfil de tu empresa",
                Descripcion = "NIT, NRC, actividad económica y dirección fiscal para emitir DTE válidos.",
                Icono = "domain",
                Completado = perfilCompleto,
                AccionTexto = "Editar empresa",
                AccionUrl = $"/Empresas/Edit/{empresaId}",
            },
            new()
            {
                Codigo = OnboardingPasos.ConfigDte,
                Titulo = "Configura tus credenciales de Hacienda",
                Descripcion = "Ambiente, usuario MH y datos del establecimiento emisor.",
                Icono = "key",
                Completado = config?.TieneCredenciales ?? false,
                AccionTexto = "Configurar DTE",
                AccionUrl = "/DteConfiguracion",
            },
            new()
            {
                Codigo = OnboardingPasos.Certificado,
                Titulo = "Carga tu certificado de firma",
                Descripcion = "Sube el certificado (.crt) emitido por Hacienda para firmar tus documentos.",
                Icono = "verified_user",
                Completado = config?.TieneCertificado ?? false,
                AccionTexto = "Cargar certificado",
                AccionUrl = "/DteConfiguracion",
            },
            new()
            {
                Codigo = OnboardingPasos.CatalogoBase,
                Titulo = "Agrega tu primer cliente y producto",
                Descripcion = "Registra al menos un cliente y un producto (o usa la carga masiva).",
                Icono = "inventory_2",
                Completado = tieneCliente && tieneProducto,
                AccionTexto = tieneCliente ? "Agregar producto" : "Agregar cliente",
                AccionUrl = tieneCliente ? "/Productos/Create" : "/Clientes/Create",
            },
            new()
            {
                Codigo = OnboardingPasos.PrimerDte,
                Titulo = "Emite tu primer DTE",
                Descripcion = "Genera y procesa tu primer documento tributario electrónico.",
                Icono = "receipt_long",
                Completado = tienePrimerDte,
                AccionTexto = "Emitir DTE",
                AccionUrl = "/DteDocumentos/Create",
            },
        };

        return new OnboardingEstadoDto
        {
            EmpresaId = empresaId,
            EmpresaNombre = empresa?.RazonSocial,
            Pasos = pasos,
        };
    }
}
