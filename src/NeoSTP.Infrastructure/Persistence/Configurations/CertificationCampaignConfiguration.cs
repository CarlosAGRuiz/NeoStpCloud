using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Certificacion;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public sealed class CertificationCampaignConfiguration : IEntityTypeConfiguration<CertificationCampaign>
{
    public void Configure(EntityTypeBuilder<CertificationCampaign> b)
    {
        b.ToTable("Dte_CertificationCampaigns", t =>
        {
            t.HasCheckConstraint("CK_CertificationCampaign_TestOnly", "[AmbienteCodigo] = 'PRUEBAS'");
            t.HasCheckConstraint("CK_CertificationCampaign_Budget", "[TotalBudget] > 0");
            t.HasCheckConstraint("CK_CertificationCampaign_UtcPeriod", "[ExpiresAtUtc] > [StartsAtUtc] AND DATEPART(TZOFFSET,[StartsAtUtc]) = 0 AND DATEPART(TZOFFSET,[ExpiresAtUtc]) = 0");
            t.HasCheckConstraint("CK_CertificationCampaign_Status", "[Status] IN ('PREPARED','ACTIVE','REVOKED','CLOSED')");
            t.HasCheckConstraint("CK_CertificationCampaign_Nit", "LEN([ExpectedNit]) = 14 AND [ExpectedNit] NOT LIKE '%[^0-9]%'");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.PublicId).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.Status });
        b.Property(x => x.ExpectedNit).HasMaxLength(14).IsUnicode(false).IsRequired();
        b.Property(x => x.AmbienteCodigo).HasMaxLength(12).IsRequired();
        b.Property(x => x.Status).HasMaxLength(12).IsRequired();
        b.Property(x => x.MatrixReference).HasMaxLength(200).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CertificationCampaignTypeBudgetConfiguration : IEntityTypeConfiguration<CertificationCampaignTypeBudget>
{
    public void Configure(EntityTypeBuilder<CertificationCampaignTypeBudget> b)
    {
        b.ToTable("Dte_CertificationCampaignTypeBudgets", t =>
        {
            t.HasCheckConstraint("CK_CertificationCampaignType_Budget", "[Budget] > 0");
            t.HasCheckConstraint("CK_CertificationCampaignType_Type", "[TipoDteCodigo] IN ('01','03','11','14')");
        });
        b.HasKey(x => new { x.CampaignId, x.TipoDteCodigo });
        b.Property(x => x.TipoDteCodigo).HasMaxLength(2).IsUnicode(false);
        b.HasOne(x => x.Campaign).WithMany(x => x.TypeBudgets).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CertificationCampaignConsumptionConfiguration : IEntityTypeConfiguration<CertificationCampaignConsumption>
{
    public void Configure(EntityTypeBuilder<CertificationCampaignConsumption> b)
    {
        b.ToTable("Dte_CertificationCampaignConsumptions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.PublicId).IsUnique();
        b.HasIndex(x => x.DteDocumentoId).IsUnique();
        b.HasIndex(x => new { x.CampaignId, x.IdempotencyKeyHash }).IsUnique();
        b.HasIndex(x => new { x.CampaignId, x.TipoDteCodigo });
        b.Property(x => x.TipoDteCodigo).HasMaxLength(2).IsUnicode(false);
        b.Property(x => x.IdempotencyKeyHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        b.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        b.Property(x => x.ScenarioReference).HasMaxLength(128).IsRequired();
        b.HasOne(x => x.TypeBudget).WithMany().HasForeignKey(x => new { x.CampaignId, x.TipoDteCodigo }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DteDocumentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
    }
}
