using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClinicalAPI.Models;

/// <summary>
/// Hồ sơ bệnh án của bệnh nhân — lưu trong Oracle DB.
/// Được tạo tự động khi ClinicalAPI nhận sự kiện từ Kafka (Ngày 3).
/// </summary>
[Table("MEDICAL_RECORDS")]
public class MedicalRecord
{
    /// <summary>Khóa chính — GUID để nhất quán với ReceptionAPI.</summary>
    [Key]
    [Column("ID")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>ID của bệnh nhân (tham chiếu sang MS SQL — không có FK vật lý vì cross-DB).</summary>
    [Required]
    [Column("PATIENT_ID")]
    public Guid PatientId { get; set; }

    /// <summary>Họ tên bệnh nhân (denormalized để tránh join cross-DB).</summary>
    [Required]
    [MaxLength(100)]
    [Column("PATIENT_NAME")]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Triệu chứng / lý do đến khám.</summary>
    [MaxLength(1000)]
    [Column("SYMPTOMS")]
    public string? Symptoms { get; set; }

    /// <summary>Chẩn đoán của bác sĩ.</summary>
    [MaxLength(1000)]
    [Column("DIAGNOSIS")]
    public string? Diagnosis { get; set; }

    /// <summary>Phác đồ điều trị / đơn thuốc.</summary>
    [MaxLength(2000)]
    [Column("TREATMENT")]
    public string? Treatment { get; set; }

    /// <summary>Bác sĩ phụ trách.</summary>
    [MaxLength(100)]
    [Column("DOCTOR_NAME")]
    public string? DoctorName { get; set; }

    /// <summary>Ngày khám.</summary>
    [Column("VISIT_DATE")]
    public DateTime VisitDate { get; set; } = DateTime.UtcNow;

    /// <summary>Trạng thái hồ sơ: Pending | InProgress | Completed.</summary>
    [MaxLength(20)]
    [Column("STATUS")]
    public string Status { get; set; } = "Pending";

    /// <summary>Ghi chú thêm.</summary>
    [MaxLength(2000)]
    [Column("NOTES")]
    public string? Notes { get; set; }
}
