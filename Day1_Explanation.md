# 📗 Ngày 1 — Phân Tích CSDL & Thiết Lập Cấu Trúc Solution

> **Mục tiêu:** Thiết kế bảng dữ liệu, định nghĩa Entity class và cấu hình DbContext kết nối độc lập với 2 loại cơ sở dữ liệu khác nhau trong cùng một solution .NET 8.

---

## 1. 🗄️ Phân Tích Thiết Kế CSDL

### 1.1 Phân Vùng MS SQL Server (ReceptionAPI)

Phân vùng này quản lý **thông tin hành chính** — dữ liệu do quầy lễ tân nhập vào.

#### Bảng `Patients` — Thông tin bệnh nhân

| Cột | Kiểu C# | Kiểu SQL | Ràng buộc | Mô tả |
|---|---|---|---|---|
| `Id` | `Guid` | `UNIQUEIDENTIFIER` | PK | Khóa chính GUID — dùng thay int để tránh xung đột phân tán |
| `FullName` | `string` | `NVARCHAR(100)` | NOT NULL | Họ và tên đầy đủ |
| `DateOfBirth` | `DateOnly` | `DATE` | NOT NULL | Ngày sinh — `DateOnly` là kiểu C# 6+ không lưu giờ phút |
| `Gender` | `string` | `NVARCHAR(10)` | NOT NULL | Male / Female / Other |
| `NationalId` | `string?` | `NVARCHAR(20)` | UNIQUE, NULL | Số CCCD — nullable vì bệnh nhân nước ngoài có thể không có |
| `PhoneNumber` | `string?` | `NVARCHAR(15)` | NULL | Số điện thoại |
| `Address` | `string?` | `NVARCHAR(300)` | NULL | Địa chỉ thường trú |
| `InsuranceType` | `string?` | `NVARCHAR(50)` | NULL | Loại BHYT |
| `InsuranceNumber` | `string?` | `NVARCHAR(30)` | NULL | Số thẻ BHYT |
| `RegisteredAt` | `DateTime` | `DATETIME2` | DEFAULT GETUTCDATE() | Thời điểm đăng ký |

> 💡 **Tại sao dùng `Guid` thay `int` làm PK?**
> Trong Microservices, nhiều service có thể tạo bản ghi đồng thời. `int` tự tăng (identity) yêu cầu DB làm trung gian cấp số — gây nghẽn cổ chai. `Guid` được sinh ngay tại ứng dụng, độc lập hoàn toàn.

#### Bảng `OutboxMessages` — Bản tin sự kiện Outbox

| Cột | Kiểu C# | Kiểu SQL | Ràng buộc | Mô tả |
|---|---|---|---|---|
| `Id` | `int` | `INT IDENTITY` | PK | Khóa chính tự tăng — ổn định vì chỉ có 1 service ghi |
| `EventType` | `string` | `NVARCHAR(100)` | NOT NULL | Tên sự kiện, ví dụ: `"PatientRegistered"` |
| `Payload` | `string` | `NVARCHAR(MAX)` | NOT NULL | Nội dung JSON của sự kiện |
| `CreatedAt` | `DateTime` | `DATETIME2` | DEFAULT GETUTCDATE() | Thời điểm tạo |
| `IsProcessed` | `bool` | `BIT` | DEFAULT 0 | `false` = chưa publish lên Kafka |
| `ProcessedAt` | `DateTime?` | `DATETIME2` | NULL | Thời điểm publish thành công |
| `PatientId` | `Guid` | `UNIQUEIDENTIFIER` | FK → Patients.Id | Liên kết về bệnh nhân |

---

### 1.2 Phân Vùng Oracle DB (ClinicalAPI)

Phân vùng này quản lý **dữ liệu lâm sàng** — hồ sơ bệnh án và danh mục thuốc.

> 📌 **Quy ước đặt tên Oracle:** Tên bảng và cột Oracle thường VIẾT HOA và dùng dấu gạch dưới (`SNAKE_CASE`). Trong EF Core, chúng ta ánh xạ tên này qua `[Column("TEN_COT")]`.

#### Bảng `MEDICAL_RECORDS` — Hồ sơ bệnh án

| Cột | Kiểu C# | Kiểu Oracle | Ràng buộc | Mô tả |
|---|---|---|---|---|
| `ID` | `Guid` | `RAW(16)` | PK | Khóa chính GUID |
| `PATIENT_ID` | `Guid` | `RAW(16)` | NOT NULL, INDEX | ID bệnh nhân từ MS SQL (không có FK vật lý vì cross-DB) |
| `PATIENT_NAME` | `string` | `NVARCHAR2(100)` | NOT NULL | Tên bệnh nhân — denormalized |
| `SYMPTOMS` | `string?` | `NVARCHAR2(1000)` | NULL | Triệu chứng |
| `DIAGNOSIS` | `string?` | `NVARCHAR2(1000)` | NULL | Chẩn đoán |
| `TREATMENT` | `string?` | `NVARCHAR2(2000)` | NULL | Phác đồ điều trị |
| `DOCTOR_NAME` | `string?` | `NVARCHAR2(100)` | NULL | Tên bác sĩ |
| `VISIT_DATE` | `DateTime` | `TIMESTAMP` | DEFAULT SYSDATE | Ngày khám |
| `STATUS` | `string` | `NVARCHAR2(20)` | DEFAULT 'Pending' | Pending / InProgress / Completed |
| `NOTES` | `string?` | `NVARCHAR2(2000)` | NULL | Ghi chú |

> 💡 **Tại sao `PATIENT_NAME` được lưu thêm ở đây (denormalized)?**
> Vì `ClinicalAPI` không được phép gọi trực tiếp sang DB của `ReceptionAPI` (vi phạm nguyên tắc Microservices — mỗi service có DB riêng). Dữ liệu tên bệnh nhân được đính kèm trong Kafka event payload và lưu vào đây để tránh join cross-service.

#### Bảng `MEDICINES` — Danh mục thuốc

| Cột | Kiểu C# | Kiểu Oracle | Ràng buộc | Mô tả |
|---|---|---|---|---|
| `ID` | `int` | `NUMBER` | PK, SEQUENCE | Khóa chính dùng Oracle Sequence |
| `TRADE_NAME` | `string` | `NVARCHAR2(200)` | NOT NULL | Tên biệt dược |
| `GENERIC_NAME` | `string` | `NVARCHAR2(200)` | NOT NULL, INDEX | Tên hoạt chất |
| `UNIT` | `string` | `NVARCHAR2(20)` | DEFAULT 'viên' | Đơn vị tính |
| `STRENGTH` | `string?` | `NVARCHAR2(50)` | NULL | Hàm lượng (500mg...) |
| `CATEGORY` | `string?` | `NVARCHAR2(100)` | NULL | Nhóm thuốc |
| `MANUFACTURER` | `string?` | `NVARCHAR2(200)` | NULL | Nhà sản xuất |
| `COST_PRICE` | `decimal` | `NUMBER(18,2)` | NOT NULL | Giá nhập |
| `SELL_PRICE` | `decimal` | `NUMBER(18,2)` | NOT NULL | Giá bán |
| `STOCK_QUANTITY` | `int` | `NUMBER` | DEFAULT 0 | Số lượng tồn kho |
| `IS_ACTIVE` | `bool` | `NUMBER(1)` | DEFAULT 1 | 1 = đang kinh doanh |
| `UPDATED_AT` | `DateTime` | `TIMESTAMP` | DEFAULT SYSDATE | Lần cập nhật cuối |

---

## 2. 🏗️ Kiến Trúc Code Được Tạo Ra

### Cấu Trúc Thư Mục

```
ReceptionAPI/
├── Models/
│   ├── Patient.cs           ← Entity bảng Patients
│   └── OutboxMessage.cs     ← Entity bảng OutboxMessages
├── Data/
│   └── ReceptionDbContext.cs ← DbContext MS SQL
└── Program.cs                ← DI + cấu hình khởi động

ClinicalAPI/
├── Models/
│   ├── MedicalRecord.cs     ← Entity bảng MEDICAL_RECORDS
│   └── Medicine.cs          ← Entity bảng MEDICINES
├── Data/
│   └── ClinicalDbContext.cs ← DbContext Oracle
└── Program.cs                ← DI + cấu hình khởi động
```

---

## 3. 🔄 Luồng Hoạt Động Ngày 1

```
┌─────────────────────────────────────────────────────────────────────┐
│                     KHỞI ĐỘNG ỨNG DỤNG                              │
│                                                                     │
│  Program.cs đọc appsettings.json                                    │
│       ↓                                                             │
│  builder.Services.AddDbContext<ReceptionDbContext>(...)             │
│       ↓                                                             │
│  .NET DI Container đăng ký ReceptionDbContext với vòng đời Scoped  │
│       ↓                                                             │
│  app.Build() → db.Database.Migrate() → Tạo bảng nếu chưa có       │
│       ↓                                                             │
│  app.Run() → Lắng nghe HTTP request                                 │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.1 Dependency Injection (DI) hoạt động thế nào?

**DI** là một mẫu thiết kế giúp các class không tự tạo đối tượng mà **yêu cầu** chúng từ bên ngoài.

```csharp
// ❌ Cách cũ — tự tạo đối tượng, không thể test
public class PatientController
{
    private readonly ReceptionDbContext _db = new ReceptionDbContext(...); // hardcode!
}

// ✅ Cách DI — .NET tự inject vào
public class PatientController : ControllerBase
{
    private readonly ReceptionDbContext _db;

    public PatientController(ReceptionDbContext db) // .NET tự truyền vào đây
    {
        _db = db;
    }
}
```

Khi bạn đăng ký `AddDbContext<ReceptionDbContext>(...)`, bạn đang nói với .NET:
> *"Bất cứ khi nào ai đó cần `ReceptionDbContext`, hãy tạo một instance mới và truyền vào, tự động kết nối đúng connection string."*

### 3.2 Vòng Đời Scoped của DbContext

`AddDbContext` mặc định đăng ký theo **Scoped** — nghĩa là mỗi HTTP request sẽ dùng chung **1 instance** DbContext từ đầu đến cuối request đó, rồi tự động dispose.

```
Request A ──► DbContext_A (new) ──► dùng xong ──► dispose
Request B ──► DbContext_B (new) ──► dùng xong ──► dispose
```

Điều này đảm bảo:
- **Thread-safe**: Mỗi request có DbContext riêng, không tranh giành nhau.
- **Transaction scope**: Mọi thao tác trong một request nằm cùng một connection.

---

## 4. 🔑 Kiến Thức Chìa Khóa

### Tại Sao Mỗi Microservice Cần DbContext Riêng?

| | Monolithic (1 DbContext) | Microservices (nhiều DbContext) |
|---|---|---|
| **Tách biệt** | Tất cả bảng trong 1 DB | Mỗi service có DB riêng |
| **Độc lập** | Thay đổi 1 bảng ảnh hưởng cả hệ thống | Thay đổi schema không lan sang service khác |
| **Scale** | Phải scale toàn bộ | Scale từng service độc lập |
| **DB** | Bắt buộc dùng cùng 1 loại DB | Mỗi service chọn DB phù hợp nhất |

Trong dự án này:
- **ReceptionAPI** dùng **MS SQL** vì cần ACID Transaction mạnh (Outbox Pattern).
- **ClinicalAPI** dùng **Oracle** vì Oracle tốt với dữ liệu y tế lớn, phức tạp.

### Data Annotations vs Fluent API

Entity class của chúng ta dùng **kết hợp cả hai**:

```csharp
// Data Annotations — khai báo trực tiếp trên property (đơn giản, đọc hiểu nhanh)
[Required]
[MaxLength(100)]
public string FullName { get; set; }

// Fluent API trong OnModelCreating — cho cấu hình phức tạp hơn (index, constraint...)
modelBuilder.Entity<Patient>(entity =>
{
    entity.HasIndex(p => p.NationalId)
          .IsUnique()
          .HasFilter("[NationalId] IS NOT NULL"); // Partial index
});
```

> 💡 **Partial Index** là gì? Chỉ đánh index trên các hàng thỏa mãn điều kiện — ở đây chỉ index `NationalId` khi nó `NOT NULL`. Tiết kiệm đáng kể không gian và tốc độ insert so với index toàn bảng.

---

## 5. ⚙️ Hướng Dẫn Chạy Migration

Sau khi cấu hình xong, bạn cần tạo migration để sinh ra bảng trong DB.

### Bước 1 — Cài dotnet-ef tool (nếu chưa có)

```bash
dotnet tool install --global dotnet-ef
```

### Bước 2 — Tạo Migration cho ReceptionAPI (MS SQL)

```bash
# Chạy từ thư mục ReceptionAPI
cd ReceptionAPI
dotnet ef migrations add InitialCreate --context ReceptionDbContext
dotnet ef database update
```

### Bước 3 — Tạo Migration cho ClinicalAPI (Oracle)

```bash
# Chạy từ thư mục ClinicalAPI
cd ../ClinicalAPI
dotnet ef migrations add InitialCreate --context ClinicalDbContext
dotnet ef database update
```

> ⚠️ **Lưu ý:** Bạn cần có SQL Server và Oracle đang chạy (hoặc dùng Docker — xem Ngày 7) thì `database update` mới thành công. Nếu chỉ muốn sinh file migration để xem, bạn có thể chạy lệnh `migrations add` mà không cần DB.

---

## 6. ✅ Tổng Kết Kết Quả Ngày 1

| Hạng mục | Trạng thái |
|---|---|
| Solution `HospitalManagement` với 3 dự án | ✅ |
| Entity class `Patient`, `OutboxMessage` (MS SQL) | ✅ |
| Entity class `MedicalRecord`, `Medicine` (Oracle) | ✅ |
| `ReceptionDbContext` với cấu hình index | ✅ |
| `ClinicalDbContext` với cú pháp Oracle | ✅ |
| `Program.cs` hai dự án cấu hình DI | ✅ |
| `appsettings.json` với connection string | ✅ |
| Solution build thành công | ✅ |

---

*Tiếp theo — Ngày 2: Hiện thực hóa Outbox Pattern với ACID Transaction và BackgroundService 🚀*
