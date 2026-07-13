using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReceptionAPI.Models;

/// <summary>
/// Thông tin hành chính của bệnh nhân — lưu trong MS SQL Server.
/// </summary>
[Table("Patients")]
public class Patient
{
    /// <summary>Khóa chính — sử dụng GUID để tránh xung đột phân tán.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Họ và tên đầy đủ của bệnh nhân.</summary>
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Ngày sinh.</summary>
    [Required]
    public DateOnly DateOfBirth { get; set; }

    /// <summary>Giới tính: Male | Female | Other.</summary>
    [Required]
    [MaxLength(10)]
    public string Gender { get; set; } = string.Empty;

    /// <summary>Số CMND / CCCD.</summary>
    [MaxLength(20)]
    public string? NationalId { get; set; }

    /// <summary>Số điện thoại liên hệ.</summary>
    [MaxLength(15)]
    public string? PhoneNumber { get; set; }

    /// <summary>Địa chỉ thường trú.</summary>
    [MaxLength(300)]
    public string? Address { get; set; }

    /// <summary>Loại bảo hiểm y tế (nếu có).</summary>
    [MaxLength(50)]
    public string? InsuranceType { get; set; }

    /// <summary>Số thẻ bảo hiểm y tế.</summary>
    [MaxLength(30)]
    public string? InsuranceNumber { get; set; }

    /// <summary>Thời điểm đăng ký — mặc định là UTC hiện tại.</summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────────────────────
    /// <summary>Các bản tin outbox được sinh ra khi bệnh nhân đăng ký.</summary>
    public ICollection<OutboxMessage> OutboxMessages { get; set; } = new List<OutboxMessage>();
}
