using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClinicalAPI.Models;

/// <summary>
/// Danh mục thuốc — lưu trong Oracle DB.
/// Được cache vào Redis theo chiến lược Cache-Aside (Ngày 4).
/// </summary>
[Table("MEDICINES")]
public class Medicine
{
    /// <summary>Khóa chính tự tăng (Oracle sequence).</summary>
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Tên biệt dược.</summary>
    [Required]
    [MaxLength(200)]
    [Column("TRADE_NAME")]
    public string TradeName { get; set; } = string.Empty;

    /// <summary>Tên hoạt chất.</summary>
    [Required]
    [MaxLength(200)]
    [Column("GENERIC_NAME")]
    public string GenericName { get; set; } = string.Empty;

    /// <summary>Đơn vị tính (viên, ml, mg...).</summary>
    [MaxLength(20)]
    [Column("UNIT")]
    public string Unit { get; set; } = "viên";

    /// <summary>Hàm lượng / nồng độ (ví dụ: 500mg).</summary>
    [MaxLength(50)]
    [Column("STRENGTH")]
    public string? Strength { get; set; }

    /// <summary>Nhóm thuốc / phân loại ATC.</summary>
    [MaxLength(100)]
    [Column("CATEGORY")]
    public string? Category { get; set; }

    /// <summary>Nhà sản xuất.</summary>
    [MaxLength(200)]
    [Column("MANUFACTURER")]
    public string? Manufacturer { get; set; }

    /// <summary>Giá nhập (VNĐ).</summary>
    [Column("COST_PRICE", TypeName = "NUMBER(18,2)")]
    public decimal CostPrice { get; set; }

    /// <summary>Giá bán lẻ (VNĐ).</summary>
    [Column("SELL_PRICE", TypeName = "NUMBER(18,2)")]
    public decimal SellPrice { get; set; }

    /// <summary>Số lượng tồn kho.</summary>
    [Column("STOCK_QUANTITY")]
    public int StockQuantity { get; set; } = 0;

    /// <summary>Trạng thái — chỉ hiển thị thuốc đang hoạt động.</summary>
    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    /// <summary>Ngày cập nhật cuối.</summary>
    [Column("UPDATED_AT")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
