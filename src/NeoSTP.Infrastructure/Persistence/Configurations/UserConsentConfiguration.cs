using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Legal;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class UserConsentConfiguration : IEntityTypeConfiguration<UserConsent>
{
    public void Configure(EntityTypeBuilder<UserConsent> b)
    {
        b.ToTable("Core_UserConsents");
        b.HasKey(x => x.Id);

        b.Property(x => x.ConsentType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Version).HasMaxLength(20).IsRequired();
        b.Property(x => x.AcceptedFromIp).HasMaxLength(45);
        b.Property(x => x.AcceptedUserAgent).HasMaxLength(500);

        b.HasIndex(x => new { x.UsuarioId, x.ConsentType, x.Version });
        b.HasIndex(x => x.AcceptedAt);
    }
}
