using Microsoft.EntityFrameworkCore;

namespace FlashFix.Api.Data;

public sealed class FlashFixDb(DbContextOptions<FlashFixDb> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<LicenseKey> LicenseKeys => Set<LicenseKey>();
    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.NormalizedUsername).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(32);
            entity.Property(x => x.NormalizedUsername).HasMaxLength(32);
            entity.Property(x => x.PasswordHash).HasMaxLength(255);
        });

        model.Entity<LicenseKey>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.KeyHash).IsUnique();
            entity.HasIndex(x => x.Prefix);
            entity.HasIndex(x => x.AssignedUserId).IsUnique();
            entity.Property(x => x.Prefix).HasMaxLength(16);
            entity.Property(x => x.KeyHash).HasMaxLength(64);
            entity.Property(x => x.DeviceHash).HasMaxLength(64);
            entity.HasOne(x => x.AssignedUser).WithOne()
                .HasForeignKey<LicenseKey>(x => x.AssignedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<SessionRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AccessHash).IsUnique();
            entity.HasIndex(x => x.RefreshHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.AccessHash).HasMaxLength(64);
            entity.Property(x => x.RefreshHash).HasMaxLength(64);
            entity.Property(x => x.DeviceHash).HasMaxLength(64);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        model.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => x.LicenseKeyId);
            entity.Property(x => x.Action).HasMaxLength(48);
            entity.Property(x => x.Outcome).HasMaxLength(24);
            entity.Property(x => x.Detail).HasMaxLength(256);
        });
    }
}
