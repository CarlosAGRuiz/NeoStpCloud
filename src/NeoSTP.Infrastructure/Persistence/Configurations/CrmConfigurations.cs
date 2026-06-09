using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Crm;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ContactoCrmConfiguration : IEntityTypeConfiguration<ContactoCrm>
{
    public void Configure(EntityTypeBuilder<ContactoCrm> b)
    {
        b.ToTable("Crm_Contactos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
        b.Property(x => x.Cargo).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(160);
        b.Property(x => x.Telefono).HasMaxLength(30);
        b.Property(x => x.Origen).HasMaxLength(20).IsRequired();
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Notas).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
        b.HasIndex(x => new { x.EmpresaId, x.Nombre });
    }
}

public class EtapaPipelineCrmConfiguration : IEntityTypeConfiguration<EtapaPipelineCrm>
{
    public void Configure(EntityTypeBuilder<EtapaPipelineCrm> b)
    {
        b.ToTable("Crm_EtapasPipeline");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.Property(x => x.ProbabilidadDefault).HasPrecision(5, 2);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.Orden });
    }
}

public class OportunidadCrmConfiguration : IEntityTypeConfiguration<OportunidadCrm>
{
    public void Configure(EntityTypeBuilder<OportunidadCrm> b)
    {
        b.ToTable("Crm_Oportunidades");
        b.HasKey(x => x.Id);
        b.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(1000);
        b.Property(x => x.MontoEstimado).HasPrecision(18, 2);
        b.Property(x => x.Probabilidad).HasPrecision(5, 2);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.MotivoPerdida).HasMaxLength(250);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Contacto).WithMany(x => x.Oportunidades).HasForeignKey(x => x.ContactoCrmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Etapa).WithMany(x => x.Oportunidades).HasForeignKey(x => x.EtapaPipelineCrmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DteDocumento).WithMany().HasForeignKey(x => x.DteDocumentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CuentaCobro).WithMany().HasForeignKey(x => x.CuentaCobroId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
        b.HasIndex(x => new { x.EmpresaId, x.EtapaPipelineCrmId });
        b.HasIndex(x => new { x.EmpresaId, x.ClienteId });
    }
}

public class ActividadCrmConfiguration : IEntityTypeConfiguration<ActividadCrm>
{
    public void Configure(EntityTypeBuilder<ActividadCrm> b)
    {
        b.ToTable("Crm_Actividades");
        b.HasKey(x => x.Id);
        b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Asunto).HasMaxLength(160).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(1000);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Resultado).HasMaxLength(1000);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Oportunidad).WithMany(x => x.Actividades).HasForeignKey(x => x.OportunidadCrmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Contacto).WithMany(x => x.Actividades).HasForeignKey(x => x.ContactoCrmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo, x.FechaProgramada });
        b.HasIndex(x => new { x.EmpresaId, x.OportunidadCrmId });
    }
}

public class CotizacionCrmConfiguration : IEntityTypeConfiguration<CotizacionCrm>
{
    public void Configure(EntityTypeBuilder<CotizacionCrm> b)
    {
        b.ToTable("Crm_Cotizaciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).HasMaxLength(40).IsRequired();
        b.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.MonedaCodigo).HasMaxLength(3).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(1000);
        b.Property(x => x.Terminos).HasMaxLength(1000);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.Property(x => x.SubTotal).HasPrecision(18, 4);
        b.Property(x => x.DescuentoTotal).HasPrecision(18, 4);
        b.Property(x => x.IvaTotal).HasPrecision(18, 4);
        b.Property(x => x.Total).HasPrecision(18, 4);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Oportunidad).WithMany(x => x.Cotizaciones).HasForeignKey(x => x.OportunidadCrmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Contacto).WithMany(x => x.Cotizaciones).HasForeignKey(x => x.ContactoCrmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DteDocumento).WithMany().HasForeignKey(x => x.DteDocumentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lineas).WithOne(x => x.Cotizacion).HasForeignKey(x => x.CotizacionCrmId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.EmpresaId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo, x.FechaEmision });
        b.HasIndex(x => new { x.EmpresaId, x.OportunidadCrmId });
        b.HasIndex(x => x.ClienteId);
        b.HasIndex(x => x.ContactoCrmId);
        b.HasIndex(x => x.DteDocumentoId);
    }
}

public class CotizacionCrmLineaConfiguration : IEntityTypeConfiguration<CotizacionCrmLinea>
{
    public void Configure(EntityTypeBuilder<CotizacionCrmLinea> b)
    {
        b.ToTable("Crm_CotizacionLineas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(50);
        b.Property(x => x.Descripcion).HasMaxLength(500).IsRequired();
        b.Property(x => x.UnidadMedidaCodigo).HasMaxLength(10).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.PrecioUnitario).HasPrecision(18, 4);
        b.Property(x => x.PorcentajeDescuento).HasPrecision(9, 4);
        b.Property(x => x.MontoDescuento).HasPrecision(18, 4);
        b.Property(x => x.VentaNoSujeta).HasPrecision(18, 4);
        b.Property(x => x.VentaExenta).HasPrecision(18, 4);
        b.Property(x => x.VentaGravada).HasPrecision(18, 4);
        b.Property(x => x.IvaItem).HasPrecision(18, 4);
        b.Property(x => x.TotalLinea).HasPrecision(18, 4);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.CotizacionCrmId });
        b.HasIndex(x => x.CotizacionCrmId);
        b.HasIndex(x => x.ProductoId);
    }
}
