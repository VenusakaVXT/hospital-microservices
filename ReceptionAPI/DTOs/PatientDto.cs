using System.ComponentModel.DataAnnotations;

namespace ReceptionAPI.DTOs;

/// <summary>
/// Data Transfer Object nhận dữ liệu đăng ký bệnh nhân từ client.
/// Tách biệt khỏi Entity để bảo vệ cấu trúc DB và kiểm soát dữ liệu đầu vào.
/// </summary>
public class PatientDto
{
    [Required(ErrorMessage = "Họ và tên không được để trống.")]
    [MaxLength(100, ErrorMessage = "Họ và tên không quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ngày sinh không được để trống.")]
    public DateOnly DateOfBirth { get; set; }

    [Required(ErrorMessage = "Giới tính không được để trống.")]
    [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Giới tính phải là: Male, Female hoặc Other.")]
    public string Gender { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? NationalId { get; set; }

    [MaxLength(15)]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? InsuranceType { get; set; }

    [MaxLength(30)]
    public string? InsuranceNumber { get; set; }
}

/// <summary>
/// Response trả về sau khi đăng ký thành công.
/// </summary>
public class PatientRegisteredResponse
{
    public Guid PatientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
