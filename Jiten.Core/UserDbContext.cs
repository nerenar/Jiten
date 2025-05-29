using Jiten.Core.Data.Authentication;
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

    public DbSet<RefreshToken> RefreshTokens { get; set; }

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
                                    .Where(x => x.Entity is User && x.State is EntityState.Added or EntityState.Modified);

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