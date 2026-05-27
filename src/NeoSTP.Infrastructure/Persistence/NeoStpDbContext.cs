using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;
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

    // DTE - Clientes y Productos
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();

    // Auditoría
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NeoStpDbContext).Assembly);
        SeedData.Apply(modelBuilder);
    }
}
