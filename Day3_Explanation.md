# Ngày 3 — Tích Hợp Apache Kafka: Kết Nối Giữa Các Microservices

> **Mục tiêu:** Hiểu và cấu hình Apache Kafka đóng vai trò là Message Broker trung tâm, nhận sự kiện từ MS SQL (ReceptionAPI) và phân phối tới Oracle (ClinicalAPI).

---

## 🎯 Kiến Trúc Tổng Quan: Luồng Dữ Liệu Ngày 3

Kiến trúc hiện tại đã hoàn thiện theo mẫu Outbox Pattern kết hợp với Message Broker:

```
[ ReceptionAPI (Producer) ]                   [ Apache Kafka ]                  [ ClinicalAPI (Consumer) ]
                                                     │                                     
1. Lưu Patient (MS SQL)                              │                                     
2. Lưu OutboxMessage (IsProcessed=false)             │                                     
         │                                           │                                     
3. OutboxPublisherWorker quét định kỳ                │                                     
         │                                           │                                     
         ├──► 4. Publish Event ──────────────────────► Topic: `hospital-patients`          
         │                                           │             │                       
5. Cập nhật IsProcessed=true                         │             ├──► 6. KafkaConsumerWorker lắng nghe
                                                     │             │                       
                                                     │             └──► 7. Tạo MedicalRecord (Oracle)
```

---

## 🛠️ Chi Tiết Cấu Hình Producer & Consumer

### 1. ReceptionAPI: `OutboxPublisherWorker` (Producer)

Được nâng cấp từ Ngày 2 (chỉ log ra console) sang đẩy dữ liệu thật.

**Cấu hình Producer quan trọng (`ProducerConfig`):**
- `BootstrapServers`: Địa chỉ của Kafka (e.g., `localhost:9092`).
- `Acks = Acks.All`: Chế độ an toàn nhất. Producer chỉ coi là gửi thành công khi **tất cả** các bản sao (replicas) của Kafka Broker đều đã xác nhận ghi nhận message.
- `MessageSendMaxRetries`: Số lần gửi lại tối đa nếu gặp lỗi mạng chập chờn.

**Xử lý trong vòng lặp:**
1. Quét `OutboxMessages` với `IsProcessed == false`.
2. Khởi tạo `Message<Null, string>` với payload là JSON.
3. Chờ kết quả trả về bằng `ProduceAsync()`.
4. Nếu thành công: Cập nhật `IsProcessed = true` (Đảm bảo *At-Least-Once delivery*).
5. Nếu thất bại (`ProduceException`): Catch lỗi, dừng xử lý batch đó để lần quét sau xử lý lại.

### 2. ClinicalAPI: `KafkaConsumerWorker` (Consumer)

Một `BackgroundService` chạy ngầm hoàn toàn độc lập với các HTTP Requests.

**Cấu hình Consumer quan trọng (`ConsumerConfig`):**
- `BootstrapServers`: Địa chỉ Kafka.
- `GroupId`: Tên nhóm (Ví dụ: `clinical-api-group`). Tính năng này giúp Kafka theo dõi Offset cho riêng ClinicalAPI. Nếu sau này ta có một dịch vụ khác (vd: `BillingAPI`) thì sẽ dùng một GroupId khác để nhận cùng event đó.
- `AutoOffsetReset = AutoOffsetReset.Earliest`: Nếu Consumer mới kết nối lần đầu (chưa có offset), nó sẽ đọc từ message cũ nhất có trong topic, không bỏ sót cái nào.
- `EnableAutoCommit = false`: Tắt tự động commit offset. Chúng ta sẽ **commit thủ công** sau khi đã lưu thành công vào Oracle, nhằm tránh mất dữ liệu nếu app crash giữa chừng.

**Xử lý Message:**
1. Block luồng `consumer.Consume(stoppingToken)` để chờ event mới.
2. Khi có event: Parse JSON (lấy `PatientId`, `FullName`).
3. Dùng `IServiceScopeFactory` tạo scope để truy xuất `ClinicalDbContext`.
4. Kiểm tra xem bệnh nhân đã có hồ sơ "Pending" chưa (ngăn ngừa duplicate event).
5. Tạo `MedicalRecord` và lưu vào Oracle.
6. Sau khi `SaveChangesAsync()` thành công, gọi `consumer.Commit()` để báo với Kafka.

---

## 🛡️ Xử Lý Lỗi Và Tính Bền Vững (Resilience)

Điều gì xảy ra nếu các thành phần hệ thống gặp sự cố?

### 1. Nếu Kafka Bị Sập (Offline)
- **Bên ReceptionAPI:** Người dùng vẫn đăng ký bệnh nhân bình thường vì việc ghi vào MS SQL (Patients + OutboxMessages) không phụ thuộc Kafka.
- **OutboxWorker:** Sẽ gặp lỗi khi gọi `ProduceAsync()`. Worker log ra lỗi và bỏ qua message đó. `IsProcessed` vẫn là `false`.
- **Khi Kafka khôi phục:** Ở lần quét (5 giây) tiếp theo, Worker kết nối lại được Kafka và đẩy toàn bộ lượng message ứ đọng. Không có bệnh nhân nào bị sót.

### 2. Nếu Oracle Bị Sập (Offline)
- **Bên ClinicalAPI:** Consumer lấy được message từ Kafka, nhưng gọi `db.SaveChangesAsync()` vào Oracle sẽ ném ra Exception.
- Nhờ `EnableAutoCommit = false`, Consumer không commit offset lên Kafka.
- Lần tới khởi động lại, Consumer sẽ **đọc lại đúng message đó** từ Kafka và thử lưu lại vào Oracle.

### 3. Vấn Đề Gửi Trùng Lặp (Idempotency)
Vì sao Consumer phải kiểm tra `db.MedicalRecords.AnyAsync(...)` trước khi tạo?
Kafka tuân theo tiêu chí **At-Least-Once Delivery** (Ít nhất một lần). Có nghĩa là trong các sự cố mạng hy hữu, Kafka có thể gửi 1 event 2 lần.
Do đó, ClinicalAPI (Consumer) cần thiết kế theo hướng **Idempotent** (Dù nhận cùng 1 sự kiện bao nhiêu lần, kết quả trong Database vẫn không đổi) bằng cách check trùng dữ liệu.

---

## ✅ Kết Quả Cuối Ngày 3

- Bệnh nhân đăng ký bên ReceptionAPI (MS SQL) sẽ có thông tin tự động nhảy sang ClinicalAPI (Oracle).
- Hệ thống hoạt động theo nguyên tắc eventual consistency (nhất quán cuối).
- Kiến trúc decouple hoàn toàn: ReceptionAPI và ClinicalAPI không cần biết địa chỉ IP hay tình trạng hoạt động trực tiếp của nhau. Mọi thứ thông qua Apache Kafka.
