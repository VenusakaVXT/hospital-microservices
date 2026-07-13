# 🏛️ Tổng Quan Kiến Trúc Hệ Thống Microservices Bệnh Viện

> **Dự án thực chiến 7 ngày** — Xây dựng hệ thống quản lý bệnh viện theo kiến trúc Microservices hiện đại với .NET 8, Kafka, Redis, Elasticsearch và Kubernetes.

---

## 🗺️ Kiến Trúc Tổng Thể

```
┌─────────────────────────────────────────────────────────────┐
│               🖥️  Winform Client (LAN Desktop)              │
│         Quầy tiếp đón — Gọi REST API qua mạng nội bộ        │
└──────────────────────────┬──────────────────────────────────┘
                           │ REST API
                           ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                     📦 Reception API  (Module 1)                             │
│                        MS SQL Server                                         │
│                                                                              │
│  ① Ghi thông tin hành chính bệnh nhân (Patients)                            │
│  ② Ghi bản tin sự kiện (OutboxMessages) trong cùng 1 Transaction            │
│  ③ Background Worker quét Outbox → Publish lên Kafka Broker                 │
└────────────────────────────┬─────────────────────────────────────────────────┘
                             │ Kafka Topic: hospital-patients
                             ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                     🏥 Clinical API  (Module 2)                              │
│                          Oracle DB                                           │
│                                                                              │
│  ① Lắng nghe Kafka → Tự động tạo lịch khám trống (MedicalRecords)          │
│  ② Cache danh mục thuốc vào Redis (Cache-Aside Pattern)                     │
│  ③ Đồng bộ bệnh án lên Elasticsearch → Full-text search cho bác sĩ         │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 🛠️ Công Nghệ Sử Dụng

| Thành phần | Công nghệ | Mục đích |
|---|---|---|
| **Backend API** | .NET 8 Web API | REST API cho từng Microservice |
| **Desktop Client** | WinForms (.NET 8) | Giao diện quầy lễ tân (LAN) |
| **Database 1** | MS SQL Server | Lưu thông tin hành chính bệnh nhân |
| **Database 2** | Oracle DB | Lưu hồ sơ bệnh án, danh mục thuốc |
| **Message Broker** | Apache Kafka | Giao tiếp bất đồng bộ giữa các service |
| **Cache** | Redis | Cache danh mục thuốc (Cache-Aside) |
| **Search Engine** | Elasticsearch | Full-text search hồ sơ bệnh án |
| **Containerization** | Docker & Docker Compose | Đóng gói và chạy hạ tầng |
| **Orchestration** | Kubernetes (K8s) | Điều phối và auto-scale container |
| **CI/CD** | GitHub Actions | Tự động test & build image |

---

## 📅 Cẩm Nang 7 Ngày Thực Chiến Cùng AI

---

### 📅 Ngày 1 — Phân Tích CSDL & Thiết Lập Cấu Trúc Solution

#### 🎯 Mục Tiêu Lý Thuyết
Hiểu cách thiết kế các bảng dữ liệu chuẩn nghiệp vụ bệnh viện, hiểu mối quan hệ 1-1, 1-N và cách cấu hình `DbContext` độc lập cho từng loại DB (MS SQL & Oracle).

#### ✅ Nhiệm Vụ
- [ ] Mở VS 2022, tạo một **Blank Solution** tên là `HospitalManagement`
- [ ] Thêm 2 dự án Web API (`ReceptionAPI`, `ClinicalAPI`) và 1 dự án Winform (`ReceptionClient`)
- [ ] Tạo file trống `Day1_Explanation.md` trong thư mục dự án

#### 🤖 Prompt Nhập Vào AI

```
Tôi đang xây dựng hệ thống Microservices bệnh viện. Hãy đóng vai là Data Architect,
phân tích chi tiết thiết kế CSDL (các trường dữ liệu, kiểu dữ liệu hiện đại trong C#,
khóa chính, khóa ngoại) cho:

- Phân vùng MS SQL (ReceptionAPI): Gồm bảng Patients (thông tin hành chính bệnh nhân)
  và bảng OutboxMessages (để lưu sự kiện đồng bộ).
- Phân vùng Oracle (ClinicalAPI): Gồm bảng MedicalRecords (hồ sơ bệnh án)
  và Medicines (danh mục thuốc).

Sau khi phân tích, hãy sinh toàn bộ code C# định nghĩa các Entity class,
file ReceptionDbContext (MS SQL), ClinicalDbContext (Oracle),
file Program.cs cấu hình Dependency Injection kết nối DB cho từng dự án.

Cuối cùng, hãy viết nội dung giải thích chi tiết luồng hoạt động của Ngày 1
bằng tiếng Việt dạng Markdown để tôi dán vào file Day1_Explanation.md.
```

#### 🏁 Kết Quả Cuối Ngày
> Cấu hình DB hoàn chỉnh, Solution build thành công (`Ctrl + Shift + B`), có file tài liệu giải thích kiến trúc Ngày 1.

---

### 📅 Ngày 2 — Hiện Thực Hóa Outbox Pattern tại Quầy Tiếp Đón (MS SQL)

#### 🎯 Mục Tiêu Lý Thuyết
Hiểu về **ACID Transaction** trong .NET Core (đảm bảo chèn dữ liệu bệnh nhân và bản tin outbox cùng thành công hoặc cùng rollback) và cách chạy tác vụ ngầm với `BackgroundService`.

#### ✅ Nhiệm Vụ
- [ ] Tạo thư mục `Services` và `BackgroundServices` trong dự án `ReceptionAPI`
- [ ] Tạo file `Day2_Explanation.md`

#### 🤖 Prompt Nhập Vào AI

```
Trong dự án ReceptionAPI (MS SQL), hãy viết một class PatientService có hàm
RegisterPatient(PatientDto dto). Sử dụng IDbContextTransaction để đảm bảo lưu
dữ liệu vào bảng Patients và serialize thông tin bệnh nhân lưu vào bảng
OutboxMessages diễn ra đồng thời, an toàn.

Tiếp theo, hãy viết một OutboxPublisherWorker kế thừa từ BackgroundService
chạy ngầm mỗi 5 giây để quét các message có IsProcessed == false, log tạm ra
console, rồi cập nhật trạng thái thành true.

Cuối cùng, hãy viết nội dung giải thích chi tiết luồng hoạt động, cơ chế
Transaction và cách BackgroundService hoạt động trong ngày hôm nay dưới dạng
Markdown để tôi lưu vào file Day2_Explanation.md.
```

#### 🏁 Kết Quả Cuối Ngày
> Gọi API test thử thấy dữ liệu lưu song song vào 2 bảng. Có file giải thích luồng Ngày 2.

---

### 📅 Ngày 3 — Cấu Hình Kafka Để Đồng Bộ Dữ Liệu Sang Clinical API (Oracle)

#### 🎯 Mục Tiêu Lý Thuyết
Hiểu cơ chế hoạt động của **Message Broker** (Producer/Consumer) giúp giao tiếp bất đồng bộ giữa các Microservices.

```
MS SQL  →  OutboxMessages  →  Kafka Broker  →  Oracle DB
          (Outbox Pattern)   (hospital-patients topic)
```

#### ✅ Nhiệm Vụ
- [ ] Cài package NuGet `Confluent.Kafka` cho cả 2 dự án API
- [ ] Tạo file `Day3_Explanation.md`

#### 🤖 Prompt Nhập Vào AI

```
Hãy thay thế phần log tạm thời của OutboxPublisherWorker ở Ngày 2 bằng một
KafkaProducer thực thụ sử dụng Confluent.Kafka để đẩy event vào topic 'hospital-patients'.

Tiếp theo, bên dự án ClinicalAPI (Oracle), hãy viết một KafkaConsumerWorker kế thừa
từ BackgroundService để lắng nghe liên tục topic này. Khi nhận được event bệnh nhân,
hãy tự động chèn một bản ghi hồ sơ khám trống cho bệnh nhân đó vào Oracle DB.

Cuối cùng, hãy viết nội dung giải thích luồng đi của dữ liệu từ:
MS SQL → Outbox → Kafka → Oracle
dưới dạng Markdown vào file Day3_Explanation.md.
```

#### 🏁 Kết Quả Cuối Ngày
> Hệ thống tự động đồng bộ dữ liệu ngầm qua Kafka giữa 2 DB khác nhau. Có file giải thích luồng Ngày 3.

---

### 📅 Ngày 4 — Tích Hợp Redis Cache Cho Danh Mục Thuốc (Clinical API)

#### 🎯 Mục Tiêu Lý Thuyết
Hiểu chiến lược **Cache-Aside** và cách sử dụng kiểu dữ liệu String/Hash trong Redis để giảm tải cho Oracle DB.

```
Request → Kiểm tra Redis → [HIT]  → Trả về dữ liệu cache (<5ms)
                         → [MISS] → Query Oracle DB → Lưu Redis (TTL: 30 phút) → Trả về
```

#### ✅ Nhiệm Vụ
- [ ] Cài package NuGet `StackExchange.Redis` vào `ClinicalAPI`
- [ ] Tạo file `Day4_Explanation.md`

#### 🤖 Prompt Nhập Vào AI

```
Trong dự án ClinicalAPI (Oracle), hãy viết một dịch vụ MedicineService có hàm
GetActiveMedicines(). Hãy dùng thư viện StackExchange.Redis để triển khai mô hình
Cache-Aside:

1. Tìm trong Redis trước với key 'hospital:medicines'
2. Nếu không có thì truy vấn từ Oracle DB qua EF Core
3. Lưu kết quả vào Redis với thời gian hết hạn là 30 phút
4. Trả về dữ liệu

Cuối cùng, hãy viết nội dung giải thích chi tiết luồng hoạt động của Cache-Aside
và cách tối ưu hiệu năng này dưới dạng Markdown vào file Day4_Explanation.md.
```

#### 🏁 Kết Quả Cuối Ngày
> API danh mục thuốc phản hồi cực nhanh (**< 5ms**) nhờ Redis cache. Có file giải thích luồng Ngày 4.

---

### 📅 Ngày 5 — Đồng Bộ Bệnh Án Lên Elasticsearch Để Tìm Kiếm Full-Text

#### 🎯 Mục Tiêu Lý Thuyết
Hiểu sự khác biệt giữa tìm kiếm quan hệ (RDBMS) và công cụ tìm kiếm chỉ mục (Search Engine) để tối ưu tính năng tra cứu văn bản lớn.

| | SQL `LIKE '%keyword%'` | Elasticsearch |
|---|---|---|
| **Tốc độ** | Chậm (full scan) | Nhanh (inverted index) |
| **Fuzzy search** | ❌ Không hỗ trợ | ✅ Tìm gần đúng |
| **Không dấu** | ❌ Cần xử lý thủ công | ✅ Built-in analyzer |
| **Scale** | Giới hạn | Phân tán ngang |

#### ✅ Nhiệm Vụ
- [ ] Cài package NuGet `Elastic.Clients.Elasticsearch` vào `ClinicalAPI`
- [ ] Tạo file `Day5_Explanation.md`

#### 🤖 Prompt Nhập Vào AI

```
Trong dự án ClinicalAPI, hãy viết một dịch vụ ElasticSearchService. Khi bác sĩ
cập nhật bệnh án (lưu vào Oracle), dịch vụ này sẽ tự động index dữ liệu bệnh án
đó lên Elasticsearch.

Viết thêm hàm SearchMedicalRecords(string keyword) để bác sĩ tìm kiếm full-text
search theo từ khóa không dấu hoặc gần đúng.

Cuối cùng, hãy viết nội dung giải thích tại sao cần dùng Elasticsearch thay vì
câu lệnh LIKE trong SQL và luồng đồng bộ này hoạt động thế nào dưới dạng Markdown
vào file Day5_Explanation.md.
```

#### 🏁 Kết Quả Cuối Ngày
> Hệ thống có khả năng tìm kiếm bệnh án thông minh siêu tốc. Có file giải thích luồng Ngày 5.

---

### 📅 Ngày 6 — Xây Dựng Giao Diện Winform Gọi API Bất Đồng Bộ (Async/Await)

#### 🎯 Mục Tiêu Lý Thuyết
Hiểu lý do tại sao Client **không được kết nối trực tiếp DB** (bảo mật) và cách lập trình bất đồng bộ (`async/await`) để giữ cho giao diện UI không bị đơ.

```
Winform UI  →  HttpClient (async/await)  →  ReceptionAPI  →  MS SQL
            ↑                                              ↑
     UI không bị freeze                          Không lộ connection string
```

#### ✅ Nhiệm Vụ
- [ ] Mở dự án Winform trong VS 2022 ở chế độ Design
- [ ] Kéo thả giao diện: ô nhập **Tên**, **Năm sinh** và nút **"Đăng ký"**
- [ ] Tạo file `Day6_Explanation.md`

#### 🤖 Prompt Nhập Vào AI

```
Hãy viết mã nguồn file code-behind (.cs) cho nút bấm đăng ký trong dự án Winform
ReceptionClient. Khi click, sử dụng HttpClient để gửi request POST dạng JSON đến
địa chỉ API trung gian ReceptionAPI (chứ không kết nối thẳng DB).

Phải áp dụng async/await chuẩn xác để luồng UI giao diện không bị treo khi chờ
phản hồi mạng.

Cuối cùng, hãy viết nội dung giải thích lợi ích bảo mật của mô hình này và nguyên
lý hoạt động của async/await trong Winform dưới dạng Markdown vào file
Day6_Explanation.md.
```

#### 🏁 Kết Quả Cuối Ngày
> Winform chạy mượt mà, bấm nút dữ liệu đẩy qua API an toàn. Có file giải thích luồng Ngày 6.

---

### 📅 Ngày 7 — Đóng Gói Docker, Cấu Hình Kubernetes & Pipeline CI/CD

#### 🎯 Mục Tiêu Lý Thuyết
Hiểu quy trình **DevOps hiện đại**: Đóng gói môi trường (Docker), Điều phối container (K8s) và Tự động hóa tích hợp (CI/CD).

```
Code Push → GitHub Actions (CI) → Build & Test → Docker Image → K8s Deployment (CD)
```

#### ✅ Nhiệm Vụ
- [ ] Tạo các file cấu hình: `Dockerfile`, `docker-compose.yml`, `deployment.yaml`
- [ ] Tạo thư mục `.github/workflows/` và file `ci-cd.yml`
- [ ] Tạo file `Day7_Explanation.md`

#### 🤖 Prompt Nhập Vào AI

```
Hãy viết nội dung cho các file cấu hình hạ tầng sau:

1. File Dockerfile đa tầng tối ưu cho ứng dụng .NET 8 Web API.

2. File docker-compose.yml định nghĩa đầy đủ các container:
   MS SQL, Oracle XE, Kafka, Redis, Elasticsearch
   để tôi khởi chạy toàn bộ hạ tầng bằng 1 câu lệnh.

3. File K8s deployment.yaml mẫu để quản lý và tự động scale ứng dụng API.

4. File .github/workflows/ci-cd.yml cấu hình pipeline GitHub Actions tự động
   chạy test và build docker image khi push code.

Cuối cùng, hãy viết một bài tổng hợp kiến trúc toàn bộ hệ thống Microservices
bệnh viện từ ngày 1 đến ngày 7 dưới dạng Markdown vào file Day7_Explanation.md.
```

#### 🏁 Kết Quả Cuối Ngày
> Toàn bộ dự án được đóng gói chuẩn **Cloud-Native**, sở hữu tài liệu tổng quan hệ thống chất lượng cao.

---

## 📊 Tổng Hợp Lộ Trình 7 Ngày

| Ngày | Chủ Đề | Công Nghệ | Đầu Ra |
|:---:|---|---|---|
| **1** | CSDL & Solution Setup | EF Core, MS SQL, Oracle | Entity classes, DbContext, DI config |
| **2** | Outbox Pattern | Transaction, BackgroundService | PatientService, OutboxWorker |
| **3** | Message Broker | Apache Kafka, Confluent.Kafka | KafkaProducer, KafkaConsumer |
| **4** | Caching | Redis, StackExchange.Redis | MedicineService (Cache-Aside) |
| **5** | Full-text Search | Elasticsearch | ElasticSearchService, SearchAPI |
| **6** | Desktop Client | WinForms, HttpClient, Async/Await | Giao diện đăng ký bệnh nhân |
| **7** | DevOps | Docker, Kubernetes, GitHub Actions | Dockerfile, K8s YAML, CI/CD Pipeline |

---

## 📁 Cấu Trúc Solution

```
HospitalManagement/
├── 📂 ReceptionAPI/                  # Module 1 - MS SQL Server
│   ├── Controllers/
│   ├── Services/
│   │   └── PatientService.cs
│   ├── BackgroundServices/
│   │   └── OutboxPublisherWorker.cs
│   ├── Models/
│   │   ├── Patient.cs
│   │   └── OutboxMessage.cs
│   ├── Data/
│   │   └── ReceptionDbContext.cs
│   └── Program.cs
│
├── 📂 ClinicalAPI/                   # Module 2 - Oracle DB
│   ├── Controllers/
│   ├── Services/
│   │   ├── MedicineService.cs
│   │   └── ElasticSearchService.cs
│   ├── BackgroundServices/
│   │   └── KafkaConsumerWorker.cs
│   ├── Models/
│   │   ├── MedicalRecord.cs
│   │   └── Medicine.cs
│   ├── Data/
│   │   └── ClinicalDbContext.cs
│   └── Program.cs
│
├── 📂 ReceptionClient/               # WinForms Desktop App
│   ├── Forms/
│   │   └── MainForm.cs
│   └── Program.cs
│
├── 📄 Dockerfile
├── 📄 docker-compose.yml
├── 📄 deployment.yaml
├── 📂 .github/
│   └── workflows/
│       └── ci-cd.yml
│
├── 📄 Day1_Explanation.md
├── 📄 Day2_Explanation.md
├── 📄 Day3_Explanation.md
├── 📄 Day4_Explanation.md
├── 📄 Day5_Explanation.md
├── 📄 Day6_Explanation.md
├── 📄 Day7_Explanation.md
└── 📄 OVERVIEW_PLAN.md              # ← File này
```

---

*Chúc bạn học tốt và xây dựng thành công hệ thống! 🚀*