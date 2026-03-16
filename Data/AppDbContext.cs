using IMISMonitor.Models;
using Microsoft.EntityFrameworkCore;

namespace IMISMonitor.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MonitoredDevice> Devices => Set<MonitoredDevice>();
    public DbSet<DowntimeEvent> DowntimeEvents => Set<DowntimeEvent>();
    public DbSet<LatencyRecord> LatencyRecords => Set<LatencyRecord>();
    public DbSet<DailyStatistics> DailyStatistics => Set<DailyStatistics>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MonitoredDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IpAddress).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<DowntimeEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.WentOfflineAt);
            entity.Property(e => e.DeviceName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
            entity.Ignore(e => e.Duration);
        });

        modelBuilder.Entity<LatencyRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.RecordedAt);
            entity.Property(e => e.LatencyMs);
        });
    }
}
