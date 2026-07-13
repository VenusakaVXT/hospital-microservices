using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReceptionAPI.Models;

/// <summary>
/// Bản tin sự kiện Outbox — lưu trong MS SQL cùng transaction với Patient.
/// Background Worker sẽ đọc bảng này và publish lên Kafka.
/// </summary>
[Table("OutboxMessages")]
public class OutboxMessage
{
    /// <summary>Khóa chính tự tăng.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Tên loại sự kiện, ví dụ: "PatientRegistered".</summary>
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Payload JSON của sự kiện (serialize từ đối tượng nghiệp vụ).</summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>Thời điểm tạo bản tin.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Đánh dấu đã được xử lý (publish lên Kafka).
    /// Mặc định false — Background Worker cập nhật thành true sau khi publish thành công.
    /// </summary>
    public bool IsProcessed { get; set; } = false;

    /// <summary>Thời điểm xử lý xong (nullable khi chưa xử lý).</summary>
    public DateTime? ProcessedAt { get; set; }

    // ── Khóa ngoại ───────────────────────────────────────────────────────────
    /// <summary>Liên kết về bệnh nhân đã tạo ra sự kiện này.</summary>
    public Guid PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient Patient { get; set; } = null!;
}
