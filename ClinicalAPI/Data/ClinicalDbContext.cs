using Microsoft.EntityFrameworkCore;
using ClinicalAPI.Models;

namespace ClinicalAPI.Data;

/// <summary>
/// DbContext cho phân vùng Oracle DB của ClinicalAPI.
/// Quản lý 2 bảng: MEDICAL_RECORDS và MEDICINES.
/// </summary>
public class ClinicalDbContext : DbContext
{
    public ClinicalDbContext(DbContextOptions<ClinicalDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<MedicalRecord> MedicalRecords { get; set; } = null!;
    public DbSet<Medicine> Medicines { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Bảng MEDICAL_RECORDS ─────────────────────────────────────────────
        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.ToTable("MEDICAL_RECORDS");

            // Index tìm kiếm nhanh theo PatientId (hay dùng nhất)
            entity.HasIndex(m => m.PatientId)
                  .HasDatabaseName("IX_MEDICAL_RECORDS_PATIENT_ID");

            // Index trạng thái để lọc hồ sơ đang chờ
            entity.HasIndex(m => m.Status)
                  .HasDatabaseName("IX_MEDICAL_RECORDS_STATUS");

            entity.Property(m => m.Status)
                  .HasMaxLength(20)
                  .HasDefaultValue("Pending");

            entity.Property(m => m.VisitDate)
                  .HasDefaultValueSql("SYSDATE"); // Oracle syntax
        });

        // ── Bảng MEDICINES ───────────────────────────────────────────────────
        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.ToTable("MEDICINES");

            // Index tìm kiếm thuốc theo tên hoạt chất
            entity.HasIndex(m => m.GenericName)
                  .HasDatabaseName("IX_MEDICINES_GENERIC_NAME");

            // Index lọc thuốc đang hoạt động (Cache-Aside sẽ dùng filter này)
            entity.HasIndex(m => m.IsActive)
                  .HasDatabaseName("IX_MEDICINES_IS_ACTIVE");

            entity.Property(m => m.IsActive).HasDefaultValue(true);
            entity.Property(m => m.StockQuantity).HasDefaultValue(0);

            entity.Property(m => m.UpdatedAt)
                  .HasDefaultValueSql("SYSDATE"); // Oracle syntax
        });
    }
}
