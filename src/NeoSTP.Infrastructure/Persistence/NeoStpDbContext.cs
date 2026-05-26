using Microsoft.EntityFrameworkCore;

namespace NeoSTP.Infrastructure.Persistence;

public class NeoStpDbContext : DbContext
{
    public NeoStpDbContext(DbContextOptions<NeoStpDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NeoStpDbContext).Assembly);
    }
}
