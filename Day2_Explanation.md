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

### Cơ Chế Trong Code (Cách Thực Tế Triển Khai)

> ⚠️ **Lưu ý quan trọng:** Khi dùng `EnableRetryOnFailure()` trong EF Core, không thể dùng `BeginTransactionAsync()` thủ công vì conflict với retry strategy. Giải pháp: add cả 2 entity rồi gọi `SaveChangesAsync()` **một lần duy nhất** — EF Core tự wrap trong transaction ACID.

```csharp
// PatientService.cs — cách thực tế, tương thích với EnableRetryOnFailure()

// BƯỚC 1: Stage Patient vào context (chưa lưu DB)
_db.Patients.Add(patient);
// Sau Add() → patient.Id đã được EF Core tự gán (Guid.NewGuid())
// → Dùng được patient.Id cho OutboxMessage ngay lập tức

// BƯỚC 2: Stage OutboxMessage vào context (chưa lưu DB)
_db.OutboxMessages.Add(outboxMessage);

// BƯỚC 3: Gọi SaveChangesAsync() MỘT LẦN DUY NHẤT
// EF Core tự động phát hiện có 2 entity pending → wrap trong 1 transaction:
//   BEGIN TRANSACTION
//     INSERT INTO Patients (...)       ← INSERT 1
//     INSERT INTO OutboxMessages (...)  ← INSERT 2
//   COMMIT
// Nếu INSERT nào fail → SQL Server tự ROLLBACK cả 2
await _db.SaveChangesAsync();
```

**Tại sao không dùng `BeginTransactionAsync()` thủ công?**

```
EnableRetryOnFailure() tạo ra SqlServerRetryingExecutionStrategy.
SaveChangesAsync() nội bộ cũng tạo một ExecutionStrategy riêng.
Khi 2 strategy cùng tồn tại → EF Core phát hiện conflict và throw:
  "SqlServerRetryingExecutionStrategy does not support user-initiated transactions"

Giải pháp tốt nhất: Dùng implicit transaction của EF Core (1 SaveChangesAsync)
→ Không conflict, ACID đảm bảo, code sạch hơn.
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

## 🚚 OutboxPublisherWorker — Vai Trò & Ý Nghĩa

### Vấn Đề Cần Giải Quyết

Khi bệnh nhân đăng ký tại quầy tiếp đón (`ReceptionAPI`), `ClinicalAPI` cần biết để **tự động tạo hồ sơ bệnh án**. Nhưng hai service chạy độc lập — không gọi trực tiếp nhau.

Cách truyền thông thường dùng **Message Broker** (Kafka, RabbitMQ). Nhưng nếu publish thẳng lên Kafka ngay lúc đăng ký:

```
❌ CÁCH NGUY HIỂM (không có Outbox):
  1. INSERT vào Patients   → thành công ✅
  2. Publish lên Kafka     → Kafka đang down! ❌
  → Event mất vĩnh viễn
  → ClinicalAPI không bao giờ tạo được hồ sơ bệnh án
  → Dữ liệu 2 service mất đồng bộ, không có cách khôi phục
```

### OutboxPublisherWorker Giải Quyết Như Thế Nào?

```
✅ CÁCH AN TOÀN (có Outbox + Worker):

  Bước 1 — PatientService (lúc đăng ký):
    BEGIN TX
      INSERT Patients        ← lưu bệnh nhân
      INSERT OutboxMessages  ← lưu "giấy nhắn giao hàng" vào DB
    COMMIT
    → Dù Kafka down, "giấy nhắn" an toàn trong SQL Server

  Bước 2 — OutboxPublisherWorker (mỗi 5 giây):
    Quét OutboxMessages WHERE IsProcessed = false
    → [Ngày 2] Log ra console
    → [Ngày 3+] Publish lên Kafka thật
    → Đánh dấu IsProcessed = true
    → Kafka vẫn down? Worker tự retry 5 giây sau
    → Kafka up trở lại? Worker publish thành công
```

### Vòng Đời Một Message Qua Worker

```
[POST /api/patients/register]
        │
        ▼
OutboxMessages:
  { Id: 'abc', EventType: 'PatientRegistered', IsProcessed: false, ProcessedAt: null }
        │
        │  ← Worker chạy, tìm thấy message này
        ▼
Worker LOG ra console:
  📤 [OUTBOX] Publishing — EventType: PatientRegistered | PatientId: xyz
        │
        ▼
OutboxMessages:
  { Id: 'abc', EventType: 'PatientRegistered', IsProcessed: true, ProcessedAt: '2024-...' }
        │
        │  ← [Ngày 3] Kafka nhận event này
        ▼
ClinicalAPI tự động tạo hồ sơ bệnh án cho bệnh nhân
```

### Tại Sao Worker Chỉ Log Console Ở Ngày 2?

Ngày 2 chưa có Kafka (sẽ setup ở Ngày 3). Worker log ra console để:
- Chứng minh worker đang chạy đúng
- Xác nhận message được tạo và đọc thành công
- Chuẩn bị sẵn điểm để thay `LogInformation` bằng Kafka publish

```csharp
// OutboxPublisherWorker.cs — điểm sẽ thay thế ở Ngày 3
_logger.LogInformation(
    "📤 [OUTBOX] Publishing message — EventType: {EventType} | PatientId: {PatientId}",
    message.EventType, message.PatientId);
// ↑ Ngày 3: thay dòng này bằng: await _kafkaProducer.ProduceAsync("patient-events", message)
```

### Tóm Tắt Vai Trò

| Câu hỏi | Trả lời |
|---|---|
| **Là gì?** | Background worker chạy ngầm suốt vòng đời app |
| **Làm gì?** | Quét và xử lý các event chưa được publish |
| **Tại sao cần?** | Đảm bảo không mất event dù Kafka/network bị lỗi |
| **Hiện tại (Ngày 2)?** | Log ra console |
| **Tương lai (Ngày 3+)?** | Publish lên Apache Kafka |
| **Đảm bảo gì?** | Nếu bệnh nhân đã lưu vào DB, ClinicalAPI **chắc chắn** sẽ nhận được thông báo |

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

## 🖥️ Những Gì Bạn Thấy Trên Terminal

Sau khi gọi `POST /api/patients/register`, terminal ReceptionAPI in ra:

```
# Ngay lập tức sau request:
info: ReceptionAPI.Services.PatientService[0]
      Staging Patient 'Nguyễn Văn An' (Id: 3f2a...) for registration...
info: ReceptionAPI.Services.PatientService[0]
      Staging OutboxMessage (EventType: PatientRegistered) for Patient 'Nguyễn Văn An'...
info: ReceptionAPI.Services.PatientService[0]
      ✅ Patient 'Nguyễn Văn An' (Id: 3f2a...) registered. OutboxMessage queued.

# Tối đa 5 giây sau (Worker tìm thấy message):
info: ReceptionAPI.BackgroundServices.OutboxPublisherWorker[0]
      📬 Found 1 pending outbox message(s). Processing...
info: ReceptionAPI.BackgroundServices.OutboxPublisherWorker[0]
      📤 [OUTBOX] Publishing message — Id: abc | EventType: PatientRegistered
      | PatientId: 3f2a... | CreatedAt: 2024-01-15 03:00:00 | Payload: {"PatientId":"3f2a...",...}
info: ReceptionAPI.BackgroundServices.OutboxPublisherWorker[0]
      ✅ Successfully marked 1 outbox message(s) as processed.
```

> 💡 Nếu terminal bị flood bởi EF Core SQL logs, thêm vào `appsettings.Development.json`:
> ```json
> "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
> ```

---

## 🚩 Ý Nghĩa Của `IsProcessed` — Tại Sao Quan Trọng?

### `IsProcessed` Không Phải Dữ Liệu Gửi Đến Oracle

Đây là điểm **dễ nhầm nhất**. `IsProcessed` chỉ là **trạng thái tracking nội bộ** của ReceptionAPI — giống như dấu "✓ Đã giao" trên một tờ phiếu giao hàng. Oracle và ClinicalAPI **không bao giờ đọc** bảng `OutboxMessages` trực tiếp.

```
IsProcessed = false  →  "Chưa gửi lên Kafka"  →  Worker sẽ xử lý
IsProcessed = true   →  "Đã gửi lên Kafka"    →  Worker bỏ qua vĩnh viễn
```

### Tại Sao Phải Đánh Dấu `IsProcessed = true`?

Để **tránh gửi trùng lặp lên Kafka**. Worker chạy mỗi 5 giây — nếu không có flag này:

```
❌ KHÔNG CÓ IsProcessed (thảm họa):
  Lần quét 1: thấy message → gửi Kafka → ClinicalAPI tạo hồ sơ ✅
  Lần quét 2: thấy lại message → gửi Kafka → ClinicalAPI tạo HỒ SƠ TRÙNG! ❌
  Lần quét 3: thấy lại → gửi → tạo thêm hồ sơ nữa... ❌
  → 1 bệnh nhân có hàng trăm hồ sơ bệnh án!

✅ CÓ IsProcessed:
  Lần quét 1: IsProcessed=false → gửi Kafka → set IsProcessed=true
  Lần quét 2: IsProcessed=true  → Worker KHÔNG LẤY (WHERE IsProcessed=0)
  → Kafka nhận đúng 1 event → ClinicalAPI tạo đúng 1 hồ sơ ✅
```

Worker chỉ query `WHERE IsProcessed = 0` — message đã `true` không bao giờ xuất hiện trong kết quả, không bao giờ được gửi lại.

### Luồng Hoàn Chỉnh Qua 3 Hệ Thống (Ngày 3+)

```
ReceptionAPI (SQL Server)        Kafka Broker          ClinicalAPI (Oracle)
────────────────────────       ──────────────         ──────────────────────

[1] Bệnh nhân đăng ký
    → Patients: INSERT          (chưa liên quan)       (chưa biết gì)
    → OutboxMessages:
        IsProcessed = false

[2] Worker quét (5 giây sau)
    → Thấy IsProcessed=false
    → Publish event ──────────────────────────────────►
    → UPDATE IsProcessed=true   Kafka lưu event         [3] ClinicalAPI NHẬN event
                                 trong topic              → INSERT MedicalRecord
                                 "patient-events"           vào Oracle ✅

[Lần quét tiếp]
    → WHERE IsProcessed=0
    → Không có kết quả          (không gửi lại)         (không bị trùng)
    → Nghỉ, chờ 5 giây tiếp
```

### Oracle Không Bao Giờ Đọc OutboxMessages

```
ReceptionAPI          ClinicalAPI
(SQL Server)          (Oracle)
    │                     │
    │   KHÔNG có đường    │
    │   kết nối trực tiếp │
    │                     │
    └──► Kafka ◄──────────┘
         (cầu nối duy nhất)
```

`OutboxMessages` sau khi `IsProcessed = true` chỉ còn là **audit log** — bằng chứng rằng event đã được gửi thành công, dùng để debug khi cần truy vết.

### Tóm Tắt Vai Trò Từng Trạng Thái

| Trạng thái | Ý nghĩa | Worker làm gì? | ClinicalAPI biết không? |
|---|---|---|---|
| `IsProcessed = false` | Event chưa gửi Kafka | **Publish + set true** | Chưa |
| `IsProcessed = true` | Event đã gửi Kafka | **Bỏ qua hoàn toàn** | Đã nhận qua Kafka |

---

## ✅ Kết Quả Cuối Ngày 2

- ✅ `POST /api/patients/register` hoạt động với validation đầy đủ
- ✅ Patient và OutboxMessage được lưu trong cùng 1 Transaction ACID (implicit, 1 lần SaveChanges)
- ✅ Hiểu lý do **không dùng** `BeginTransactionAsync()` khi có `EnableRetryOnFailure()`
- ✅ `OutboxPublisherWorker` chạy ngầm mỗi 5 giây, log ra console
- ✅ Hiểu vai trò worker: "người giao hàng" đảm bảo ClinicalAPI luôn nhận được event
- ✅ Hiểu cơ chế `IServiceScopeFactory` trong BackgroundService (Singleton vs Scoped)
- ✅ Hiểu `IsProcessed` flag: tránh gửi trùng lặp, không liên quan trực tiếp đến Oracle
- ✅ Hiểu ClinicalAPI **chỉ nhận event qua Kafka**, không đọc OutboxMessages của ReceptionAPI

**Ngày 3:** Thay thế "log ra console" bằng publish thực sự lên **Apache Kafka** và ClinicalAPI sẽ tự động nhận event tạo hồ sơ bệnh án.

