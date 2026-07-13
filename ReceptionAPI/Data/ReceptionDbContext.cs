using Microsoft.EntityFrameworkCore;
using ReceptionAPI.Models;

namespace ReceptionAPI.Data;

/// <summary>
/// DbContext cho phân vùng MS SQL Server của ReceptionAPI.
/// Quản lý 2 bảng: Patients và OutboxMessages.
/// </summary>
public class ReceptionDbContext : DbContext
{
    public ReceptionDbContext(DbContextOptions<ReceptionDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Bảng Patients ────────────────────────────────────────────────────
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("Patients");

            // Index giúp tìm kiếm nhanh theo CCCD / số BHYT
            entity.HasIndex(p => p.NationalId)
                  .IsUnique()
                  .HasFilter("[NationalId] IS NOT NULL"); // Partial index SQL Server

            entity.HasIndex(p => p.InsuranceNumber)
                  .HasFilter("[InsuranceNumber] IS NOT NULL");

            entity.Property(p => p.FullName).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Gender).HasMaxLength(10).IsRequired();
            entity.Property(p => p.RegisteredAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ── Bảng OutboxMessages ──────────────────────────────────────────────
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");

            // Index để Worker quét nhanh các message chưa xử lý
            entity.HasIndex(o => o.IsProcessed);
            entity.HasIndex(o => o.CreatedAt);

            entity.Property(o => o.Payload)
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(o => o.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Quan hệ 1-N: một bệnh nhân có thể có nhiều outbox message
            entity.HasOne(o => o.Patient)
                  .WithMany(p => p.OutboxMessages)
                  .HasForeignKey(o => o.PatientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
