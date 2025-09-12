using Jiten.Core.Data;
using Jiten.Core.Data.Authentication;
using Jiten.Core.Data.User;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Core;

public class UserDbContext : IdentityDbContext<User>
{
    public UserDbContext()
    {
    }

    public UserDbContext(DbContextOptions<UserDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserCoverage> UserCoverages { get; set; }
    public DbSet<UserKnownWord> UserKnownWords { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<UserMetadata> UserMetadatas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("user");

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Token);
            entity.Property(rt => rt.JwtId).IsRequired();
            entity.Property(rt => rt.ExpiryDate).IsRequired();
            entity.Property(rt => rt.UserId).IsRequired();
            entity.HasOne(rt => rt.User)
                  .WithMany()
                  .HasForeignKey(rt => rt.UserId);
        });

        modelBuilder.Entity<RefreshToken>()
                    .HasIndex(rt => rt.UserId);

        modelBuilder.Entity<UserCoverage>(entity =>
        {
            entity.HasKey(uc => new { uc.UserId, uc.DeckId });
            entity.Property(uc => uc.Coverage).IsRequired();
            entity.Property(uc => uc.UniqueCoverage).IsRequired();

            entity.HasIndex(uc => uc.UserId).HasDatabaseName("IX_UserCoverage_UserId");
        });

        modelBuilder.Entity<UserKnownWord>(entity =>
        {
            entity.HasKey(uk => new { uk.UserId, uk.WordId, uk.ReadingIndex });
            entity.Property(uk => uk.LearnedDate).IsRequired();
            entity.Property(uk => uk.KnownState).IsRequired();

            entity.HasIndex(uk => uk.UserId).HasDatabaseName("IX_UserKnownWord_UserId");
        });

        modelBuilder.Entity<UserMetadata>(entity =>
        {
            entity.HasKey(um => um.UserId);
            entity.Property(um => um.CoverageRefreshedAt).IsRequired(false);

            entity.HasOne<User>()
                  .WithOne()
                  .HasForeignKey<UserMetadata>(um => um.UserId);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        AddTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AddTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void AddTimestamps()
    {
        var entities = ChangeTracker.Entries()
                                    .Where(x => x is { Entity: User, State: EntityState.Added or EntityState.Modified });

        foreach (var entity in entities)
        {
            var now = DateTime.UtcNow;
            if (entity.State == EntityState.Added)
            {
                ((User)entity.Entity).CreatedAt = now;
            }

            ((User)entity.Entity).UpdatedAt = now;
        }
    }
}