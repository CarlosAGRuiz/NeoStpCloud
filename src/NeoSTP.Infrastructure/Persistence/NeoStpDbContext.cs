using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Dte.Contingencia;
using NeoSTP.Domain.Core.Dte.Diagnostico;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Legal;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence.Seed;

namespace NeoSTP.Infrastructure.Persistence;

public class NeoStpDbContext : DbContext
{
    public NeoStpDbContext(DbContextOptions<NeoStpDbContext> options) : base(options)
    {
    }

    // Catálogos
    public DbSet<Catalogo> Catalogos => Set<Catalogo>();
    public DbSet<CatalogoItem> CatalogoItems => Set<CatalogoItem>();

    // Empresas
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<PuntoVenta> PuntosVenta => Set<PuntoVenta>();

    // Seguridad
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Licenciamiento
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<PlanModulo> PlanModulos => Set<PlanModulo>();
    public DbSet<EmpresaPlan> EmpresaPlanes => Set<EmpresaPlan>();
    public DbSet<EmpresaModulo> EmpresaModulos => Set<EmpresaModulo>();

    // DTE - Clientes, Productos y Configuración
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<DteConfiguracion> DteConfiguracion => Set<DteConfiguracion>();

    // DTE - Documentos
    public DbSet<DteCorrelativo> DteCorrelativos => Set<DteCorrelativo>();
    public DbSet<DteDocumento> DteDocumentos => Set<DteDocumento>();
    public DbSet<DteDocumentoDetalle> DteDocumentoDetalles => Set<DteDocumentoDetalle>();
    public DbSet<DteDocumentoJson> DteDocumentoJson => Set<DteDocumentoJson>();

    // DTE - Certificación (Sprint 14)
    public DbSet<CertificacionMatriz> CertificacionMatriz => Set<CertificacionMatriz>();
    public DbSet<CertificacionEscenario> CertificacionEscenarios => Set<CertificacionEscenario>();
    public DbSet<CertificacionPrueba> CertificacionPruebas => Set<CertificacionPrueba>();
    public DbSet<CertificacionError> CertificacionErrores => Set<CertificacionError>();

    // DTE - Eventos persistentes (Sprint 15)
    public DbSet<DteEvento> DteEventos => Set<DteEvento>();
    public DbSet<DteEventoJson> DteEventoJson => Set<DteEventoJson>();
    public DbSet<DteEventoRespuestaHacienda> DteEventoRespuestas => Set<DteEventoRespuestaHacienda>();
    public DbSet<DteEventoDocumentoRelacionado> DteEventoDocumentosRelacionados => Set<DteEventoDocumentoRelacionado>();

    // DTE - Lotes de contingencia (Sprint 16)
    public DbSet<DteContingenciaLote> DteContingenciaLotes => Set<DteContingenciaLote>();
    public DbSet<DteContingenciaLoteDetalle> DteContingenciaLoteDetalles => Set<DteContingenciaLoteDetalle>();

    // DTE - Diagnóstico de errores (Sprint 17)
    public DbSet<DteErrorCatalogo> DteErrorCatalogo => Set<DteErrorCatalogo>();
    public DbSet<DteErrorOcurrencia> DteErrorOcurrencias => Set<DteErrorOcurrencia>();

    // Legal — consentimiento (Sprint 18)
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();

    // Auditoría
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NeoStpDbContext).Assembly);
        SeedData.Apply(modelBuilder);
    }
}
