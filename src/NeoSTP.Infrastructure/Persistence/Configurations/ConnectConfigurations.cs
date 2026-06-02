using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Connect;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ConnectApiKeyConfiguration : IEntityTypeConfiguration<ConnectApiKey>
{
    public void Configure(EntityTypeBuilder<ConnectApiKey> b)
    {
        b.ToTable("Connect_ApiKeys");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.Prefix).HasMaxLength(16).IsRequired();
        b.Property(x => x.KeyHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.Scopes).HasMaxLength(500).IsRequired();
        b.Property(x => x.RevokedBy).HasMaxLength(100);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasIndex(x => x.KeyHash).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.Activo });
    }
}

public class ConnectWebhookConfiguration : IEntityTypeConfiguration<ConnectWebhook>
{
    public void Configure(EntityTypeBuilder<ConnectWebhook> b)
    {
        b.ToTable("Connect_Webhooks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Url).HasMaxLength(500).IsRequired();
        b.Property(x => x.SecretoHmac).HasMaxLength(256).IsRequired();
        b.Property(x => x.Eventos).HasMaxLength(300).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.ApiKey)
         .WithMany(x => x.Webhooks)
         .HasForeignKey(x => x.ApiKeyId)
         .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.EmpresaId, x.Activo });
    }
}

public class ConnectWebhookDeliveryConfiguration : IEntityTypeConfiguration<ConnectWebhookDelivery>
{
    public void Configure(EntityTypeBuilder<ConnectWebhookDelivery> b)
    {
        b.ToTable("Connect_WebhookDeliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Evento).HasMaxLength(60).IsRequired();
        b.Property(x => x.Payload).HasMaxLength(8000).IsRequired();
        b.Property(x => x.Estado).HasMaxLength(20).IsRequired();
        b.Property(x => x.Error).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Webhook)
         .WithMany(x => x.Deliveries)
         .HasForeignKey(x => x.WebhookId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.Estado, x.ProximoIntento });
        b.HasIndex(x => new { x.EmpresaId, x.CreatedAt });
    }
}
