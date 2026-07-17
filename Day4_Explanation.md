# Ngày 4 — Tối Ưu Hiệu Năng Bằng Cache-Aside Pattern (Redis)

> **Mục tiêu:** Áp dụng mô hình Cache-Aside để giảm tải cho Oracle DB khi truy vấn danh sách thuốc (dữ liệu đọc nhiều, ít ghi). Xây dựng cơ chế chống Cache Stampede và đảm bảo hệ thống vẫn sống sót (Resilience) khi Redis mất kết nối.

---

## 🚀 Luồng Hoạt Động Của Cache-Aside

Trong `ClinicalAPI`, class `MedicineService` đóng vai trò điều phối luồng lấy dữ liệu thuốc. Mô hình **Cache-Aside** (Lazy Loading) được áp dụng như sau:

1. **Client (Bác sĩ)** yêu cầu lấy danh sách thuốc đang hoạt động.
2. **MedicineService** ưu tiên chọc vào **Redis** (`hospital:medicines`) trước tiên.
3. Nếu dữ liệu có sẵn (✅ **Cache Hit**), trả về ngay lập tức (Rất nhanh, ~1ms).
4. Nếu dữ liệu chưa có hoặc đã hết hạn (❌ **Cache Miss**):
   - Service sẽ tự động query xuống **Oracle DB** để lấy dữ liệu gốc.
   - Sau đó, lưu dữ liệu vừa lấy được vào lại Redis (với `TTL = 30 phút`).
   - Cuối cùng, trả dữ liệu về cho Client.

---

## 🛡️ Chống "Cache Stampede" (Bão Cache)

### Vấn Đề (Cache Stampede là gì?)
Giả sử có 1000 bác sĩ cùng lúc load trang kê đơn đúng vào giây thứ 31 (khi Cache vừa hết hạn và bị xóa khỏi Redis).
Lúc này, cả 1000 request đều nhận được **Cache Miss**. Nếu không có cơ chế bảo vệ, cả 1000 request này sẽ đồng loạt nã 1000 câu lệnh `SELECT` xuống Oracle DB cùng một mili-giây.
Hậu quả: Oracle DB quá tải, CPU tăng vọt, hệ thống có thể bị sập (Crash).

### Giải Pháp: `SemaphoreSlim` + Double-Check Locking
Đoạn code trong `MedicineService` giải quyết triệt để vấn đề này:

```csharp
private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
```

1. **Khóa độc quyền (Lock):** Khi xảy ra Cache Miss, các request phải xếp hàng trước `_semaphore.WaitAsync()`. Chỉ **duy nhất 1 request đầu tiên** được đi tiếp để query DB. 999 request còn lại bị đứng chờ.
2. **Double-Check Locking:** Khi request đầu tiên query DB xong và nạp lại dữ liệu vào Redis, nó sẽ nhả khóa (`_semaphore.Release()`). 
3. Các request đang chờ sau đó lần lượt được vào khóa. Nhưng chúng sẽ **kiểm tra Redis lại một lần nữa** (`doubleCheckCache`). Vì request đầu tiên đã nạp cache rồi, nên 999 request đi sau sẽ ăn ✅ **Cache Hit** ngay trong khóa và trả về dữ liệu, không ai chọc xuống DB nữa!

---

## 🛠️ Tính Bền Vững / Bỏ Qua Cache (Resilience & Bypass)

Trong kiến trúc Microservices, nguyên tắc sống còn là: **"Sự sập của một dịch vụ phụ trợ (như Cache) KHÔNG ĐƯỢC PHÉP làm sập luồng nghiệp vụ chính."**

### Vấn đề:
Nếu Redis Container bị tắt đột ngột, hàm `_redis.GetDatabase().StringGetAsync()` sẽ ném ra lỗi `RedisConnectionException`. Nếu ta không bắt lỗi này, API kê đơn của bác sĩ sẽ trả về mã lỗi 500 (Server Error) -> Toàn bệnh viện đình trệ chỉ vì... cái Cache sập.

### Giải pháp: Fallback to DB (Bypass)
Tôi đã bao bọc phần gọi Redis trong khối `try-catch`:

```csharp
catch (RedisConnectionException ex)
{
    _logger.LogWarning(ex, "⚠️ [REDIS OFFLINE] Redis is unavailable. Bypassing cache to query Oracle directly.");
    return await GetFromDatabaseAsync(cancellationToken);
}
```

Nếu Redis "chết", hệ thống sẽ:
1. Ghi log cảnh báo cho đội DevOps biết.
2. **Bypass hoàn toàn Redis**, đi thẳng đường vòng xuống Oracle DB để lấy dữ liệu.
3. Bác sĩ vẫn lấy được danh sách thuốc và kê đơn bình thường (dù tốc độ có thể chậm hơn vài chục mili-giây).

Đây chính là khái niệm **Fault Tolerance** (Chịu lỗi) cực kỳ quan trọng đối với các hệ thống phân tán cấp độ doanh nghiệp (Enterprise).
