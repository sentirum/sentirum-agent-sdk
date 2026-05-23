using Microsoft.EntityFrameworkCore;

namespace Sentirum.Agent.Memory.EntityFrameworkCore;

/// <summary>
/// Convenience <see cref="DbContext"/> that exposes a single
/// <see cref="DbSet{TEntity}"/> for <see cref="SentirumMemoryRecord"/>.
/// Use this when memory is the only thing the app stores in its DB; for
/// shared contexts call
/// <c>EfCoreMemoryDbContextExtensions.ApplySentirumMemoryConfiguration</c>
/// from your own <see cref="DbContext.OnModelCreating"/>.
/// </summary>
public class SentirumMemoryDbContext : DbContext
{
    /// <summary>Initializes a new instance of the <see cref="SentirumMemoryDbContext"/> class.</summary>
    public SentirumMemoryDbContext(DbContextOptions<SentirumMemoryDbContext> options)
        : base(options)
    {
    }

    /// <summary>Memory rows.</summary>
    public DbSet<SentirumMemoryRecord> Memory => Set<SentirumMemoryRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplySentirumMemoryConfiguration();
    }
}

/// <summary>
/// Extensions for embedding the Sentirum memory entity into a host's
/// existing <see cref="ModelBuilder"/>.
/// </summary>
public static class EfCoreMemoryDbContextExtensions
{
    /// <summary>
    /// Applies the standard index/configuration for
    /// <see cref="SentirumMemoryRecord"/> against <paramref name="modelBuilder"/>.
    /// </summary>
    public static ModelBuilder ApplySentirumMemoryConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentirumMemoryRecord>(entity =>
        {
            entity.HasIndex(e => new { e.Scope, e.AgentId, e.UserId, e.SessionId, e.Key })
                  .IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
        });
        return modelBuilder;
    }
}
