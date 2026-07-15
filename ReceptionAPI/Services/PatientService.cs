using System.Text.Json;
using ReceptionAPI.Data;
using ReceptionAPI.DTOs;
using ReceptionAPI.Models;

namespace ReceptionAPI.Services;

/// <summary>
/// Dịch vụ đăng ký bệnh nhân — triển khai Outbox Pattern.
///
/// THIẾT KẾ TRANSACTION:
///   Thay vì dùng BeginTransactionAsync() thủ công (xung đột với EnableRetryOnFailure),
///   ta dùng cơ chế tự nhiên của EF Core:
///   → Add() cả Patient và OutboxMessage vào DbContext (chưa lưu)
///   → Gọi SaveChangesAsync() MỘT LẦN DUY NHẤT
///   → EF Core tự động wrap tất cả INSERT trong 1 transaction ACID
///   → Nếu bất kỳ INSERT nào thất bại → toàn bộ rollback
///   → Tương thích hoàn toàn với EnableRetryOnFailure()
/// </summary>
public class PatientService : IPatientService
{
    private readonly ReceptionDbContext _db;
    private readonly ILogger<PatientService> _logger;

    public PatientService(ReceptionDbContext db, ILogger<PatientService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PatientRegisteredResponse> RegisterPatientAsync(PatientDto dto)
    {
        // ── BƯỚC 1: Tạo Patient entity ───────────────────────────────────────
        // Sau khi gọi _db.Patients.Add(patient), EF Core tự gán patient.Id = Guid.NewGuid()
        // (ValueGeneratedOnAdd). Ta có thể dùng patient.Id ngay lập tức mà chưa cần SaveChanges.
        var patient = new Patient
        {
            FullName        = dto.FullName.Trim(),
            DateOfBirth     = dto.DateOfBirth,
            Gender          = dto.Gender,
            NationalId      = dto.NationalId?.Trim(),
            PhoneNumber     = dto.PhoneNumber?.Trim(),
            Address         = dto.Address?.Trim(),
            InsuranceType   = dto.InsuranceType?.Trim(),
            InsuranceNumber = dto.InsuranceNumber?.Trim(),
            RegisteredAt    = DateTime.UtcNow
        };

        _db.Patients.Add(patient);
        // Sau Add() → patient.Id đã được EF Core gán, dùng được cho OutboxMessage

        _logger.LogInformation(
            "Staging Patient '{FullName}' (Id: {PatientId}) for registration...",
            patient.FullName, patient.Id);

        // ── BƯỚC 2: Tạo OutboxMessage (chưa lưu) ────────────────────────────
        // Payload sẽ được ClinicalAPI đọc để tạo hồ sơ bệnh án (Ngày 3+)
        var eventPayload = new
        {
            PatientId    = patient.Id,
            FullName     = patient.FullName,
            DateOfBirth  = patient.DateOfBirth,
            Gender       = patient.Gender,
            RegisteredAt = patient.RegisteredAt
        };

        var outboxMessage = new OutboxMessage
        {
            EventType   = "PatientRegistered",
            Payload     = JsonSerializer.Serialize(eventPayload),
            CreatedAt   = DateTime.UtcNow,
            IsProcessed = false,
            PatientId   = patient.Id
        };

        _db.OutboxMessages.Add(outboxMessage);

        _logger.LogInformation(
            "Staging OutboxMessage (EventType: {EventType}) for Patient '{FullName}'...",
            outboxMessage.EventType, patient.FullName);

        // ── BƯỚC 3: Lưu CẢ HAI entity trong 1 lần SaveChangesAsync ──────────
        // EF Core tự động phát hiện có nhiều thay đổi pending → wrap trong 1 transaction:
        //   BEGIN TRANSACTION
        //     INSERT INTO Patients (...)
        //     INSERT INTO OutboxMessages (...)
        //   COMMIT
        //
        // Nếu bất kỳ INSERT nào thất bại → SQL Server tự ROLLBACK toàn bộ.
        // Cách này tương thích với EnableRetryOnFailure() vì không có user-initiated transaction.
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "✅ Patient '{FullName}' (Id: {PatientId}) registered. OutboxMessage queued.",
            patient.FullName, patient.Id);

        return new PatientRegisteredResponse
        {
            PatientId    = patient.Id,
            FullName     = patient.FullName,
            RegisteredAt = patient.RegisteredAt,
            Message      = $"Đăng ký thành công! Bệnh nhân '{patient.FullName}' đã được ghi nhận."
        };
    }
}
