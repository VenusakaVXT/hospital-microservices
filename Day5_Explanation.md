# Ngày 5 — Tìm Kiếm Nâng Cao với Elasticsearch (Full-text Search)

> **Mục tiêu:** Tích hợp bộ máy tìm kiếm văn bản mạnh mẽ nhất thế giới (Elasticsearch) vào hệ thống bệnh viện. Giúp bác sĩ có thể gõ tìm kiếm không dấu, sai lỗi chính tả, hoặc tìm các triệu chứng/chẩn đoán phức tạp mà cơ sở dữ liệu truyền thống (như Oracle, SQL Server) xử lý rất vất vả.

---

## 1. Tại Sao Lại Là Elasticsearch Mà Không Phải SQL `LIKE %...%`?

Khi bác sĩ tìm kiếm bệnh án với từ khóa dài, ví dụ: *"Đau đầu, chóng mặt, nghi ngờ rối loạn tiền đình"*, nếu dùng SQL thông thường:
```sql
SELECT * FROM MEDICAL_RECORDS WHERE Symptoms LIKE '%Đau đầu%' OR Diagnosis LIKE '%chóng mặt%'
```
### Vấn đề của SQL `LIKE`:
1. **Full Table Scan (Quét toàn bộ bảng):** Toán tử `LIKE %...%` khiến DB không thể dùng Index B-Tree thông thường, nó phải duyệt qua từng dòng một (có thể là hàng triệu bệnh án), gây quá tải CPU và thắt cổ chai DB.
2. **Không thông minh:** Nó tìm chuỗi chính xác từng chữ. Nếu bác sĩ gõ sai chính tả *"Chong mat"* (không dấu), kết quả sẽ bằng 0.
3. **Không có "Độ liên quan" (Score):** SQL chỉ trả về CÓ hoặc KHÔNG. Nó không biết bệnh án nào liên quan NHIỀU NHẤT để xếp lên đầu.

### Sự Vượt Trội Của Elasticsearch (Inverted Index):
Elasticsearch hoạt động giống như **Mục lục cuối cuốn sách**. 
Khi bệnh án được lưu vào Elasticsearch, đoạn text dài sẽ được băm nhỏ thành các "Token" (VD: `đau`, `đầu`, `chóng`, `mặt`) thông qua cơ chế Analyzer.
- Tốc độ truy xuất chỉ trong vài mili-giây cho dù có hàng chục triệu bản ghi.
- Có khả năng xử lý **Tiếng Việt không dấu** thông qua `Asciifolding Filter`.
- Hỗ trợ **Fuzzy Search** (tìm kiếm mờ/sai chính tả) và MultiMatch (tìm trên nhiều cột cùng lúc).
- **Scoring (Điểm số TF-IDF / BM25):** Bệnh án nào chứa nhiều từ khóa quan trọng hơn sẽ được xếp hạng (Rank) lên trước.

---

## 2. Luồng Đồng Bộ Dữ Liệu: Oracle DB -> Elasticsearch

Trong Microservices, Database SQL (Oracle) vẫn đóng vai trò là "Nguồn chân lý" (Source of Truth) cho tính toàn vẹn (ACID), còn Elasticsearch đóng vai trò là "Read Model" (Mô hình đọc tối ưu).

Luồng xử lý (Data Synchronization) diễn ra như sau:

1. Bệnh án được thêm mới, cập nhật hoặc xóa thông qua API của `MedicalRecordsController` hoặc từ `KafkaConsumerWorker`.
2. Dữ liệu được EF Core lưu thành công vào **Oracle DB** (Bảo đảm an toàn dữ liệu).
3. Ngay sau khi lệnh `SaveChangesAsync()` thực thi xong, hệ thống sẽ kích hoạt lệnh gọi hàm `_elasticService.IndexMedicalRecordAsync(...)` (hoặc `DeleteMedicalRecordAsync`).
4. `ElasticSearchService` đóng gói bệnh án dưới dạng JSON và bắn qua cổng 9200 của cụm Elasticsearch. Dữ liệu lúc này được Index vào bộ lưu trữ NoSQL của nó.

Khi bác sĩ thực hiện lệnh tìm kiếm (`GET /api/medicalrecords/search?keyword=...`), Controller sẽ đi thẳng xuống Elasticsearch để lấy kết quả thay vì chọc vào Oracle DB. Điều này giúp giảm tải triệt để cho Database chính của bệnh viện!
