using ReceptionAPI.DTOs;

namespace ReceptionAPI.Services;

/// <summary>
/// Interface định nghĩa hợp đồng cho PatientService.
/// Giúp dễ dàng unit test và thay thế implementation.
/// </summary>
public interface IPatientService
{
    /// <summary>
    /// Đăng ký bệnh nhân mới: lưu Patient + OutboxMessage trong cùng 1 Transaction.
    /// </summary>
    Task<PatientRegisteredResponse> RegisterPatientAsync(PatientDto dto);
}
