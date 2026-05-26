using Microsoft.Extensions.Logging;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Auth;

public class AuditoriaService : IAuditoriaService
{
    private readonly NeoStpDbContext _db;
    private readonly ILogger<AuditoriaService> _logger;

    public AuditoriaService(NeoStpDbContext db, ILogger<AuditoriaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RegistrarAsync(AuditoriaEvent evento, CancellationToken ct = default)
    {
        try
        {
            var entity = new Auditoria
            {
                EmpresaId = evento.EmpresaId,
                UsuarioId = evento.UsuarioId,
                Username = evento.Username,
                Modulo = evento.Modulo,
                Accion = evento.Accion,
                Entidad = evento.Entidad,
                EntidadId = evento.EntidadId,
                DatosAntes = evento.DatosAntes,
                DatosDespues = evento.DatosDespues,
                Resultado = evento.Resultado,
                Detalle = evento.Detalle,
                IpAddress = evento.IpAddress,
                UserAgent = evento.UserAgent,
                TraceId = evento.TraceId,
                CreatedAt = DateTime.UtcNow,
            };

            _db.Auditoria.Add(entity);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // La auditoría nunca debe romper el flujo principal.
            _logger.LogError(ex, "No se pudo registrar auditoría {Modulo}/{Accion}", evento.Modulo, evento.Accion);
        }
    }
}
