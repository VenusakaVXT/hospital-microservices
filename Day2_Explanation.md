# Ngày 2 — Hiện Thực Hóa Outbox Pattern tại Quầy Tiếp Đón (MS SQL)

> **Mục tiêu:** Hiểu ACID Transaction trong .NET Core và cách chạy tác vụ ngầm với `BackgroundService`.

---

## 🎯 Tổng Quan Kiến Trúc Ngày 2

```
[Client / Swagger]
        │
        │  POST /api/patients/register
        ▼
[PatientsController]
        │
        │  Gọi IPatientService
        ▼
[PatientService]  ←── IDbContextTransaction (ACID)
        │
        ├── INSERT vào bảng Patients
        ├── INSERT vào bảng OutboxMessages
        └── COMMIT (cả 2 thành công) hoặc ROLLBACK (cả 2 thất bại)
                                   │
                                   │ (Chạy song song, ngầm)
                                   ▼
                    [OutboxPublisherWorker]  ←── BackgroundService
                            │
                            │  Mỗi 5 giây: quét OutboxMessages có IsProcessed == false
                            ├── Log thông tin ra console
                            └── Cập nhật IsProcessed = true
```

---

## 📁 Cấu Trúc File Được Tạo

```
ReceptionAPI/
├── DTOs/
│   └── PatientDto.cs                   ← Input/Output model cho API
├── Services/
│   ├── IPatientService.cs              ← Interface (hợp đồng)
│   └── PatientService.cs               ← Implementation với Transaction
├── BackgroundServices/
│   └── OutboxPublisherWorker.cs        ← Worker chạy ngầm mỗi 5 giây
├── Controllers/
│   └── PatientsController.cs           ← HTTP endpoint POST /api/patients/register
└── Program.cs                          ← Đăng ký services mới
```

---

## 🔐 ACID Transaction — Trái Tim Của Outbox Pattern

### Transaction Là Gì?

Một Transaction là một nhóm các thao tác DB **phải thành công hoặc thất bại cùng nhau**. Không có trường hợp "thành công một nửa".

### 4 Tính Chất ACID

| Tính chất | Tiếng Việt | Ý nghĩa trong code |
|---|---|---|
| **A**tomicity | Nguyên tử | `Patients` và `OutboxMessages` đều được INSERT hoặc đều không có gì |
| **C**onsistency | Nhất quán | DB luôn ở trạng thái hợp lệ sau mỗi transaction |
| **I**solation | Cô lập | Transaction khác không thấy dữ liệu chưa commit |
| **D**urability | Bền vững | Sau khi commit, dữ liệu được ghi vĩnh viễn dù server crash |

### Tại Sao Cần Transaction Ở Đây?

```
KHÔNG CÓ TRANSACTION (nguy hiểm):
  1. INSERT Patients       -> thành công
  2. INSERT OutboxMessages -> lỗi mạng!
  Kết quả: Patient được lưu nhưng ClinicalAPI không bao giờ biết!

CÓ TRANSACTION:
  1. BEGIN TRANSACTION
  2. INSERT Patients       -> staged (chưa commit)
  3. INSERT OutboxMessages -> lỗi mạng!
  4. ROLLBACK -> cả 2 INSERT bị hủy
  Kết quả: Không có gì được lưu -> client biết và thử lại
```

### Cơ Chế Trong Code

```csharp
await using var transaction = await _db.Database.BeginTransactionAsync();
// SQL: BEGIN TRANSACTION

try
{
    _db.Patients.Add(patient);
    await _db.SaveChangesAsync();
    // SQL: INSERT INTO Patients (...) — staged, chưa visible

    _db.OutboxMessages.Add(outboxMessage);
    await _db.SaveChangesAsync();
    // SQL: INSERT INTO OutboxMessages (...) — staged, chưa visible

    await transaction.CommitAsync();
    // SQL: COMMIT — TẤT CẢ trở nên permanent
}
catch
{
    await transaction.RollbackAsync();
    // SQL: ROLLBACK — TẤT CẢ bị hủy hoàn toàn
    throw;
}
```

---

## ⚙️ BackgroundService — Cơ Chế Hoạt Động

### Vòng Đời BackgroundService

```
App khởi động
    │
    ▼
IHostedService.StartAsync()        <- .NET gọi tự động
    │
    ▼
ExecuteAsync(CancellationToken)    <- Code của chúng ta
    │
    └── while (!stoppingToken.IsCancellationRequested)
            │
            ├── ProcessPendingMessagesAsync()
            └── Task.Delay(5 giây, stoppingToken)
    │
    │  (Khi app tắt: stoppingToken bị cancel)
    ▼
IHostedService.StopAsync()         <- graceful shutdown
```

### Vấn Đề Singleton vs Scoped (Quan Trọng!)

```csharp
// SAI — KHÔNG làm thế này:
public class OutboxPublisherWorker : BackgroundService
{
    private readonly ReceptionDbContext _db;
    // Constructor inject trực tiếp -> LỖI:
    // "Cannot consume scoped service from singleton"
}

// ĐÚNG — Dùng IServiceScopeFactory:
public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Tạo scope mới mỗi lần -> lấy DbContext an toàn
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ReceptionDbContext>();
            // ... dùng db
        }
    }
}
```

**Giải thích:**
- `BackgroundService` → **Singleton** (1 instance suốt vòng đời app)
- `DbContext` → **Scoped** (tạo mới mỗi request, sau đó dispose)
- Singleton **không được** giữ reference đến Scoped service
- `IServiceScopeFactory` là Singleton-safe → tạo scope mới mỗi khi cần

---

## 🔄 Luồng Hoàn Chỉnh Khi Đăng Ký Bệnh Nhân

```
Bước 1: Client gửi POST /api/patients/register
  Body: { "fullName": "Nguyễn Văn A", "dateOfBirth": "1990-05-15", ... }

Bước 2: PatientsController validate ModelState
  Nếu lỗi -> 400 Bad Request
  Nếu OK   -> gọi PatientService.RegisterPatientAsync(dto)

Bước 3: PatientService
  BEGIN TRANSACTION
    INSERT INTO Patients (Id, FullName, DateOfBirth, ...)
    INSERT INTO OutboxMessages (EventType, Payload, IsProcessed, PatientId)
  COMMIT
  Return PatientRegisteredResponse { PatientId, FullName, RegisteredAt }

Bước 4: Controller trả về 201 Created

==== Song song (5 giây sau) ====

Bước 5: OutboxPublisherWorker chạy ngầm
  SELECT * FROM OutboxMessages WHERE IsProcessed = 0
  LOG: "📤 [OUTBOX] Publishing — EventType: PatientRegistered | PatientId: ..."
  UPDATE OutboxMessages SET IsProcessed = 1, ProcessedAt = NOW()
  Chờ 5 giây -> lặp lại
```

---

## 🧩 Đăng Ký DI Trong Program.cs

```csharp
// PatientService: Scoped — tạo mới mỗi HTTP request, an toàn với DbContext
builder.Services.AddScoped<IPatientService, PatientService>();

// OutboxPublisherWorker: Hosted Service — .NET tự quản lý vòng đời
builder.Services.AddHostedService<OutboxPublisherWorker>();
```

---

## 💡 Vì Sao Outbox Pattern Quan Trọng Trong Microservices?

```
CÁCH KHÔNG AN TOÀN:
  1. Save Patient vào DB             <- thành công
  2. Publish event lên Kafka         <- Kafka down! mất event
  ClinicalAPI không nhận được -> mất đồng bộ

OUTBOX PATTERN:
  1. BEGIN TRANSACTION
  2. Save Patient vào DB             <- cùng 1 transaction
  3. Save OutboxMessage vào DB       <-
  4. COMMIT
  5. Worker đọc OutboxMessage -> publish lên Kafka
  Kafka down? Worker retry sau 5 giây. Kafka up? Publish thành công.
```

**Outbox Pattern đảm bảo:** Nếu dữ liệu được lưu vào DB, event *chắc chắn sẽ được publish* — chỉ là sớm hay muộn.

---

## ✅ Kết Quả Cuối Ngày 2

- ✅ `POST /api/patients/register` hoạt động với validation đầy đủ
- ✅ Patient và OutboxMessage được lưu trong cùng 1 Transaction (ACID)
- ✅ `OutboxPublisherWorker` chạy ngầm mỗi 5 giây, log ra console
- ✅ Message được đánh dấu `IsProcessed = true` sau khi xử lý
- ✅ Hiểu cơ chế `IServiceScopeFactory` trong BackgroundService

**Ngày 3:** Thay thế "log ra console" bằng publish thực sự lên **Apache Kafka**.
